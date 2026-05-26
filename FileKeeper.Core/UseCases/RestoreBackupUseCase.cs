using ErrorOr;
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
    private readonly ISnapshotService _snapshotService;
    private readonly IFileWrapper _fileWrapper;
    private readonly IOptionsMonitor<UserSettingsOptions> _userSettingsOptions;
    private readonly ILogger<DeleteBackupUseCase> _logger;

    public RestoreBackupUseCase(
        ISnapshotService snapshotService,
        IFileWrapper fileWrapper,
        IOptionsMonitor<UserSettingsOptions> userSettingsOptions,
        ILogger<DeleteBackupUseCase> logger)
    {
        _snapshotService = snapshotService;
        _fileWrapper = fileWrapper;
        _userSettingsOptions = userSettingsOptions;
        _logger = logger;
        _snapshotService = snapshotService;
    }

    public async Task<ErrorOr<Success>> ExecuteAsync(Guid snapshotId, string destinationFolder, IProgress<BackupProgress>? progress,
        CancellationToken token)
    {
        _logger.LogInformation("Starting backup {SnapshotID} restoration process.", snapshotId);
        
        var configuration = _userSettingsOptions.CurrentValue;
        var storageDir = Path.Combine(configuration.StorageDirectory, "data");

        var snapshotIndexResult = await _snapshotService.GetIndexAsync(token);
        if (snapshotIndexResult.IsError)
            return snapshotIndexResult.Errors;

        var snapshotIndex = snapshotIndexResult.Value;
        
        var snapshot = snapshotIndex.Snapshots.FirstOrDefault(s => s.Id == snapshotId);
        if (snapshot is null)
        {
            _logger.LogInformation("Snapshot {SnapshotId} doesn't exist.", snapshotId);
            return Error.NotFound($"Snapshot {snapshotId} doesn't exist.");
        }
        
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

            await _snapshotService.RestoreFileAsync(
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