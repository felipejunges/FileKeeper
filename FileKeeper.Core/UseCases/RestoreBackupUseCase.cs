using ErrorOr;
using FileKeeper.Core.Helpers;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Models;
using Microsoft.Extensions.Logging;

namespace FileKeeper.Core.UseCases;

public class RestoreBackupUseCase : IRestoreBackupUseCase
{
    private readonly IFileRepository _fileRepository;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<RestoreBackupUseCase> _logger;

    public RestoreBackupUseCase(
        IFileRepository fileRepository,
        IFileSystem fileSystem, ILogger<RestoreBackupUseCase> logger)
    {
        _fileRepository = fileRepository;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> ExecuteAsync(long backupId, string destinationFolder, IProgress<RestoreProgress>? progress, CancellationToken token)
    {
        _logger.LogInformation(
            "Initiating backup restoration process for backup ID {BackupId} to destination folder '{DestinationFolder}'.",
            backupId,
            destinationFolder);
        
        // create the folder, if it does not exist
        var directoryValidation = ValidateDestinationDirectory(destinationFolder);
        if (directoryValidation.IsError)
            return directoryValidation.Errors;

        _logger.LogInformation(
            "Starting backup restoration for backup ID {BackupId} to destination folder '{DestinationFolder}'.",
            backupId,
            destinationFolder);
        
        // recover the files to restore via stream
        var filesToRecoverStream = await _fileRepository.GetStreamOfFilesToRecoverAsync(backupId, token);
        
        _logger.LogDebug(
            "Retrieved stream of files to recover for backup ID {BackupId}. Beginning file restoration process.",
            backupId);
        
        var fileIndex = 0;

        await foreach (var fileToRecover in filesToRecoverStream.WithCancellation(token))
        {
            fileIndex++;
            
            // Report progress
            progress?.Report(new RestoreProgress
            {
                CurrentFileIndex = fileIndex,
                CurrentFileName = fileToRecover.FileName,
                CurrentFolder = fileToRecover.RelativePath
            });

            var finalDestinationName = Path.Combine(
                destinationFolder,
                fileToRecover.BackupPath.TrimStart(Path.DirectorySeparatorChar),
                fileToRecover.RelativePath.TrimStart(Path.DirectorySeparatorChar),
                fileToRecover.FileName);

            try
            {
                var directory = Path.GetDirectoryName(finalDestinationName)!;
                if (!_fileSystem.DirectoryExists(directory))
                    _fileSystem.CreateDirectory(directory);
                
                _logger.LogInformation("Restoring file '{FileName}' to '{FinalDestinationName}'.", fileToRecover.FileName, finalDestinationName);

                // Fetch content SEPARATELY for this file only (not from the stream which loads all)
                var contentResult = await _fileRepository.GetFileContentAsync(fileToRecover.Id, token);
                if (contentResult.IsError)
                    return contentResult.Errors;

                byte[] bytes = contentResult.Value;
                if (bytes.Length == 0)
                {
                    _logger.LogWarning("File '{FileName}' has no content. Skipping restoration for this file.", fileToRecover.FileName);
                    continue;
                }

                byte[] decompressed = null!;
                try
                {
                    decompressed = await CompressionHelper.DecompressAsync(bytes, token);
                    
                    await using var fileStream = new FileStream(finalDestinationName, FileMode.Create, FileAccess.Write);
                    await fileStream.WriteAsync(decompressed, token);
                }
                finally
                {
                    // Clear arrays immediately after use
                    Array.Clear(bytes);
                    if (decompressed.Length > 0)
                        Array.Clear(decompressed);
                    
                    // Force garbage collection only when necessary (large files or memory pressure)
                    // In .NET 10+, the GC is more efficient, so be selective about forcing collections
                    if (fileIndex % 50 == 0 || fileToRecover.Size > 50_000_000)
                    {
                        // Only collect if memory usage justifies it
                        GC.Collect(generation: 1, mode: GCCollectionMode.Optimized);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(
                    e, 
                    "An error occurred while restoring file '{FileName}' to '{FinalDestinationName}'.",
                    fileToRecover.FileName,
                    finalDestinationName);
                
                return Error.Unexpected(description: $"An error has happened while trying to restore file '{fileToRecover.FileName}': {e.Message}");
            }
        }
        
        _logger.LogInformation(
            "Backup restoration process completed successfully for backup ID {BackupId} to destination folder '{DestinationFolder}' ({FileCount} files restored).",
            backupId,
            destinationFolder,
            fileIndex);

        return Result.Success;
    }

    private ErrorOr<Success> ValidateDestinationDirectory(string destinationFolder)
    {
        if (!_fileSystem.DirectoryExists(destinationFolder))
        {
            _fileSystem.CreateDirectory(destinationFolder);
            _logger.LogInformation("Creating new directory for backup restoration: {DestinationFolder}", destinationFolder);
            return Result.Success;
        }

        if (!_fileSystem.IsDirectoryEmpty(destinationFolder))
        {
            _logger.LogWarning("The destination directory '{DestinationFolder}' is not empty. Backup restoration requires an empty directory.", destinationFolder);
            return Error.Failure(description: "The destination folder must be empty to restore the backup.");
        }

        return Result.Success;
    }
}