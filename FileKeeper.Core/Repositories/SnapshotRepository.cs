using ErrorOr;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Wrappers;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Wrappers;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FileKeeper.Core.Repositories;

public class SnapshotRepository : ISnapshotRepository
{
    private readonly string _snapshotsDirectory;
    private readonly IFileWrapper _fileWrapper;
    
    private readonly ILogger<SnapshotRepository> _logger;

    public SnapshotRepository(
        ILogger<SnapshotRepository> logger,
        IFileWrapper? fileWrapper = null,
        string? snapshotsDirectory = null)
    {
        _logger = logger;
        _fileWrapper = fileWrapper ?? new FileWrapper();
        _snapshotsDirectory = snapshotsDirectory ?? Path.Combine(AppContext.BaseDirectory, "snapshots");
    }

    public Task<IEnumerable<Snapshot>> GetAllSnapshotsAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        string[] snapshotFiles;
        try
        {
            snapshotFiles = _fileWrapper.GetFiles(_snapshotsDirectory, "*.json", SearchOption.TopDirectoryOnly);
        }
        catch (DirectoryNotFoundException)
        {
            _logger.LogInformation("Snapshots directory '{SnapshotsDirectory}' was not found.", _snapshotsDirectory);
            return Task.FromResult<IEnumerable<Snapshot>>([]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list snapshot files under '{SnapshotsDirectory}'.", _snapshotsDirectory);
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
        throw new NotImplementedException();
    }

    public async Task<ErrorOr<Snapshot>> GetSnapshotAsync(Guid id, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var snapshotPath = Path.Combine(_snapshotsDirectory, $"{id}.json");

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
            return Error.Failure($"Failed to open snapshot file for id '{id}'.");
        }

        try
        {
            var snapshot = await JsonSerializer.DeserializeAsync<Snapshot>(stream, cancellationToken: token);
            if (snapshot == null)
            {
                return Error.Failure($"Snapshot file '{snapshotPath}' deserialized to null.");
            }

            return snapshot;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize snapshot file '{SnapshotPath}' due to invalid JSON.", snapshotPath);
            return Error.Failure($"Snapshot file '{snapshotPath}' contains invalid JSON.");
        }
        finally
        {
            await stream.DisposeAsync();
        }
    }

    public Task<ErrorOr<Success>> AddSnapshotAsync(Snapshot snapshot, CancellationToken token)
    {
        throw new NotImplementedException();
    }
}