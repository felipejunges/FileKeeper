using FileKeeper.Core.Models;

namespace FileKeeper.Core.Interfaces;

public interface IIndexService
{
    Task<BackupIndex> GetBackupIndexAsync(CancellationToken cancellationToken);
}