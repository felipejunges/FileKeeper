using ErrorOr;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Interfaces.Wrappers;
using FileKeeper.Core.Models;
using FileKeeper.Core.Models.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileKeeper.Core.UseCases;

public class RestoreBackupUseCase : IRestoreBackupUseCase
{
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly IFileWrapper _fileWrapper;
    private readonly ICompressedEncryptedFileWriter _compressedEncryptedFileWriter;
    private readonly IOptionsMonitor<UserSettingsOptions> _userSettingsOptions;
    private readonly ILogger<DeleteBackupUseCase> _logger;

    public RestoreBackupUseCase(
        ISnapshotRepository snapshotRepository,
        IFileWrapper fileWrapper,
        ICompressedEncryptedFileWriter compressedEncryptedFileWriter,
        IOptionsMonitor<UserSettingsOptions> userSettingsOptions,
        ILogger<DeleteBackupUseCase> logger)
    {
        _snapshotRepository = snapshotRepository;
        _fileWrapper = fileWrapper;
        _compressedEncryptedFileWriter = compressedEncryptedFileWriter;
        _userSettingsOptions = userSettingsOptions;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> ExecuteAsync(Guid snapshotId, string destinationFolder, IProgress<BackupProgress>? progress,
        CancellationToken token)
    {
        _logger.LogInformation("Starting backup {SnapshotID} restoration process.", snapshotId);
        
        var configuration = _userSettingsOptions.CurrentValue;
        var storageDir = Path.Combine(configuration.StorageDirectory, "data");

        var snapshotResult = await _snapshotRepository.GetSnapshotAsync(snapshotId, token);
        if (snapshotResult.IsError)
            return snapshotResult.Errors;

        var snapshot = snapshotResult.Value;
        
        var currentFileIndex = 0;
        var totalFiles = snapshot.Files.Count;

        foreach (var file in snapshot.Files)
        {
            if (token.IsCancellationRequested) break;

            currentFileIndex++;
            
            progress?.Report(new BackupProgress
            {
                CurrentFileIndex = currentFileIndex,
                TotalFiles = totalFiles,
                CurrentFileName = file.RelativePath,
                CurrentFolder = file.SourceDirectory
            });

            var fullFilePath = Path.Combine(storageDir, file.StoredPath);
            
            var outputFilePath = Path.Combine(
                destinationFolder,
                file.SourceDirectory.TrimStart(Path.DirectorySeparatorChar),
                file.RelativePath);
            
            var destinationWithRelativeFolder = Path.GetDirectoryName(outputFilePath);
            
            _logger.LogInformation(
                "Restoring file {FullFilePath} to {OutputFilePath}",
                fullFilePath,
                outputFilePath);

            _fileWrapper.CreateDirectoryIfNotExists(destinationWithRelativeFolder!);

            await _compressedEncryptedFileWriter.DecompressAndDecryptFileAsync(
                fullFilePath,
                outputFilePath,
                token);
        }

        if (token.IsCancellationRequested)
            return Error.Unexpected(description: "Operation cancelled");

        _logger.LogInformation("Backup {SnapshotID} restoration process completed.", snapshotId);

        return Result.Success;
    }
}