using ErrorOr;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Wrappers;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Models.Errors;
using FileKeeper.Core.Models.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FileKeeper.Core.Repositories;

public class SnapshotRepository : ISnapshotRepository
{
    private static readonly JsonSerializerOptions SnapshotWriteOptions = new()
    {
        WriteIndented = true
    };

    private readonly IFileWrapper _fileWrapper;
    private readonly IOptions<UserSettingsOptions> _userSettingsOptions;
    private readonly ILogger<SnapshotRepository> _logger;

    public SnapshotRepository(
        IFileWrapper fileWrapper,
        IOptions<UserSettingsOptions> userSettingsOptions,
        ILogger<SnapshotRepository> logger)
    {
        _fileWrapper = fileWrapper;
        _userSettingsOptions = userSettingsOptions;
        _logger = logger;
    }

    public Task<IEnumerable<Snapshot>> GetAllSnapshotsAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        string[] snapshotFiles;
        try
        {
            snapshotFiles = _fileWrapper.GetFiles(_userSettingsOptions.Value.StorageDirectory, "*.json", SearchOption.TopDirectoryOnly);
        }
        catch (DirectoryNotFoundException)
        {
            _logger.LogWarning("Snapshots directory '{SnapshotsDirectory}' was not found.", _userSettingsOptions.Value.StorageDirectory);
            return Task.FromResult<IEnumerable<Snapshot>>([]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list snapshot files under '{SnapshotsDirectory}'.", _userSettingsOptions.Value.StorageDirectory);
            return Task.FromResult<IEnumerable<Snapshot>>([]);
        }

        return LoadSnapshotsAsync(snapshotFiles, token);
    }

    private async Task<IEnumerable<Snapshot>> LoadSnapshotsAsync(string[] snapshotFiles, CancellationToken token)
    {
        var snapshots = new List<Snapshot>(snapshotFiles.Length);

        foreach (var snapshotFile in snapshotFiles)
        {
            token.ThrowIfCancellationRequested();

            Stream? stream = null;
            try
            {
                stream = _fileWrapper.OpenRead(snapshotFile);
                var snapshot = await JsonSerializer.DeserializeAsync<Snapshot>(stream, cancellationToken: token);
                if (snapshot == null)
                {
                    _logger.LogWarning("Snapshot file '{SnapshotPath}' deserialized to null and will be skipped.", snapshotFile);
                    continue;
                }

                snapshots.Add(snapshot);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Snapshot file '{SnapshotPath}' contains invalid JSON and will be skipped.", snapshotFile);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read snapshot file '{SnapshotPath}'. File will be skipped.", snapshotFile);
            }
            finally
            {
                if (stream is not null)
                {
                    await stream.DisposeAsync();
                }
            }
        }

        return snapshots
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToList();
    }

    public Task<ErrorOr<Snapshot>> GetLastSnapshotAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return GetLastSnapshotInternalAsync(token);
    }

    private async Task<ErrorOr<Snapshot>> GetLastSnapshotInternalAsync(CancellationToken token)
    {
        var snapshots = await GetAllSnapshotsAsync(token);
        var latestSnapshot = snapshots.FirstOrDefault();

        if (latestSnapshot is null)
        {
            return Error.NotFound("No snapshots were found.");
        }

        return latestSnapshot;
    }

    public async Task<ErrorOr<Snapshot>> GetSnapshotAsync(Guid id, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var snapshotPath = Path.Combine(_userSettingsOptions.Value.StorageDirectory, $"{id}.json");

        if (!_fileWrapper.Exists(snapshotPath))
        {
            return Error.NotFound($"Snapshot file not found for id '{id}'.");
        }

        Stream stream;

        try
        {
            stream = _fileWrapper.OpenRead(snapshotPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open snapshot file '{SnapshotPath}' for reading.", snapshotPath);
            return Error.Failure(description: $"Failed to open snapshot file for id '{id}'.");
        }

        try
        {
            var snapshot = await JsonSerializer.DeserializeAsync<Snapshot>(stream, cancellationToken: token);
            if (snapshot == null)
            {
                return Error.Failure(description: $"Snapshot file '{snapshotPath}' deserialized to null.");
            }

            return snapshot;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize snapshot file '{SnapshotPath}' due to invalid JSON.", snapshotPath);
            return Error.Failure(description: $"Snapshot file '{snapshotPath}' contains invalid JSON.");
        }
        finally
        {
            await stream.DisposeAsync();
        }
    }

    public async Task<ErrorOr<Snapshot>> GetNextSnapshotAsync(Guid id, CancellationToken token)
    {
        var guid = GetNextSnapshotId(id, token);

        if (guid.IsError)
            return guid.Errors;

        return await GetSnapshotAsync(guid.Value, token);
    }

    public async Task<ErrorOr<Success>> AddSnapshotAsync(Snapshot snapshot, CancellationToken token)
    {
        if (token.IsCancellationRequested)
            return CommonErrors.OperationCanceled;

        var snapshotPath = Path.Combine(_userSettingsOptions.Value.StorageDirectory, $"{snapshot.Id}.json");

        return await SaveSnapshotAsync(snapshot, snapshotPath, token);
    }

    public async Task<ErrorOr<Success>> UpdateSnapshotAsync(Snapshot snapshot, CancellationToken token)
    {
        if (token.IsCancellationRequested)
            return CommonErrors.OperationCanceled;

        var snapshotPath = Path.Combine(_userSettingsOptions.Value.StorageDirectory, $"{snapshot.Id}.json");
        
        return await SaveSnapshotAsync(snapshot, snapshotPath, token);
    }

    public Task<ErrorOr<Success>> DeleteSnapshotAsync(Guid id, CancellationToken token)
    {
    }

    private async Task<ErrorOr<Success>> SaveSnapshotAsync(Snapshot snapshot, string snapshotPath, CancellationToken token)
    {
        var createFirectoryResult = StorageDirectoryIfNotExists();
        if (createFirectoryResult.IsError)
            return createFirectoryResult.Errors;
        
        Stream? stream = null;
        try
        {
            stream = _fileWrapper.Create(snapshotPath);
            await JsonSerializer.SerializeAsync(stream, snapshot, SnapshotWriteOptions, token);
            await stream.FlushAsync(token);

            return Result.Success;
        }
        catch (OperationCanceledException)
        {
            return CommonErrors.OperationCanceled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save snapshot '{SnapshotId}' into '{SnapshotPath}'.", snapshot.Id, snapshotPath);
            return Error.Failure(description: $"Failed to save snapshot '{snapshot.Id}'.");
        }
        finally
        {
            if (stream is not null)
            {
                await stream.DisposeAsync();
            }
        }
    }

    private ErrorOr<Guid> GetNextSnapshotId(Guid id, CancellationToken token)
    {
        string[] snapshotFiles;
        
        try
        {
            snapshotFiles = _fileWrapper.GetFiles(
                _userSettingsOptions.Value.StorageDirectory,
                "*.json",
                SearchOption.TopDirectoryOnly);
        }
        catch (DirectoryNotFoundException)
        {
            _logger.LogWarning(
                "Snapshots directory '{SnapshotsDirectory}' was not found.",
                _userSettingsOptions.Value.StorageDirectory);

            return Error.NotFound(description: "No snapshots were found.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to list snapshot files under '{SnapshotsDirectory}'.",
                _userSettingsOptions.Value.StorageDirectory);

            return Error.Failure(description: "Failed to enumerate snapshot files.");
        }

        var snapshotIds = new List<Guid>(snapshotFiles.Length);

        foreach (var file in snapshotFiles)
        {
            if (token.IsCancellationRequested)
                return CommonErrors.OperationCanceled;
            
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (Guid.TryParse(fileName, out var parsedId))
            {
                snapshotIds.Add(parsedId);
            }
        }
        
        if (snapshotIds.Count == 0)
        {
            return Error.NotFound(description: "No snapshots were found.");
        }

        // Snapshot IDs are generated as Guid v7 in this project, so ordering by Guid gives
        // a practical chronological sequence for "next".
        var orderedIds = snapshotIds
            .OrderByDescending(x => x)
            .ToList();
        
        if (token.IsCancellationRequested)
            return CommonErrors.OperationCanceled;

        var currentIndex = orderedIds.FindIndex(x => x == id);
        if (currentIndex < 0)
        {
            return Error.NotFound(description: $"Snapshot id '{id}' was not found.");
        }

        var nextIndex = currentIndex + 1;
        if (nextIndex >= orderedIds.Count)
        {
            return Error.NotFound(description: $"No next snapshot was found for id '{id}'.");
        }

        return orderedIds[nextIndex];
    }
    
    private ErrorOr<Success> StorageDirectoryIfNotExists()
    {
        try
        {
            _fileWrapper.CreateDirectoryIfNotExists(_userSettingsOptions.Value.StorageDirectory);
            
            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, 
                "Failed to create snapshots directory '{SnapshotsDirectory}'.",
                _userSettingsOptions.Value.StorageDirectory);
            
            return Error.Failure(description: $"Failed to create snapshots directory '{_userSettingsOptions.Value.StorageDirectory}'.");
        }
    }
}