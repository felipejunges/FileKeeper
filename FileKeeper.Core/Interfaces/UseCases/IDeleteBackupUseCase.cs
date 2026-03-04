using ErrorOr;

namespace FileKeeper.Core.Interfaces.UseCases;

public interface IDeleteBackupUseCase
{
    Task<ErrorOr<Success>> ExecuteAsync(long backupId, CancellationToken token);
}