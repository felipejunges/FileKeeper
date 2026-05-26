using ErrorOr;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Models.Options;
using Microsoft.Extensions.Options;
using System.IO.Compression;

namespace FileKeeper.Core.Services;

public class SnapshotService : ISnapshotService
{
    private readonly ITarRepository _tarRepository;
    private readonly IOptionsMonitor<UserSettingsOptions> _userSettingsOptions;

    public SnapshotService(
        ITarRepository tarRepository,
        IOptionsMonitor<UserSettingsOptions> userSettingsOptions)
    {
        _tarRepository = tarRepository;
        _userSettingsOptions = userSettingsOptions;
    }

    public Task<ErrorOr<SnapshotIndex>> GetIndexAsync(CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public Task<ErrorOr<Snapshot>> GetSnapshotAsync(Guid id, CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public Task<ErrorOr<Success>> SaveIndexAsync(SnapshotIndex index, CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public Task<ErrorOr<Success>> AddSnapshotAsync(Snapshot snapshot, CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public async Task<ErrorOr<Success>> AddFileAsync(string sourceFilePath, string? entryPath, CancellationToken token)
    {
        if (!_tarRepository.IsOpen)
        {
            _tarRepository.Open(
                _userSettingsOptions.CurrentValue.StorageDirectory,
                CompressionMode.Compress,
                true);
        }

        try
        {
            await _tarRepository.AddFileAsync(sourceFilePath, entryPath, token);

            return Result.Success;
        }
        catch (Exception ex)
        {
            return Error.Failure(
                code: "AddFile.Exception",
                description: $"Error add file to the snapshot data: {ex.Message}");
        }
    }

    public Task FlushFilesAsync(CancellationToken token)
    {
        return _tarRepository.FlushAsync(token);
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