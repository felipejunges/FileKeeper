using ErrorOr;
using FileKeeper.Core.Models.Entities;

namespace FileKeeper.Core.Interfaces.Repositories;

public interface IBackupRepository
{
    Task<ErrorOr<IEnumerable<Backup>>> GetAllAsync(CancellationToken token);
    Task<ErrorOr<long>> InsertAsync(Backup backup, CancellationToken token);
    Task UpdateAsync(Backup backup, CancellationToken token);
}