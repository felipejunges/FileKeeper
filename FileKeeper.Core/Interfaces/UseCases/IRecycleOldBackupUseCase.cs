using ErrorOr;

namespace FileKeeper.Core.Interfaces.UseCases;

public interface IRecycleOldBackupUseCase
{
    Task<ErrorOr<int>> ExecuteAsync(CancellationToken cancellationToken);
}