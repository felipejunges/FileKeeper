using ErrorOr;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Wrappers;
using FileKeeper.Core.Models.Entities;
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

    public Task<ErrorOr<Success>> AddSnapshotAsync(Snapshot snapshot, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var snapshotPath = Path.Combine(_userSettingsOptions.Value.StorageDirectory, $"{snapshot.Id}.json");

        try
        {
            _fileWrapper.CreateDirectoryIfNotExists(_userSettingsOptions.Value.StorageDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create snapshots directory '{SnapshotsDirectory}'.", _userSettingsOptions.Value.StorageDirectory);
            return Task.FromResult<ErrorOr<Success>>(Error.Failure(description: $"Failed to create snapshots directory '{_userSettingsOptions.Value.StorageDirectory}'."));
        }

        return SaveSnapshotAsync(snapshot, snapshotPath, token);
    }

    private async Task<ErrorOr<Success>> SaveSnapshotAsync(Snapshot snapshot, string snapshotPath, CancellationToken token)
    {
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
            throw;
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
}