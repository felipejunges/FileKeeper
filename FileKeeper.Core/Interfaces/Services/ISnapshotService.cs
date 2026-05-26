using ErrorOr;
using FileKeeper.Core.Models.Entities;

namespace FileKeeper.Core.Interfaces.Services;

public interface ISnapshotService : IAsyncDisposable, IDisposable
{
    Task<ErrorOr<SnapshotIndex>> GetIndexAsync(CancellationToken token);
    
    Task<ErrorOr<Snapshot>> GetSnapshotAsync(Guid id, CancellationToken token);
    
    Task<ErrorOr<Success>> SaveIndexAsync(SnapshotIndex index, CancellationToken token);

    Task<ErrorOr<Success>> AddSnapshotAsync(Snapshot snapshot, CancellationToken token);

    Task<ErrorOr<Success>> AddFileAsync(string sourceFilePath, string? entryPath, CancellationToken token);
    
    Task FlushFilesAsync(CancellationToken token);
}