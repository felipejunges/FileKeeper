using ErrorOr;
using FileKeeper.Core.Models.Entities;

namespace FileKeeper.Core.Interfaces.Repositories;

public interface IBackupRepository
{
    Task<ErrorOr<Backup>> GetByIdAsync(long id, CancellationToken token);
    Task<ErrorOr<int>> GetCountAsync(CancellationToken cancellationToken);
    Task<ErrorOr<Backup>> GetNextBackupAfterAsync(DateTime dateTime, CancellationToken token);
    Task<ErrorOr<IEnumerable<Backup>>> GetAllAsync(CancellationToken token);
    Task<ErrorOr<long>> GetAllBackupsTotalSizeAsync(CancellationToken token);
    Task<ErrorOr<long>> InsertAsync(Backup backup, CancellationToken token);
    Task UpdateAsync(Backup backup, CancellationToken token);
    Task<ErrorOr<int>> DeleteAsync(long backupId, CancellationToken token);
    Task<ErrorOr<Backup>> GetOldestAsync(CancellationToken cancellationToken);
}