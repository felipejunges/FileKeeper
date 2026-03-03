using ErrorOr;

namespace FileKeeper.Core.Interfaces.UseCases;

public interface IRestoreBackupUseCase
{
    Task<ErrorOr<Success>> ExecuteAsync(long backupId, string destinationFolder, CancellationToken token);
}