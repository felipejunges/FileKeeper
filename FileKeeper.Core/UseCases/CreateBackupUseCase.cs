using ErrorOr;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Interfaces.Wrappers;
using FileKeeper.Core.Models;
using FileKeeper.Core.Models.DTOs;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Models.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileKeeper.Core.UseCases;

public class CreateBackupUseCase : ICreateBackupUseCase
{
    private readonly ISnapshotService _snapshotService;
    private readonly IFileWrapper _fileWrapper;
    private readonly IOptionsMonitor<UserSettingsOptions> _userSettingsOptions;
    private readonly ILogger<CreateBackupUseCase> _logger;

    public CreateBackupUseCase(
        ISnapshotService snapshotService,
        IFileWrapper fileWrapper,
        IOptionsMonitor<UserSettingsOptions> userSettingsOptions,
        ILogger<CreateBackupUseCase> logger)
    {
        _snapshotService = snapshotService;
        _fileWrapper = fileWrapper;
        _userSettingsOptions = userSettingsOptions;
        _logger = logger;

        _userSettingsOptions.OnChange(_ =>
        {
            // Handle UserSettings changes here if needed
            // For example, log or notify about changes
        });
    }

    public async Task<ErrorOr<Snapshot>> ExecuteAsync(IProgress<BackupProgress>? progress, CancellationToken token)
    {
        _logger.LogInformation("Starting backup creation process.");

        var configuration = _userSettingsOptions.CurrentValue;

        var snapshotIndexResult = await _snapshotService.GetIndexAsync(token);
        if (snapshotIndexResult.IsError)
            return snapshotIndexResult.Errors;

        var snapshotIndex = snapshotIndexResult.Value;

        var lastSnapshot = snapshotIndex.Snapshots.LastOrDefault();

        var newSnapshot = Snapshot.Create();

        LogSnapshotsInfo(newSnapshot, lastSnapshot);
        
        foreach (var sourceDirectory in configuration.SourceDirectories)
        {
            if (token.IsCancellationRequested) break;

            var filesOnDisk = _fileWrapper.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);

            // TODO: think about Parallel

            var currentFileIndex = 0;
            var totalFiles = filesOnDisk.Length;

            var filesToSave = new List<FileToSave>();
            var filesToKeep = new List<FileEntry>();

            foreach (var fileOnDisk in filesOnDisk)
            {
                if (token.IsCancellationRequested) break;

                currentFileIndex++;

                progress?.Report(new BackupProgress
                {
                    CurrentFileIndex = currentFileIndex,
                    TotalFiles = totalFiles,
                    CurrentFileName = fileOnDisk,
                    CurrentFolder = sourceDirectory
                });

                if (CheckShouldIgnoreFolder(configuration.IgnoredFolders, fileOnDisk))
                {
                    _logger.LogInformation("Processing '{FilePath}': Skipping because it is in an ignored folder.", fileOnDisk);
                    continue;
                }

                var fileToSaveResult = await CreateFileToSaveAsync(fileOnDisk, sourceDirectory, token);
                if (fileToSaveResult.IsError)
                    continue;

                var fileToSave = fileToSaveResult.Value;

                var existingFile = lastSnapshot?.Files.FirstOrDefault(f =>
                    f.SourceDirectory == sourceDirectory
                    && f.RelativePath == fileToSave.RelativePath);

                if (existingFile == null)
                {
                    // Is a new file: we need to add it to the data structure
                    fileToSave.UpdateFoundIn(newSnapshot.SnapshotName);
                    filesToSave.Add(fileToSave);

                    _logger.LogInformation("Processing '{FilePath}': new file", fileOnDisk);
                    _logger.LogDebug("New file hash: {NewHash}", fileToSave.Hash);
                }
                else if (existingFile.Hash != fileToSave.Hash)
                {
                    // File exists, but hash is different: store its data structure
                    fileToSave.UpdateFoundIn(newSnapshot.SnapshotName);
                    filesToSave.Add(fileToSave);

                    _logger.LogInformation("Processing '{FilePath}': file changed", fileOnDisk);
                    _logger.LogTrace("Existing file hash: {ExistingHash}, New file hash: {NewHash}", existingFile.Hash, fileToSave.Hash);
                }
                else
                {
                    // File exists and hash is the same: we can reuse the stored file from the last snapshot
                    filesToKeep.Add(existingFile);

                    _logger.LogInformation("Processing '{FilePath}': file unchanged", fileOnDisk);
                }
            }

            foreach (var fileToSave in filesToSave)
            {
                newSnapshot.AddFile(
                    FileEntry.Create(
                        sourceDirectory,
                        fileToSave.RelativePath,
                        fileToSave.StoredPath,
                        fileToSave.Hash,
                        fileToSave.Size,
                        fileToSave.LastModified,
                        fileToSave.FoundInSnapshot));
            }

            foreach (var fileToKeep in filesToKeep)
            {
                newSnapshot.AddFile(fileToKeep);
            }

            await _snapshotService.AddFilesAsync(filesToSave, token); // TODO: what if this fails??
            // TODO: idea: return de list of success, and add THEM to the snapshot
        }

        if (token.IsCancellationRequested)
            return Error.Unexpected(description: "Operation cancelled");

        newSnapshot.SortFiles();

        snapshotIndex.Snapshots.Add(newSnapshot);

        await _snapshotService.SaveIndexAsync(snapshotIndex, token);

        _logger.LogInformation("Backup creating process finished");

        return newSnapshot;
    }

    private void LogSnapshotsInfo(Snapshot newSnapshot, Snapshot? lastSnapshot)
    {
        _logger.LogInformation("Created new Snapshot {SnapshotName}", newSnapshot.SnapshotName);

        if (lastSnapshot != null)
            _logger.LogInformation("Last snapshot found: {SnapshotName} created on {CreatedOn}", lastSnapshot.SnapshotName,
                lastSnapshot.CreatedAtUtc);
        else
            _logger.LogInformation("No previous snapshot found. This will be the first backup.");
    }

    private async Task<ErrorOr<FileToSave>> CreateFileToSaveAsync(string fileOnDisk, string sourceDirectory, CancellationToken token)
    {
        var relativePath = Path.GetRelativePath(sourceDirectory, fileOnDisk);

        var guid = Guid.CreateVersion7().ToString("N");
        var storedPath = $"{guid[..8]}/{guid}";

        try
        {
            var fileInfo = await _fileWrapper.GetFileMetadataAsync(fileOnDisk, token);

            return new FileToSave(
                fullPath: fileOnDisk,
                storedPath: storedPath,
                relativePath: relativePath,
                hash: fileInfo.Hash,
                size: fileInfo.Size,
                lastModified: fileInfo.LastModified);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file metadata for '{FilePath}'", fileOnDisk);
            return Error.Failure(description: $"Failed to get file metadata for '{fileOnDisk}'");
        }
    }

    private bool CheckShouldIgnoreFolder(string[] ignoredFolders, string path)
    {
        if (ignoredFolders.Length == 0)
            return false;

        var pathComponents = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        return ignoredFolders.Any(ignoreFolder =>
            pathComponents.Any(component =>
                component.Equals(ignoreFolder, StringComparison.OrdinalIgnoreCase)));
    }
}