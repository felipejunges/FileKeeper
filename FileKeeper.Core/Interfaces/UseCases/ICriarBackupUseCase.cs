using ErrorOr;
using FileKeeper.Core.Models.Entities;

namespace FileKeeper.Core.Interfaces.UseCases;

public interface ICriarBackupUseCase
{
    Task<ErrorOr<Backup>> ExecuteAsync(CancellationToken token);
}