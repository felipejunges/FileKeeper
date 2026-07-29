using ErrorOr;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Models;
using FileKeeper.Core.Models.DTOs;
using Microsoft.Extensions.Logging;

namespace FileKeeper.Core.UseCases;

public class DeleteBackupUseCase : IDeleteBackupUseCase
{
    private readonly ISnapshotService _snapshotService;
    private readonly ILogger<DeleteBackupUseCase> _logger;

    public DeleteBackupUseCase(
        ISnapshotService snapshotService,
        ILogger<DeleteBackupUseCase> logger)
    {
        _snapshotService = snapshotService;
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
            return Error.NotFound(description: $"Snapshot {snapshotId} doesn't exist.");
        }

        var snapshotIx = snapshotIndex.Snapshots.IndexOf(snapshot) + 1;
        var nextSnapshot = snapshotIx < snapshotIndex.Snapshots.Count ? snapshotIndex.Snapshots[snapshotIx] : null;

        var filesToDelete = new List<FileToDelete>();
        var filesToCheck = snapshot.Files.Where(f => f.FoundInSnapshot == snapshot.SnapshotName).ToList();

        foreach (var fileEntry in filesToCheck)
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

                filesToDelete.Add(new FileToDelete(fileEntry.StoredPath));
            }
            else if (fileEntryInNextSnapshot.Hash != fileEntry.Hash || fileEntryInNextSnapshot.FoundInSnapshot != snapshot.SnapshotName)
            {
                // file exists in the next snapshot, but has a different hash or do not point to current FileEntry: delete it
                _logger.LogInformation("File {FilePath}: deleting, exists in next snapshot but different", fullPath);

                filesToDelete.Add(new FileToDelete(fileEntry.StoredPath));
            }
            else
            {
                // file exists in the next snapshot, has the same hash and point to the current Snapshot: keep it!
                _logger.LogInformation("File {FilePath}: skipping deletion, exists in next snapshot", fullPath);

                // Unchanged files are carried forward without ever being "re-stamped", so more
                // than just the immediate next snapshot may still say FoundInSnapshot == snapshot.SnapshotName.
                // Promote all of them to the new owner, not just the immediate next one.
                for (var laterIx = snapshotIx; laterIx < snapshotIndex.Snapshots.Count; laterIx++)
                {
                    var laterSnapshot = snapshotIndex.Snapshots[laterIx];

                    var matchingEntries = laterSnapshot.Files.Where(f =>
                        f.SourceDirectory == fileEntry.SourceDirectory
                        && f.RelativePath == fileEntry.RelativePath
                        && f.FoundInSnapshot == snapshot.SnapshotName);

                    foreach (var matchingEntry in matchingEntries)
                        matchingEntry.SetFoundInSnapshot(nextSnapshot!.SnapshotName);
                }
            }
        }

        _logger.LogInformation("Starting deleting files...");

        // delete the files from the zip
        var deleteResult = await DeleteFilesAsync(filesToDelete, token);
        if (deleteResult.IsError)
            return deleteResult.Errors;

        await _snapshotService.RemoveEmptyDirectoriesAsync(token);
        
        snapshotIndex.Snapshots.Remove(snapshot);

        await _snapshotService.SaveIndexAsync(snapshotIndex, token);

        _logger.LogInformation("Backup deletion process finished");

        return Result.Success;
    }

    private async Task<ErrorOr<Success>> DeleteFilesAsync(List<FileToDelete> filesToDelete, CancellationToken token)
    {
        if (!filesToDelete.Any())
            return Result.Success;
        
        filesToDelete.ForEach(f => _logger.LogInformation("Batch deleting file {File}", f.StoredPath));
        
        return await _snapshotService.DeleteFilesAsync(filesToDelete, token);
    }
}