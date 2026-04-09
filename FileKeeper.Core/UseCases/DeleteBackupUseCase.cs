using ErrorOr;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Interfaces.Wrappers;
using FileKeeper.Core.Models;
using Microsoft.Extensions.Logging;

namespace FileKeeper.Core.UseCases;

public class DeleteBackupUseCase : IDeleteBackupUseCase
{
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly IFileWrapper _fileWrapper;
    private readonly ILogger<DeleteBackupUseCase> _logger;

    public DeleteBackupUseCase(
        ISnapshotRepository snapshotRepository,
        IFileWrapper fileWrapper,
        ILogger<DeleteBackupUseCase> logger)
    {
        _snapshotRepository = snapshotRepository;
        _fileWrapper = fileWrapper;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> ExecuteAsync(Guid snapshotId, IProgress<BackupProgress>? progress, CancellationToken token)
    {
        _logger.LogInformation("Starting backup deletion process.");
        
        var snapshotResult = await _snapshotRepository.GetSnapshotAsync(snapshotId, token);
        if (snapshotResult.IsError)
            return snapshotResult.Errors;
        
        var nextSnapshotResult = await _snapshotRepository.GetNextSnapshotAsync(snapshotId, token);
        if (nextSnapshotResult.IsError && nextSnapshotResult.FirstError.Type != ErrorType.NotFound)
            return nextSnapshotResult.Errors;
        
        var snapshot = snapshotResult.Value;
        var nextSnapshot = nextSnapshotResult.IsError ? null : nextSnapshotResult.Value;

        foreach (var fileEntry in snapshot.Files)
        {
            var fullPath = Path.Combine(fileEntry.SourceDirectory, fileEntry.RelativePath);
            
            // find a file entry in the next snapshot pointing to the current fileEntry
            var fileEntryInNextSnapshot = nextSnapshot?.Files.FirstOrDefault(f => 
                f.SourceDirectory == fileEntry.SourceDirectory
                && f.RelativePath == fileEntry.RelativePath); // TODO: create compare in model

            bool deleteFile = false;
            
            if (fileEntryInNextSnapshot is null)
            {
                // file does not exists in the next snapshot, so delete it
                deleteFile = true;
            }
            else if (fileEntryInNextSnapshot.Hash != fileEntry.Hash && fileEntryInNextSnapshot.FoundInSnapshot == snapshot.SnapshotName)
            {
                // file exists in the next snapshot, but has a different hash or do not point to FileEntry: delete it
                deleteFile = true;
            }
            else
            {
                // file exists in the next snapshot, has the same hash and point to the current Snapshot: keep it!
                fileEntryInNextSnapshot.SetFoundInSnapshot(nextSnapshot!.SnapshotName);
            }

            if (deleteFile)
            {
                _logger.LogInformation("Deleting file {FilePath}.", fullPath);

                _fileWrapper.DeleteFile(fileEntry.StoredPath);
                // TODO: think what to do with empty folders (check if it is empty here?)
            }
            else
            {
                _logger.LogInformation(
                    "File {FilePath} found in the next snapshot {NextSnapshotName}, skipping deletion.",
                    fullPath,
                    nextSnapshot.SnapshotName);
            }
        }

        // delete the current snapshot
        await _snapshotRepository.DeleteSnapshotAsync(snapshot.Id, token);

        // and update the next (if exists)
        if (nextSnapshot is not null)
            await _snapshotRepository.UpdateSnapshot(nextSnapshot, token);
        
        _logger.LogInformation("Backup deletion process finished");
        
        return Result.Success;
    }
}