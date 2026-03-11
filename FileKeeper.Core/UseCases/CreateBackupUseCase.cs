using ErrorOr;
using FileKeeper.Core.Extensions;
using FileKeeper.Core.Helpers;
using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Models.DMs;
using FileKeeper.Core.Models.Entities;
using Microsoft.Extensions.Logging;

namespace FileKeeper.Core.UseCases;

public class CreateBackupUseCase : ICreateBackupUseCase
{
    private readonly IFileSystem _fileSystem;
    private readonly IFileRepository _fileRepository;
    private readonly IBackupRepository _backupRepository;
    private readonly IDatabaseService _databaseService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<CreateBackupUseCase> _logger;

    public CreateBackupUseCase(
        IFileSystem fileSystem,
        IFileRepository fileRepository,
        IBackupRepository backupRepository,
        IDatabaseService databaseService,
        IConfigurationService configurationService,
        ILogger<CreateBackupUseCase> logger)
    {
        _fileSystem = fileSystem;
        _fileRepository = fileRepository;
        _backupRepository = backupRepository;
        _databaseService = databaseService;
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task<ErrorOr<Backup>> ExecuteAsync(CancellationToken token)
    {
        _logger.LogInformation("Starting backup creation process.");
        
        var configuration = await _configurationService.GetConfigurationAsync(token);
        if (!configuration.MonitoredFolders.Any())
        {
            _logger.LogWarning("No monitored folders configured. Backup creation aborted.");
            return Error.Failure(description: "At least one monitored folder must be configured to create a backup.");
        }
        
        _databaseService.BeginTransaction();

        var newBackup = Backup.CreateNew();
        var backupResult = await _backupRepository.InsertAsync(newBackup, token);

        if (backupResult.IsError)
        {
            _databaseService.RollbackTransaction();
            _logger.LogWarning("Failed to create backup record in the database. Backup creation aborted. Errors: {Errors}", backupResult.Errors);
            
            return backupResult.Errors;
        }
        
        _logger.LogInformation("Backup record created with ID {BackupId}. Starting file processing.", newBackup.Id);
        
        foreach(var folder in configuration.MonitoredFolders)
        {
            _logger.LogInformation("Processing folder '{Folder}' for backup ID {BackupId}.", folder, newBackup.Id);
            
            var folderBackup = await ExecuteBackupFromFolderAsync(folder, newBackup, token);

            if (folderBackup.IsError)
            {
                _databaseService.RollbackTransaction();
                _logger.LogWarning(
                    "Failed to process folder '{Folder}' for backup ID {BackupId}. Backup creation aborted. Errors: {Errors}",
                    folder,
                    newBackup.Id,
                    folderBackup.Errors);
                
                return folderBackup.Errors;
            }
        }
        
        _databaseService.CommitTransaction();

        return newBackup;
    }

    private async Task<ErrorOr<Success>> ExecuteBackupFromFolderAsync(string backupPath, Backup newBackup, CancellationToken token)
    {
        var localFiles = _fileSystem.GetFiles(backupPath, "*.*", SearchOption.AllDirectories);
        var storedFilesResult = await _fileRepository.GetFilesWithVersionAsync(backupPath, token);

        if (storedFilesResult.IsError)
            return storedFilesResult.Errors;

        var storedFiles = storedFilesResult.Value.ToList();

        var relativePathsProcessados = new List<(string RelativePath, string FileName)>();

        foreach (var localFile in localFiles)
        {
            var pathOnly = Path.GetDirectoryName(localFile) ?? string.Empty;
            var fileName = Path.GetFileName(localFile);
            var relativePath = Path.GetRelativePath(backupPath, pathOnly);
            
            var storedFile = storedFiles.FirstOrDefault(f => f.RelativePath == relativePath && f.FileName == fileName);

            await using var localFileStream = _fileSystem.GetReadFileStream(localFile);
            var localFileHash = await HasingHelpers.ComputeHashFromStreamAsync(localFileStream, token);
            
            var fileAction = ObtainFileAction(storedFile, localFileHash);
            
            _logger.LogDebug(
                "File '{FileName}' in folder '{Folder}' determined to be '{FileAction}' for backup ID {BackupId}.",
                fileName,
                backupPath,
                fileAction,
                newBackup.Id);

            if (fileAction == FileAction.Create)
            {
                var result = await AddNewFileToStorageAsync(backupPath, relativePath, fileName, localFileHash, newBackup, localFileStream, token);

                if (result.IsError)
                    return result.Errors;

                newBackup.IncrementCreatedFiles();
            }
            else if (fileAction == FileAction.Update)
            {
                var result = await AddNewVersionToFileInStorageAsync(storedFile!.Id, localFileHash, newBackup, false, localFileStream, token);

                if (result.IsError)
                    return result.Errors;

                newBackup.IncrementUpdatedFiles();
            }

            relativePathsProcessados.Add((relativePath, fileName));
        }

        await ValidateDeletedFilesAsync(storedFiles, relativePathsProcessados, newBackup, token);

        await _backupRepository.UpdateAsync(newBackup, token);
        
        _logger.LogInformation(
            "Completed processing folder '{Folder}' for backup ID {BackupId}. Created: {CreatedFiles}, Updated: {UpdatedFiles}, Deleted: {DeletedFiles}.",
            backupPath,
            newBackup.Id,
            newBackup.CreatedFiles,
            newBackup.UpdatedFiles,
            newBackup.DeletedFiles);

        return Result.Success;
    }

    private FileAction ObtainFileAction(FileVersionDM? storedFile, string localFileHash)
    {
        return storedFile is null
            ? FileAction.Create
            : storedFile.CurrentHash != localFileHash
                ? FileAction.Update
                : FileAction.Ignore;
    }

    private async Task ValidateDeletedFilesAsync(List<FileVersionDM> storedFiles, List<(string RelativePath, string FileName)> relativePathsProcessados, Backup newBackup, CancellationToken token)
    {
        var idsArquivosExcluir = storedFiles
            .Where(f => !relativePathsProcessados.Contains((f.RelativePath, f.FileName)))
            .Select(f => f.Id)
            .ToList();

        if (idsArquivosExcluir.Any())
        {
            _logger.LogInformation("Marking {FileCount} files as deleted for backup ID {BackupId}.", idsArquivosExcluir.Count, newBackup.Id);
            
            var deletionResult = await _fileRepository.MarkAsDeletedAsync(idsArquivosExcluir, newBackup.Id, token);
            var deletedFiels = deletionResult.IsError ? 0 : deletionResult.Value;

            newBackup.IncrementDeletedFiles(deletedFiels);
        }
    }

    private async Task<ErrorOr<long>> AddNewFileToStorageAsync(string backupPath, string relativePath, string fileName, string fileHash, Backup backup, FileStream fileStream, CancellationToken token)
    {
        var file = FileModel.CreateNew(backupPath, relativePath, fileName);

        var result = await _fileRepository.InsertAsync(file, token);

        if (result.IsError)
            return result.Errors;

        return await AddNewVersionToFileInStorageAsync(file.Id, fileHash, backup, true, fileStream, token);
    }

    private async Task<ErrorOr<long>> AddNewVersionToFileInStorageAsync(long fileId, string fileHash, Backup backup, bool isNew, FileStream fileStream, CancellationToken token)
    {
        var fileVersion = FileVersion.CreateNew(
            fileId,
            backup.Id,
            isNew,
            fileStream.Length,
            fileHash,
            await fileStream.ReadAllBytesAsync(token));
        
        backup.IncrementTotalSize(fileVersion.Size);

        return await _fileRepository.InsertVersionAsync(fileVersion, token);
    }

    private enum FileAction
    {
        Create,
        Update,
        Ignore
    }
}