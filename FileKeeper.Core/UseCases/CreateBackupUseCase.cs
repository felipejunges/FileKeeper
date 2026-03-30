using ErrorOr;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Interfaces.Wrappers;
using FileKeeper.Core.Models;
using FileKeeper.Core.Models.DTOs;
using FileKeeper.Core.Models.Entities;

namespace FileKeeper.Core.UseCases;

public class CreateBackupUseCase : ICreateBackupUseCase
{
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly IFileWrapper _fileWrapper;
    private readonly ICompressedEncryptedFileWriter _compressedEncryptedFileWriter;
    private readonly IConfigurationService _configurationService;

    public CreateBackupUseCase(
        ISnapshotRepository snapshotRepository,
        IFileWrapper fileWrapper,
        ICompressedEncryptedFileWriter compressedEncryptedFileWriter, IConfigurationService configurationService)
    {
        _snapshotRepository = snapshotRepository;
        _fileWrapper = fileWrapper;
        _compressedEncryptedFileWriter = compressedEncryptedFileWriter;
        _configurationService = configurationService;
    }

    public async Task<ErrorOr<Snapshot>> ExecuteAsync(IProgress<BackupProgress>? progress, CancellationToken token)
    {
        var configuration = await _configurationService.GetConfigurationAsync(token);

        var lastSnapshotResult = await _snapshotRepository.GetLastSnapshotAsync(token);
        if (lastSnapshotResult.IsError && lastSnapshotResult.FirstError.Type != ErrorType.NotFound)
        {
            return lastSnapshotResult.Errors;
        }

        var lastSnapshot = lastSnapshotResult.IsError ? null : lastSnapshotResult.Value;

        var newSnapshot = Snapshot.Create();

        foreach (var sourceDirectory in configuration.SourceDirectories)
        {
            if (token.IsCancellationRequested) break;

            var filesOnDisk = _fileWrapper.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);

            var filesToSave = new List<FileToSave>();
            var filesToMantain = new List<FileToSave>();

            // TODO: think about Parallel

            foreach (var fileOnDisk in filesOnDisk)
            {
                if (token.IsCancellationRequested) break;

                var fileToSave = await CreateFileToSaveAsync(fileOnDisk, sourceDirectory, token);

                var existingFile = lastSnapshot?.Files.FirstOrDefault(f => f.RelativePath == fileToSave.RelativePath);

                if (existingFile == null)
                {
                    // CASO: O arquivo é novo: então precisamos adicionar ao backup
                    fileToSave.FoundInSnapshot = newSnapshot.SnapshotName;
                    filesToSave.Add(fileToSave);
                }
                else if (existingFile.Hash != fileToSave.Hash)
                {
                    // CASO: O arquivo já existe, mas o hash é diferente: então precisamos atualizar o arquivo do backup
                    fileToSave.FoundInSnapshot = newSnapshot.SnapshotName;
                    filesToSave.Add(fileToSave);
                }
                else
                {
                    // CASO: O arquivo já existe, e o hash é o mesmo: então podemos reaproveitar o arquivo do backup anterior
                    fileToSave.FoundInSnapshot = existingFile.FoundInSnapshot;
                    filesToMantain.Add(fileToSave);
                }
            }

            foreach (var fileToSave in filesToSave)
            {
                if (token.IsCancellationRequested) break;

                var compressResult =
                    await _compressedEncryptedFileWriter.CompressFromStreamToFileAsync(fileToSave.FullPath, fileToSave.StoredPath, token);

                if (compressResult.IsError)
                    return compressResult.Errors;

                newSnapshot.AddFile(
                    FileEntry.Create(
                        fileToSave.RelativePath,
                        fileToSave.StoredPath,
                        fileToSave.Hash,
                        fileToSave.Size,
                        fileToSave.LastModified,
                        fileToSave.FoundInSnapshot));
            }

            foreach (var fileToMantain in filesToMantain)
            {
                newSnapshot.AddFile(
                    FileEntry.Create(
                        fileToMantain.RelativePath,
                        fileToMantain.StoredPath,
                        fileToMantain.Hash,
                        fileToMantain.Size,
                        fileToMantain.LastModified,
                        fileToMantain.FoundInSnapshot));
            }
        }

        if (token.IsCancellationRequested) return Error.Unexpected(description: "Operation cancelled");

        var addSnapshotResult = await _snapshotRepository.AddSnapshotAsync(newSnapshot, token);
        if (addSnapshotResult.IsError)
            return addSnapshotResult.Errors;

        return newSnapshot;
    }

    private async Task<FileToSave> CreateFileToSaveAsync(string fileOnDisk, string sourceDirectory, CancellationToken token)
    {
        var relativePath = Path.GetRelativePath(sourceDirectory, fileOnDisk);

        var guid = Guid.NewGuid().ToString("N");
        var storedPath = $"{guid[..8]}/{guid}";

        var fileInfo = await _fileWrapper.GetFileMetadataAsync(fileOnDisk, token);

        return new FileToSave()
        {
            FullPath = fileOnDisk,
            StoredPath = storedPath,
            RelativePath = relativePath,
            Hash = fileInfo.Hash,
            Size = fileInfo.Size,
            LastModified = fileInfo.LastModified
        };
    }
}