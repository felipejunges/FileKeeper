using ErrorOr;
using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Models.DMs;
using Microsoft.Extensions.Logging;
using System.Runtime.ExceptionServices;

namespace FileKeeper.Core.UseCases;

public class DeleteBackupUseCase : IDeleteBackupUseCase
{
    private readonly IBackupRepository _backupRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IDatabaseService _databaseService;
    private readonly ILogger<DeleteBackupUseCase> _logger;

    public DeleteBackupUseCase(
        IBackupRepository backupRepository,
        IFileRepository fileRepository,
        IDatabaseService databaseService,
        ILogger<DeleteBackupUseCase> logger)
    {
        _backupRepository = backupRepository;
        _fileRepository = fileRepository;
        _databaseService = databaseService;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> ExecuteAsync(long backupId, CancellationToken token)
    {
        _databaseService.BeginTransaction();

        var result = await ExecuteBackupDeletionProcessAsync(backupId, token);
        if (result.IsError)
        {
            _databaseService.RollbackTransaction();
            return result.Errors;
        }

        _databaseService.CommitTransaction();

        return Result.Success;
    }

    private async Task<ErrorOr<Success>> ExecuteBackupDeletionProcessAsync(long backupId, CancellationToken token)
    {
        _logger.LogInformation("Starting backup deletion process for backupId: {BackupId}.", backupId);

        // 1. Get backup information
        var backupResult = await _backupRepository.GetByIdAsync(backupId, token);
        if (backupResult.IsError)
        {
            _logger.LogWarning(
                "Failed to retrieve backup with id {BackupId}. Backup deletion aborted. Errors: {Errors}",
                backupId,
                backupResult.Errors);

            return backupResult.Errors;
        }

        var backup = backupResult.Value;

        // 2. Get the next backup after the one to be deleted
        var nextBackupResult = await _backupRepository.GetNextBackupByIdAsync(backup.Id, token);
        if (nextBackupResult.IsError && nextBackupResult.FirstError.Type != ErrorType.NotFound)
        {
            _logger.LogWarning(
                "Failed to retrieve the next backup after backupId {BackupId}. Backup deletion aborted. Errors: {Errors}",
                backupId,
                nextBackupResult.Errors);

            return nextBackupResult.Errors;
        }
        
        var nextBackup = nextBackupResult.IsError ? null : nextBackupResult.Value;
        if (nextBackup is not null && backup.CreatedAt > nextBackup.CreatedAt)
        {
            _logger.LogWarning(
                "The next backup with id {NextBackupId} has a creation date earlier than the current backup with id {BackupId}. Backup deletion aborted.",
                nextBackup.Id,
                backupId);
            
            return Error.Validation(description: "The next backup's creation date is earlier than the current backup's creation date, which is unexpected. Backup deletion aborted.");
        }

        // 3. Obtain the list of versions linked to the current backup
        var filesVersionsResult = await _fileRepository.GetFilesToDeleteAsync(backup.Id, nextBackup?.Id, token);
        if (filesVersionsResult.IsError)
        {
            _logger.LogWarning("Failed to retrieve file versions for backupId {BackupId}. Backup deletion aborted. Errors: {Errors}",
                backupId,
                filesVersionsResult.Errors);

            return filesVersionsResult.Errors;
        }

        var filesVersions = filesVersionsResult.Value.ToList();

        // 4. Move versions that do not exist in the next backup to the next backup (if it exists)
        if (nextBackup is not null)
        {
            var moveVersionsResult = await MoveVersionsToNextBackupAsync(backupId, nextBackup.Id, filesVersions, token);
            if (moveVersionsResult.IsError)
                return moveVersionsResult;
        }

        // 5. delete all the versions has is kept in the current backup
        var deleteVersionsResult = await DeleteVersionsKeptInCurrentBackupAsync(backupId, filesVersions, token);
        if (deleteVersionsResult.IsError)
            return deleteVersionsResult;

        // 6. delete all files without versions (if any)
        var filesDeletionResult = await _fileRepository.DeleteFilesWithoutVersionsAsync(token);
        if (filesDeletionResult.IsError)
        {
            _logger.LogWarning(
                "Failed to delete files without versions for backupId {BackupId}. Backup deletion aborted. Errors: {Errors}",
                backupId,
                filesDeletionResult.Errors);
            
            return filesDeletionResult.Errors;
        }

        // 7. delete the backup record
        var backupDeletionResult = await _backupRepository.DeleteAsync(backupId, token);
        if (backupDeletionResult.IsError)
        {
            _logger.LogWarning("Failed to delete backup record for backupId {BackupId}. Backup deletion aborted. Errors: {Errors}",
                backupId,
                backupDeletionResult.Errors);
            
            return backupDeletionResult.Errors;
        }

        _logger.LogInformation("Backup deletion process completed successfully for backupId: {BackupId}.", backupId);

        return Result.Success;
    }

    private async Task<ErrorOr<Success>> DeleteVersionsKeptInCurrentBackupAsync(long backupId, List<FileToDeleteDM> filesVersions,
        CancellationToken token)
    {
        _logger.LogDebug("Deleting all versions left in backupId {BackupId}", backupId);

        var versionDeletionsResult = await _fileRepository.DeleteAllVersionsInBackupAsync(backupId, token);
        if (versionDeletionsResult.IsError)
        {
            _logger.LogWarning("Failed to delete file versions for backupId {BackupId}. Backup deletion aborted. Errors: {Errors}",
                backupId,
                versionDeletionsResult.Errors);

            return versionDeletionsResult.Errors;
        }

        return Result.Success;
    }

    private async Task<ErrorOr<Success>> MoveVersionsToNextBackupAsync(long backupId, long nextBackupId, List<FileToDeleteDM> filesVersions,
        CancellationToken token)
    {
        var idsVersionsToMoveToNextBackup = filesVersions
            .Where(f => !f.ExistsInNextBackup)
            .Select(f => f.Id)
            .ToList();

        _logger.LogDebug(
            "Moving the following file versions to the next backup with id {NextBackupId} for backupId {BackupId}: {IdsVersionsToMoveToNextBackup}",
            nextBackupId, backupId, idsVersionsToMoveToNextBackup);

        var versionsMovedResult = await _fileRepository.MoveVersionsToBackupAsync(idsVersionsToMoveToNextBackup, nextBackupId, token);
        if (versionsMovedResult.IsError)
        {
            _logger.LogWarning("Failed to delete file versions for backupId {BackupId}. Backup deletion aborted. Errors: {Errors}", backupId,
                versionsMovedResult.Errors);

            return versionsMovedResult.Errors;
        }
        
        var increementResult = await IncrementNewFilesCountInNextBackupAsync(nextBackupId, idsVersionsToMoveToNextBackup, token);
        if (increementResult.IsError)
        {
            _logger.LogWarning(
                "Failed to increment new files count in the next backup with id {NextBackupId}. Backup deletion aborted. Errors: {Errors}",
                nextBackupId,
                increementResult.Errors);
            
            return increementResult;
        }

        return Result.Success;
    }

    private async Task<ErrorOr<Success>> IncrementNewFilesCountInNextBackupAsync(long backupId, IEnumerable<long> idsVersionsToMoveToNextBackup, CancellationToken token)
    {
        var incrementResult = await _backupRepository.IncrementMovedFilesDataToBackupAsync(backupId, idsVersionsToMoveToNextBackup, token);
        if (incrementResult.IsError)
        {
            _logger.LogWarning(
                "Failed to increment new files count in the next backup with id {BackupId}. Backup deletion aborted. Errors: {Errors}",
                backupId,
                incrementResult.Errors);

            return incrementResult.Errors;
        }

        return Result.Success;
    }
}