using ErrorOr;
using FileKeeper.Core.Extensions;
using FileKeeper.Core.Helpers;
using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Models.DMs;
using FileKeeper.Core.Models.Entities;

namespace FileKeeper.Core.UseCases;

public class CriarBackupUseCase : ICriarBackupUseCase
{
    private readonly IFileSystem _fileSystem;
    private readonly IFileRepository _fileRepository;
    private readonly IBackupRepository _backupRepository;
    private readonly IDatabaseService _databaseService;
    private readonly IConfigurationService _configurationService;

    public CriarBackupUseCase(
        IFileSystem fileSystem,
        IFileRepository fileRepository,
        IBackupRepository backupRepository,
        IDatabaseService databaseService,
        IConfigurationService configurationService)
    {
        _fileSystem = fileSystem;
        _fileRepository = fileRepository;
        _backupRepository = backupRepository;
        _databaseService = databaseService;
        _configurationService = configurationService;
    }

    public async Task<ErrorOr<Backup>> ExecuteAsync(CancellationToken token)
    {
        var configuration = await _configurationService.GetConfigurationAsync(token);
        if (!configuration.MonitoredFolders.Any())
        {
            return Error.Failure(description: "At least one monitored folder must be configured to create a backup.");
        }
        
        _databaseService.BeginTransaction();

        var newBackup = Backup.CreateNew();
        var backupResult = await _backupRepository.InsertAsync(newBackup, token);

        if (backupResult.IsError)
        {
            _databaseService.RollbackTransaction();
            return backupResult.Errors;
        }
        
        foreach(var folder in configuration.MonitoredFolders)
        {
            var folderBackup = await ExecuteBackupFromFolderAsync(folder, newBackup, token);

            if (folderBackup.IsError)
            {
                _databaseService.RollbackTransaction();
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

            if (storedFile is null)
            {
                var result = await AddNewFileToStorageAsync(backupPath, relativePath, fileName, localFileHash, newBackup.Id, localFileStream, token);

                if (result.IsError)
                    return result.Errors;

                newBackup.IncrementCreatedFiles();
            }
            else if (storedFile.CurrentHash != localFileHash)
            {
                var result = await AddNewVersionToFileInStorageAsync(storedFile.Id, localFileHash, newBackup.Id, localFileStream, token);

                if (result.IsError)
                    return result.Errors;

                newBackup.IncrementUpdatedFiles();
            }

            relativePathsProcessados.Add((relativePath, fileName));
        }

        await ValidarArquivosExcluidosAsync(storedFiles, relativePathsProcessados, newBackup, token);

        await _backupRepository.UpdateAsync(newBackup, token);

        return Result.Success;
    }

    private async Task ValidarArquivosExcluidosAsync(List<FileVersionDM> storedFiles, List<(string RelativePath, string FileName)> relativePathsProcessados, Backup newBackup, CancellationToken token)
    {
        var idsArquivosExcluir = storedFiles
            .Where(f => !relativePathsProcessados.Contains((f.RelativePath, f.FileName)))
            .Select(f => f.Id)
            .ToList();

        if (idsArquivosExcluir.Any())
        {
            var deletionResult = await _fileRepository.MarkAsDeletedAsync(idsArquivosExcluir, newBackup.Id, token);
            var deletedFiels = deletionResult.IsError ? 0 : deletionResult.Value;

            newBackup.IncrementDeletedFiles(deletedFiels);
        }
    }

    private async Task<ErrorOr<long>> AddNewFileToStorageAsync(string backupPath, string relativePath, string fileName, string fileHash, long backupId, FileStream fileStream, CancellationToken token)
    {
        var file = FileModel.CreateNew(backupPath, relativePath, fileName);

        var result = await _fileRepository.InsertAsync(file, token);

        if (result.IsError)
            return result.Errors;

        return await AddNewVersionToFileInStorageAsync(file.Id, fileHash, backupId, fileStream, token);
    }

    private async Task<ErrorOr<long>> AddNewVersionToFileInStorageAsync(long fileId, string fileHash, long backupId, FileStream fileStream, CancellationToken token)
    {
        var fileVersion = FileVersion.CreateNew(
            fileId,
            backupId,
            fileStream.Length,
            fileHash,
            await fileStream.ReadAllBytesAsync(token));

        return await _fileRepository.InsertVersionAsync(fileVersion, token);
    }
}