using ErrorOr;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Interfaces.Wrappers;
using FileKeeper.Core.Models;
using Microsoft.Extensions.Logging;

namespace FileKeeper.Core.UseCases;

public class DeleteBackupUseCase : IDeleteBackupUseCase
{
    private readonly ISnapshotService _snapshotService;
    private readonly IFileWrapper _fileWrapper;
    private readonly ILogger<DeleteBackupUseCase> _logger;

    public DeleteBackupUseCase(
        ISnapshotService snapshotService,
        IFileWrapper fileWrapper,
        ILogger<DeleteBackupUseCase> logger)
    {
        _snapshotService = snapshotService;
        _fileWrapper = fileWrapper;
        _logger = logger;
        _snapshotService = snapshotService;
    }

    public async Task<ErrorOr<Success>> ExecuteAsync(Guid snapshotId, IProgress<BackupProgress>? progress, CancellationToken token)
    {
        _logger.LogInformation("Starting backup deletion process.");
        
        var snapshotIndexResult = await _snapshotService.GetIndexAsync(token);
        if (snapshotIndexResult.IsError)
            return snapshotIndexResult.Errors;

        var snapshotIndex = snapshotIndexResult.Value;
        
        var snapshot = snapshotIndex.Snapshots.FirstOrDefault(s => s.Id == snapshotId);
        if (snapshot is null)
        {
            _logger.LogInformation("Snapshot {SnapshotId} doesn't exist.", snapshotId);
            return Error.NotFound($"Snapshot {snapshotId} doesn't exist.");
        }

        var snapshotIx = snapshotIndex.Snapshots.IndexOf(snapshot) + 1;
        var nextSnapshot = snapshotIx < snapshotIndex.Snapshots.Count ? snapshotIndex.Snapshots[snapshotIx] : null;
        
        var filesToDelete = new List<string>();
        
        foreach (var fileEntry in snapshot.Files)
        {
            var fullPath = Path.Combine(fileEntry.SourceDirectory, fileEntry.RelativePath);
            
            // find a file entry in the next snapshot pointing to the current fileEntry
            var fileEntryInNextSnapshot = nextSnapshot?.Files.FirstOrDefault(f => 
                f.SourceDirectory == fileEntry.SourceDirectory
                && f.RelativePath == fileEntry.RelativePath); // TODO: create compare in model

            if (fileEntryInNextSnapshot is null)
            {
                // file does not exists in the next snapshot, so delete it
                _logger.LogInformation("File {FilePath}: deleting, does not exists in next snapshot", fullPath);
                
                filesToDelete.Add(fileEntry.StoredPath);
            }
            else if (fileEntryInNextSnapshot.Hash != fileEntry.Hash || fileEntryInNextSnapshot.FoundInSnapshot != snapshot.SnapshotName)
            {
                // file exists in the next snapshot, but has a different hash or do not point to current FileEntry: delete it
                _logger.LogInformation("File {FilePath}: deleting, exists in next snapshot but different", fullPath);
                
                filesToDelete.Add(fileEntry.StoredPath);
            }
            else
            {
                // file exists in the next snapshot, has the same hash and point to the current Snapshot: keep it!
                _logger.LogInformation("File {FilePath}: skipping deletion, exists in next snapshot", fullPath);
                
                fileEntryInNextSnapshot.SetFoundInSnapshot(nextSnapshot!.SnapshotName);
            }
        }

        // Delete the snaptshot
        snapshotIndex.Snapshots.Remove(snapshot);
        
        await _snapshotService.SaveIndexAsync(snapshotIndex, token);
        
        _logger.LogInformation("Snapshots saved, starting deleting files...");

        DeleteFilesAndCleanFolders(filesToDelete);
        
        _logger.LogInformation("Backup deletion process finished");
        
        return Result.Success;
    }

    private void DeleteFilesAndCleanFolders(List<string> filesToDelete)
    {
        var foldersToCheck =
            filesToDelete
                .Select(Path.GetDirectoryName)
                .Where(f => f != null)
                .Distinct()
                .Select(f => f!)
                .ToList();
        
        foreach (var fileToDelete in filesToDelete)
        {
            _logger.LogInformation("Deleting file {FilePath}.", fileToDelete);

            try
            {
                _fileWrapper.DeleteFile(fileToDelete); // TODO: call snapshotService!
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete file {FilePath}.", fileToDelete);
            }
        }

        foreach (var folderToCheck in foldersToCheck)
        {
            try
            {
                if (_fileWrapper.DirectoryExists(folderToCheck) && _fileWrapper.DirectoryIsEmpty(folderToCheck))
                {
                    _fileWrapper.DeleteDirectory(folderToCheck);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete folder {FolderPath}.", folderToCheck);
            }
        }
    }
}