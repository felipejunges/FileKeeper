using ErrorOr;
using FileKeeper.Core.Models;
using FileKeeper.Core.Models.Entities;

namespace FileKeeper.Core.Interfaces.Repositories;

public interface ISnapshotRepository
{
    Task<ErrorOr<IEnumerable<Snapshot>>> GetAllSnapshotsAsync(CancellationToken token);
    Task<ErrorOr<Snapshot>> GetLastSnapshotAsync(CancellationToken token);
    Task<ErrorOr<Snapshot>> GetSnapshotAsync(Guid id, CancellationToken token);
    Task<ErrorOr<Snapshot>> GetNextSnapshotAsync(Guid id, CancellationToken token);
    Task<ErrorOr<Success>> AddSnapshotAsync(Snapshot snapshot, CancellationToken token);
    Task<ErrorOr<Success>> UpdateSnapshotAsync(Snapshot snapshot, CancellationToken token);
    Task<ErrorOr<Success>> DeleteSnapshotAsync(Guid id, CancellationToken token);
}