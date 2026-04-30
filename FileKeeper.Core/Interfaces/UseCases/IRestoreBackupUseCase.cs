using ErrorOr;
using FileKeeper.Core.Models;

namespace FileKeeper.Core.Interfaces.UseCases;

public interface IRestoreBackupUseCase
{
    Task<ErrorOr<Success>> ExecuteAsync(Guid snapshotId, string destinationFolder, IProgress<BackupProgress>? progress, CancellationToken token);
}