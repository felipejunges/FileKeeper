using ErrorOr;
using FileKeeper.Core.Models;

namespace FileKeeper.Core.Interfaces.UseCases;

public interface IRestoreBackupUseCase
{
    Task<ErrorOr<Success>> ExecuteAsync(long backupId, string destinationFolder, CancellationToken token, IProgress<RestoreProgress>? progress = null);
}