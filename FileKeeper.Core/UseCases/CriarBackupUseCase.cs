using ErrorOr;
using FileKeeper.Core.Extensions;
using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Persistence.Repositories;
using File = FileKeeper.Core.Models.Entities.File;

namespace FileKeeper.Core.UseCases;

public class CriarBackupUseCase : ICriarBackupUseCase
{
    private readonly IFileSystem _fileSystem;
    private readonly IFileRepository _fileRepository;
    private readonly IBackupRepository _backupRepository;
    private readonly IDatabaseService _databaseService;

    public CriarBackupUseCase(
        IFileSystem fileSystem,
        IFileRepository fileRepository,
        IBackupRepository backupRepository,
        IDatabaseService databaseService)
    {
        _fileSystem = fileSystem;
        _fileRepository = fileRepository;
        _backupRepository = backupRepository;
        _databaseService = databaseService;
    }

    public async Task<ErrorOr<Backup>> ExecuteAsync(CancellationToken token)
    {
        var todoDir = "/home/felipe/Dropbox/Imagens-Perfil"; // TODO: TODO!

        var localFiles = _fileSystem.GetFiles(todoDir, "*.*", SearchOption.AllDirectories);
        var storedFilesResult = await _fileRepository.GetFilesWithVersionAsync(todoDir, token);

        if (storedFilesResult.IsError)
            return storedFilesResult.Errors;

        var storedFiles = storedFilesResult.Value.ToList();

        // begin transaction
        _databaseService.BeginTransaction();

        var newBackup = Backup.CreateNew();
        var backupResult = await _backupRepository.InsertAsync(newBackup, token);

        if (backupResult.IsError)
            return backupResult.Errors;

        var relativePathsProcessados = new List<string>();

        foreach (var localFile in localFiles)
        {
            var relativePath = Path.GetRelativePath(todoDir, localFile);
            var storedFile = storedFiles.FirstOrDefault(f => f.RelativePath == relativePath);

            await using var localFileStream = _fileSystem.GetReadFileStream(localFile);
            var localFileHash = await HasingHelpers.ComputeHashFromStreamAsync(localFileStream, token);

            if (storedFile is null)
            {
                var result = await AddNewFileToStorageAsync(todoDir, relativePath, localFileHash, newBackup.Id, localFileStream, token);

                if (result.IsError)
                {
                    _databaseService.RollbackTransaction();
                    return result.Errors;
                }

                newBackup.IncrementCreatedFiles();
            }
            else if (storedFile.CurrentHash != localFileHash)
            {
                var result = await AddNewVersionToFileInStorageAsync(storedFile.Id, localFileHash, newBackup.Id, localFileStream, token);

                if (result.IsError)
                {
                    _databaseService.RollbackTransaction();
                    return result.Errors;
                }

                newBackup.IncrementUpdatedFiles();
            }

            relativePathsProcessados.Add(relativePath);
        }

        var idsArquivosExcluir = storedFiles
            .Where(f => !relativePathsProcessados.Contains(f.RelativePath))
            .Select(f => f.Id)
            .ToList();

        var deletionResult = await _fileRepository.MarkAsDeletedAsync(idsArquivosExcluir, newBackup.Id, token);
        var deletedFiels = deletionResult.IsError ? 0 : deletionResult.Value;

        newBackup.IncrementDeletedFiles(deletedFiels);
        await _backupRepository.UpdateAsync(newBackup, token);

        _databaseService.CommitTransaction();

        return newBackup;
    }

    private async Task<ErrorOr<long>> AddNewFileToStorageAsync(string backupPath, string relativePath, string fileHash, long backupId, FileStream fileStream, CancellationToken token)
    {
        var file = File.CreateNew(backupPath, relativePath);

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