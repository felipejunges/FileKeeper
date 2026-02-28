using ErrorOr;

namespace FileKeeper.Core.Interfaces.UseCases;

public interface ICriarBackupUseCase
{
    Task<ErrorOr<Success>> ExecuteAsync(CancellationToken token);
}