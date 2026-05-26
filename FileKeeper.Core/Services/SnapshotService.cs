using ErrorOr;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Models.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Compression;
using System.Text.Json;

namespace FileKeeper.Core.Services;

public class SnapshotService : ISnapshotService
{
    private readonly ITarRepository _tarRepository;
    private readonly IOptionsMonitor<UserSettingsOptions> _userSettingsOptions;
    private readonly ILogger<SnapshotService> _logger;

    public SnapshotService(
        ITarRepository tarRepository,
        IOptionsMonitor<UserSettingsOptions> userSettingsOptions,
        ILogger<SnapshotService> logger)
    {
        _tarRepository = tarRepository;
        _userSettingsOptions = userSettingsOptions;
        _logger = logger;
    }
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    
    private static readonly string IndexFileName = "index.json";

    public async Task<ErrorOr<SnapshotIndex>> GetIndexAsync(CancellationToken token)
    {
        OpenRepositoryIfClosed(CompressionMode.Decompress);

        try
        {
            var stream = await _tarRepository.GetFileContentStreamAsync(IndexFileName, token);

            if (stream.CanSeek)
                stream.Position = 0;

            var model = await JsonSerializer.DeserializeAsync<SnapshotIndex>(stream, JsonOptions, token);

            return model ?? SnapshotIndex.Empty();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting snapshot index.");
            
            return Error.Failure(
                code: $"{nameof(SnapshotService)}.{nameof(GetIndexAsync)}",
                description: $"Error getting snapshot index: {ex.Message}");
        }
    }

    public async Task<ErrorOr<Success>> SaveIndexAsync(SnapshotIndex index, CancellationToken token)
    {
        OpenRepositoryIfClosed(CompressionMode.Compress);

        try
        {
            await using var stream = new MemoryStream();

            await JsonSerializer.SerializeAsync(stream, index, JsonOptions, token);
            stream.Position = 0;

            await _tarRepository.AddStreamAsync(stream, IndexFileName, token);
            await FlushFilesAsync(token);

            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving snapshot index.");
            
            return Error.Failure(
                code: $"{nameof(SnapshotService)}.{nameof(SaveIndexAsync)}",
                description: $"Error saving snapshot index: {ex.Message}");
        }
    }

    public async Task<ErrorOr<Success>> AddFileAsync(string sourceFilePath, string entryPath, CancellationToken token)
    {
        OpenRepositoryIfClosed(CompressionMode.Compress);

        try
        {
            await _tarRepository.AddFileAsync(sourceFilePath, entryPath, token);

            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding file {SourceFilePath} to the repository.", sourceFilePath);
            
            return Error.Failure(
                code: $"{nameof(SnapshotService)}.{nameof(AddFileAsync)}",
                description: $"Error adding file {sourceFilePath} to the repository: {ex.Message}");
        }
    }

    public async Task<ErrorOr<Success>> RestoreFileAsync(string entryPath, string outputFilePath, CancellationToken token)
    {
        OpenRepositoryIfClosed(CompressionMode.Decompress);
        
        try
        {
            await _tarRepository.ExtractFileAsync(entryPath, outputFilePath, token);

            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring file {EntryPath} from the repository.", entryPath);
            
            return Error.Failure(
                code: $"{nameof(SnapshotService)}.{nameof(RestoreFileAsync)}",
                description: $"Error restoring file {entryPath} from the repository: {ex.Message}");
        }
    }

    public async Task<ErrorOr<Success>> DeleteFileAsync(string entryPath, CancellationToken token)
    {
        OpenRepositoryIfClosed(CompressionMode.Compress);
        
        try
        {
            await _tarRepository.DeleteFileAsync(entryPath, token);

            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {EntryPath} in the repository.", entryPath);
            
            return Error.Failure(
                code: $"{nameof(SnapshotService)}.{nameof(DeleteFileAsync)}",
                description: $"Error deleting file {entryPath} from the repository: {ex.Message}");
        }
    }

    public Task FlushFilesAsync(CancellationToken token)
    {
        return _tarRepository.FlushAsync(token);
    }

    private void OpenRepositoryIfClosed(CompressionMode compressionMode)
    {
        if (_tarRepository.IsOpen)
            return;

        _tarRepository.Open(
            Path.Combine(_userSettingsOptions.CurrentValue.StorageDirectory, "store.tar.gz"),
            compressionMode,
            true);
    }

    public void Dispose()
    {
        _tarRepository.Close();
        _tarRepository.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _tarRepository.Close();
        await _tarRepository.DisposeAsync();
    }
}