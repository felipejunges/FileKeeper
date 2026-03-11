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
        
        // se a pasta nao existe, criar
        var directoryValidation = ValidateDestinationDirectory(destinationFolder);
        if (directoryValidation.IsError)
            return directoryValidation.Errors;

        // recupera o backup do banco de dados com streaming (one file at a time)
        var filesToRecoverStream = await _fileRepository.GetStreamOfFilesToRecoverAsync(backupId, token);
        var fileIndex = 0;
        
        _logger.LogInformation(
            "Starting backup restoration for backup ID {BackupId} to destination folder '{DestinationFolder}'.",
            backupId,
            destinationFolder);

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

                var bytes = fileToRecover.Content ?? [];
                var decompressed = await CompressionHelper.DecompressAsync(bytes, token);
                
                await using var fileStream = new FileStream(finalDestinationName, FileMode.Create, FileAccess.Write);
                await fileStream.WriteAsync(decompressed, token);
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