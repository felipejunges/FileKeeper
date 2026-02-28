using ErrorOr;
using FileKeeper.Core.Extensions;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Persistence.Repositories;

namespace FileKeeper.Core.UseCases;

public class CriarBackupUseCase : ICriarBackupUseCase
{
    private readonly IFileSystem _fileSystem;
    private readonly IFileRepository _fileRepository;
    
    public CriarBackupUseCase(
        IFileSystem fileSystem,
        IFileRepository fileRepository)
    {
        _fileSystem = fileSystem;
        _fileRepository = fileRepository;
    }

    public async Task<ErrorOr<Success>> ExecuteAsync(CancellationToken token)
    {
        var TODODIR = "/home/felipe/Dropbox/Imagens-Perfil"; // TODO: TODO!

        var newVersionNumber = long.Parse(DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
        
        var localFiles = _fileSystem.GetFiles(TODODIR, "*.*", SearchOption.AllDirectories);
        var storedFilesResult = await _fileRepository.GetFilesWithVersionAsync(TODODIR, token);
        
        // TODO: POBREMA: pra funcionar o GET por Path acima, talvez tenhamos que salvar o Path como sendo o Path do backup, todos o mesmo
        // e guardar um RELATIVE, que é o path dentro do diretório de backup. aí funciona
        // exemplo:
        // HOJE está:
        //public required string Path { get; init; }
        // public required string Name { get; init; }
        // FICARIA:
        //public required string BackupPath { get; init; } // o path do backup, ex: "/home/felipe/Dropbox/Imagens-Perfil"
        // public required string RelativePath { get; init; } // o path relativo dentro do backup, ex: "2024/Junho/foto.jpg"

        if (storedFilesResult.IsError)
            return storedFilesResult.Errors;

        var storedFiles = storedFilesResult.Value.ToList();
        
        // TODO: begin transaction

        foreach (var localFile in localFiles)
        {
            var fileName = Path.GetFileName(localFile);
            var storedFile = storedFiles.FirstOrDefault(f => f.Name == fileName);

            await using var localFileStream = _fileSystem.GetReadFileStream(localFile);
            var localFileHash = await HasingHelpers.ComputeHashFromStreamAsync(localFileStream, token);
            
            if (storedFile is null)
            {
                var result = await AddNewFileToStorageAsync(localFile, localFileHash, newVersionNumber, localFileStream, token);

                if (result.IsError)
                {
                    // TODO: rollback
                    return result;
                }
            }
            else if (storedFile.CurrentHash != localFileHash)
            {
                var result = await AddNewVersionToFileInStorageAsync(storedFile.Id, localFileHash, newVersionNumber, localFileStream, token);
                
                if (result.IsError)
                {
                    // TODO: rollback
                    return result;
                }
            }
        }

        var nomesDosArquivosLocais = localFiles.Select(Path.GetFileName).ToHashSet();
        var idsArquivosExcluir = storedFiles.Where(f => !nomesDosArquivosLocais.Contains(f.Name)).Select(f => f.Id).ToList();

        await _fileRepository.MarkAsDeletedAsync(idsArquivosExcluir, newVersionNumber, token);
        
        // TODO: commit transaction

        return Result.Success;
    }

    private async Task<ErrorOr<Success>> AddNewFileToStorageAsync(string fullName, string fileHash, long newVersionNumber, FileStream fileStream, CancellationToken token)
    {
        var path = Path.GetDirectoryName(fullName) ?? string.Empty;
        var name = Path.GetFileName(fullName);
        
        var file = new FileModel()
        {
            Id = Guid.CreateVersion7().ToString(),
            Path = path,
            Name = name
        };
        
        var result = await _fileRepository.InsertAsync(file, token);

        if (result.IsError)
            return result;

        return await AddNewVersionToFileInStorageAsync(file.Id, fileHash, newVersionNumber, fileStream, token);
    }
    
    private async Task<ErrorOr<Success>> AddNewVersionToFileInStorageAsync(string fileId, string fileHash, long newVersionNumber, FileStream fileStream, CancellationToken token)
    {
        var fileVersion = new FileVersion()
        {
            Id = Guid.CreateVersion7().ToString(),
            FileId = fileId,
            Hash = fileHash,
            Size = fileStream.Length,
            Content = await fileStream.ReadAllBytesAsync(token),
            VersionNumber = newVersionNumber,
            CreatedAt = DateTime.UtcNow
        };
        
        return await _fileRepository.InsertVersionAsync(fileVersion, token);
    }
}