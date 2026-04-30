using ErrorOr;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Interfaces.Wrappers;
using FileKeeper.Core.Models;
using FileKeeper.Core.Models.DTOs;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Models.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileKeeper.Core.UseCases;

public class CreateBackupUseCase : ICreateBackupUseCase
{
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly IFileWrapper _fileWrapper;
    private readonly ICompressedEncryptedFileWriter _compressedEncryptedFileWriter;
    private readonly IOptionsMonitor<UserSettingsOptions> _userSettingsOptions;
    private readonly ILogger<CreateBackupUseCase> _logger;

    public CreateBackupUseCase(
        ISnapshotRepository snapshotRepository,
        IFileWrapper fileWrapper,
        ICompressedEncryptedFileWriter compressedEncryptedFileWriter,
        IOptionsMonitor<UserSettingsOptions> userSettingsOptions,
        ILogger<CreateBackupUseCase> logger)
    {
        _snapshotRepository = snapshotRepository;
        _fileWrapper = fileWrapper;
        _compressedEncryptedFileWriter = compressedEncryptedFileWriter;
        _userSettingsOptions = userSettingsOptions;
        _logger = logger;

        _userSettingsOptions.OnChange(_ =>
        {
            // Handle UserSettings changes here if needed
            // For example, log or notify about changes
        });
    }

    public async Task<ErrorOr<Snapshot>> ExecuteAsync(IProgress<BackupProgress>? progress, CancellationToken token)
    {
        _logger.LogInformation("Starting backup creation process.");
        
        var configuration = _userSettingsOptions.CurrentValue;
        
        var lastSnapshotResult = await _snapshotRepository.GetLastSnapshotAsync(token);
        if (lastSnapshotResult.IsError && lastSnapshotResult.FirstError.Type != ErrorType.NotFound)
        {
            return lastSnapshotResult.Errors;
        }

        var lastSnapshot = lastSnapshotResult.IsError ? null : lastSnapshotResult.Value;

        var newSnapshot = Snapshot.Create();
        
        LogSnapshotsInfo(newSnapshot, lastSnapshot);

        foreach (var sourceDirectory in configuration.SourceDirectories)
        {
            if (token.IsCancellationRequested) break;

            var filesOnDisk = _fileWrapper.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);

            // TODO: think about Parallel

            var currentFileIndex = 0;
            var totalFiles = filesOnDisk.Length;

            foreach (var fileOnDisk in filesOnDisk)
            {
                if (token.IsCancellationRequested) break;

                currentFileIndex++;
                
                progress?.Report(new BackupProgress
                {
                    CurrentFileIndex = currentFileIndex,
                    TotalFiles = totalFiles,
                    CurrentFileName = fileOnDisk,
                    CurrentFolder = sourceDirectory
                });
                
                if (CheckShouldIgnoreFolder(configuration.IgnoredFolders, fileOnDisk))
                {
                    _logger.LogInformation("Processing '{FilePath}': Skipping because it is in an ignored folder.", fileOnDisk);
                    continue;
                }

                var storeFile = false;

                var fileToSaveResult = await CreateFileToSaveAsync(fileOnDisk, sourceDirectory, token);
                if (fileToSaveResult.IsError)
                    continue;

                var fileToSave = fileToSaveResult.Value;

                var existingFile = lastSnapshot?.Files.FirstOrDefault(f =>
                    f.SourceDirectory == sourceDirectory
                    && f.RelativePath == fileToSave.RelativePath);

                if (existingFile == null)
                {
                    // Is a new file: we need to add it to the data structure
                    fileToSave.UpdateFoundIn(newSnapshot.SnapshotName);
                    storeFile = true;
                    
                    _logger.LogInformation("Processing '{FilePath}': new file", fileOnDisk);
                    _logger.LogDebug("New file hash: {NewHash}", fileToSave.Hash);
                }
                else if (existingFile.Hash != fileToSave.Hash)
                {
                    // File exists, but hash is different: store its data structure
                    fileToSave.UpdateFoundIn(newSnapshot.SnapshotName);
                    storeFile = true;
                    
                    _logger.LogInformation("Processing '{FilePath}': file changed", fileOnDisk);
                    _logger.LogTrace("Existing file hash: {ExistingHash}, New file hash: {NewHash}", existingFile.Hash, fileToSave.Hash);
                }
                else
                {
                    // File exists and hash is the same: we can reuse the stored file from the last snapshot

                    _logger.LogInformation("Processing '{FilePath}': file unchanged", fileOnDisk);
                }
                
                if (token.IsCancellationRequested) break;

                if (storeFile)
                {
                    var storeFileResult = await StoreFileAsync(configuration, fileToSave, token);
                    
                    if (!storeFileResult.IsError)
                    {
                        newSnapshot.AddFile(
                            FileEntry.Create(
                                sourceDirectory,
                                fileToSave.RelativePath,
                                fileToSave.StoredPath,
                                fileToSave.Hash,
                                fileToSave.Size,
                                fileToSave.LastModified,
                                fileToSave.FoundInSnapshot));
                    }
                }
                else if (existingFile != null)
                {
                    newSnapshot.AddFile(existingFile);
                }
            }
        }
        
        if (token.IsCancellationRequested)
            return Error.Unexpected(description: "Operation cancelled");
        
        newSnapshot.SortFiles();

        var addSnapshotResult = await _snapshotRepository.AddSnapshotAsync(newSnapshot, token);
        if (addSnapshotResult.IsError)
            return addSnapshotResult.Errors;
        
        _logger.LogInformation("Backup creating process finished");

        return newSnapshot;
    }

    private void LogSnapshotsInfo(Snapshot newSnapshot, Snapshot? lastSnapshot)
    {
        _logger.LogInformation("Created new Snapshot {SnapshotName}", newSnapshot.SnapshotName);

        if (lastSnapshot != null)
            _logger.LogInformation("Last snapshot found: {SnapshotName} created on {CreatedOn}", lastSnapshot.SnapshotName, lastSnapshot.CreatedAtUtc);
        else
            _logger.LogInformation("No previous snapshot found. This will be the first backup.");
    }

    private async Task<ErrorOr<Success>> StoreFileAsync(UserSettingsOptions configuration, FileToSave fileToSave, CancellationToken token)
    {
        var fullPath = Path.Combine(configuration.StorageDirectory, "data", fileToSave.StoredPath);
        var dir = Path.GetDirectoryName(fullPath);
        
        _fileWrapper.CreateDirectoryIfNotExists(dir!);

        var compressResult =
            await _compressedEncryptedFileWriter.CompressFromStreamToFileAsync(fileToSave.FullPath, fullPath, token);

        if (compressResult.IsError)
            return compressResult.Errors;
        
        return Result.Success;
    }

    private async Task<ErrorOr<FileToSave>> CreateFileToSaveAsync(string fileOnDisk, string sourceDirectory, CancellationToken token)
    {
        var relativePath = Path.GetRelativePath(sourceDirectory, fileOnDisk);

        var guid = Guid.CreateVersion7().ToString("N");
        var storedPath = $"{guid[..8]}/{guid}";

        try
        {
            var fileInfo = await _fileWrapper.GetFileMetadataAsync(fileOnDisk, token);

            return new FileToSave(
                fullPath: fileOnDisk,
                storedPath: storedPath,
                relativePath: relativePath,
                hash: fileInfo.Hash,
                size: fileInfo.Size,
                lastModified: fileInfo.LastModified);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file metadata for '{FilePath}'", fileOnDisk);
            return Error.Failure(description: $"Failed to get file metadata for '{fileOnDisk}'");
        }
    }

    private bool CheckShouldIgnoreFolder(string[] ignoredFolders, string path)
    {
        if (ignoredFolders.Length == 0)
            return false;

        var pathComponents = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        return ignoredFolders.Any(ignoreFolder =>
            pathComponents.Any(component =>
                component.Equals(ignoreFolder, StringComparison.OrdinalIgnoreCase)));
    }
}