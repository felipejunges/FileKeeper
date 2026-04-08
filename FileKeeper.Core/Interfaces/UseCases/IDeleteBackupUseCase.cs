using ErrorOr;
using FileKeeper.Core.Models;

namespace FileKeeper.Core.Interfaces.UseCases;

public interface IDeleteBackupUseCase
{
    Task<ErrorOr<Success>> ExecuteAsync(Guid snapshotId, IProgress<BackupProgress>? progress, CancellationToken token);
}