using ErrorOr;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UseCases;
using Microsoft.Extensions.Logging;

namespace FileKeeper.Core.UseCases;

public class RestoreBackupUseCase : IRestoreBackupUseCase
{
    private readonly IFileRepository _fileRepository;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<RestoreBackupUseCase> _logger;

    public RestoreBackupUseCase(
        IFileRepository fileRepository,
        IFileSystem fileSystem, ILogger<RestoreBackupUseCase> logger)
    {
        _fileRepository = fileRepository;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> ExecuteAsync(long backupId, string destinationFolder, CancellationToken token)
    {
        _logger.LogInformation(
            "Initiating backup restoration process for backup ID {BackupId} to destination folder '{DestinationFolder}'.",
            backupId,
            destinationFolder);
        
        // se a pasta nao existe, criar
        var directoryValidation = ValidateDestinationDirectory(destinationFolder);
        if (directoryValidation.IsError)
            return directoryValidation.Errors;

        // recupera o backup do banco de dados
        // TODO: do not load all files into memory at once, but rather stream them one by one
        var filesToRecover = await _fileRepository.GetFilesToRecoverAsync(backupId, token);
        if (filesToRecover.IsError)
            return filesToRecover.Errors;
        
        _logger.LogInformation(
            "Starting backup restoration for backup ID {BackupId} to destination folder '{DestinationFolder}' with {FileCount} files to recover.",
            backupId,
            destinationFolder,
            filesToRecover.Value.Count());

        foreach (var fileToRecover in filesToRecover.Value)
        {
            var finalDestinationName = Path.Combine(
                destinationFolder,
                fileToRecover.BackupPath.TrimStart(Path.DirectorySeparatorChar),
                fileToRecover.RelativePath.TrimStart(Path.DirectorySeparatorChar),
                fileToRecover.FileName);

            try
            {
                var directory = Path.GetDirectoryName(finalDestinationName)!;
                if (!_fileSystem.DirectoryExists(directory))
                    _fileSystem.CreateDirectory(directory);
                
                _logger.LogInformation("Restoring file '{FileName}' to '{FinalDestinationName}'.", fileToRecover.FileName, finalDestinationName);
                
                await using var fileStream = new FileStream(finalDestinationName, FileMode.Create, FileAccess.Write);
                await fileStream.WriteAsync(fileToRecover.DecompressedContent, token);
            }
            catch (Exception e)
            {
                _logger.LogError(
                    e, 
                    "An error occurred while restoring file '{FileName}' to '{FinalDestinationName}'.",
                    fileToRecover.FileName,
                    finalDestinationName);
                
                return Error.Unexpected(description: $"Ocorreu um erro ao restaurar o arquivo '{fileToRecover.FileName}': {e.Message}");
            }
        }
        
        _logger.LogInformation(
            "Backup restoration process completed successfully for backup ID {BackupId} to destination folder '{DestinationFolder}'.",
            backupId,
            destinationFolder);

        return Result.Success;
    }

    private ErrorOr<Success> ValidateDestinationDirectory(string destinationFolder)
    {
        if (!_fileSystem.DirectoryExists(destinationFolder))
        {
            _fileSystem.CreateDirectory(destinationFolder);
            _logger.LogInformation("Creating new directory for backup restoration: {DestinationFolder}", destinationFolder);
            return Result.Success;
        }

        if (!_fileSystem.IsDirectoryEmpty(destinationFolder))
        {
            _logger.LogWarning("The destination directory '{DestinationFolder}' is not empty. Backup restoration requires an empty directory.", destinationFolder);
            return Error.Failure(description: "O diretório de destino deve estar vazio para restaurar o backup.");
        }

        return Result.Success;
    }
}