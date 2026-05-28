using ErrorOr;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Models;
using FileKeeper.Core.Models.DTOs;
using FileKeeper.Core.Models.Entities;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FileKeeper.Core.Services;

public class SnapshotService : ISnapshotService
{
    private readonly IFileStoreRepository _fileStoreRepository;
    private readonly ILogger<SnapshotService> _logger;

    public SnapshotService(
        IFileStoreRepository fileStoreRepository,
        ILogger<SnapshotService> logger)
    {
        _fileStoreRepository = fileStoreRepository;
        _logger = logger;
    }
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    
    private static readonly string IndexFileName = "index.json";

    public async Task<ErrorOr<SnapshotIndex>> GetIndexAsync(CancellationToken token)
    {
        try
        {
            var stream = await _fileStoreRepository.GetFileContentStreamAsync(IndexFileName, token);

            if (stream.CanSeek)
                stream.Position = 0;

            var model = await JsonSerializer.DeserializeAsync<SnapshotIndex>(stream, JsonOptions, token);

            return model ?? SnapshotIndex.Empty();
        }
        catch (FileNotFoundException)
        {
            return SnapshotIndex.Empty();
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
        try
        {
            await using var stream = new MemoryStream();

            await JsonSerializer.SerializeAsync(stream, index, JsonOptions, token);
            stream.Position = 0;

            await _fileStoreRepository.AddStreamAsync(stream, IndexFileName, token);

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

    public async Task<ErrorOr<Success>> AddFileAsync(FileToSave file, CancellationToken token)
    {
        try
        {
            await _fileStoreRepository.AddFileAsync(file, token);

            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding file {SourceFilePath} to the repository.", file.FullPath);
            
            return Error.Failure(
                code: $"{nameof(SnapshotService)}.{nameof(AddFileAsync)}",
                description: $"Error adding file {file.FullPath} to the repository: {ex.Message}");
        }
    }

    public async Task<ErrorOr<Success>> AddFilesAsync(IEnumerable<FileToSave> files, IProgress<BackupProgress>? progress, CancellationToken token)
    {
        try
        {
            await _fileStoreRepository.AddFilesAsync(files, progress, token);

            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding file list {Files} to the repository.", files);
            
            return Error.Failure(
                code: $"{nameof(SnapshotService)}.{nameof(AddFilesAsync)}",
                description: $"Error adding file list to the repository: {ex.Message}");
        }
    }

    public async Task<ErrorOr<Success>> RestoreFileAsync(FileToRestore file, CancellationToken token)
    {
        try
        {
            await _fileStoreRepository.ExtractFileAsync(file, token);

            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring file {FullPath} from the repository.", file.FullPath);
            
            return Error.Failure(
                code: $"{nameof(SnapshotService)}.{nameof(RestoreFileAsync)}",
                description: $"Error restoring file {file.FullPath} from the repository: {ex.Message}");
        }
    }

    public async Task<ErrorOr<Success>> RestoreFilesAsync(IEnumerable<FileToRestore> files, IProgress<BackupProgress>? progress, CancellationToken token)
    {
        try
        {
            await _fileStoreRepository.ExtractFilesAsync(files, progress, token);

            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring file list {Files} from the repository.", files);
            
            return Error.Failure(
                code: $"{nameof(SnapshotService)}.{nameof(RestoreFileAsync)}",
                description: $"Error restoring file list from the repository: {ex.Message}");
        }
    }

    public async Task<ErrorOr<Success>> DeleteFileAsync(FileToDelete file, CancellationToken token)
    {
        try
        {
            await _fileStoreRepository.DeleteFileAsync(file, token);

            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {StoredPath} in the repository.", file.StoredPath);
            
            return Error.Failure(
                code: $"{nameof(SnapshotService)}.{nameof(DeleteFileAsync)}",
                description: $"Error deleting file {file.StoredPath} from the repository: {ex.Message}");
        }
    }
    
    public async Task<ErrorOr<Success>> DeleteFilesAsync(IEnumerable<FileToDelete> files, CancellationToken token)
    {
        try
        {
            await _fileStoreRepository.DeleteFilesAsync(files, token);

            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting files {Files} in the repository.", files);
            
            return Error.Failure(
                code: $"{nameof(SnapshotService)}.{nameof(DeleteFilesAsync)}",
                description: $"Error deleting files from the repository: {ex.Message}");
        }
    }
    
    public async Task<ErrorOr<Success>> RemoveEmptyDirectoriesAsync(CancellationToken token)
    {
        try
        {
            await _fileStoreRepository.RemoveEmptyDirectoriesAsync(token);

            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning empty folders in the repository.");
            
            return Error.Failure(
                code: $"{nameof(SnapshotService)}.{nameof(DeleteFileAsync)}",
                description: $"Error cleaning empty folders in the repository: {ex.Message}");
        }
    }
}