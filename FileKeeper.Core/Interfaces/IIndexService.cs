using FileKeeper.Core.Models;

namespace FileKeeper.Core.Interfaces;

public interface IIndexService
{
    Task<(BackupIndex, string)> GetBackupIndexAsync(CancellationToken cancellationToken);
}