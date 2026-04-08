using ErrorOr;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Interfaces.Wrappers;
using FileKeeper.Core.Models;
using FileKeeper.Core.Models.Entities;
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
        
        var snapshot = snapshotResult.Value;
        
        // TODO: get next snapshot
        var nextSnapshot = (Snapshot?)null;

        foreach (var fileEntry in snapshot.Files)
        {
            var fullPath = Path.Combine(fileEntry.SourceDirectory, fileEntry.RelativePath);
            
            // find a file entry in the next snapshot pointing to the current fileEntry
            var fileEntryInNextSnapshot = nextSnapshot?.Files.FirstOrDefault(f => 
                f.SourceDirectory == fileEntry.SourceDirectory
                && f.RelativePath == fileEntry.RelativePath
                && f.Hash == fileEntry.Hash
                && f.FoundInSnapshot == snapshot.SnapshotName);
            
            if (fileEntry.FoundInSnapshot == snapshot.SnapshotName && fileEntryInNextSnapshot is not null)
            {
                fileEntryInNextSnapshot.SetFoundInSnapshot(nextSnapshot!.SnapshotName);
                
                _logger.LogInformation(
                    "File {FilePath} found in the next snapshot {NextSnapshotName}, skipping deletion.",
                    fullPath,
                    nextSnapshot.SnapshotName);
                
                continue; // Do NOT delete the file!
            }

            _logger.LogInformation("Deleting file {FilePath}.", fullPath);
            
            _fileWrapper.DeleteFile(fileEntry.StoredPath);
            // TODO: think what to do with empty folders (check if it is empty here?)
        }
        
        // TODO: delete snapshot
        // TODO: save next snapshot with updated file entries (if it is the case)

        _logger.LogInformation("Backup deletion process finished");
        
        return Result.Success;
    }
}