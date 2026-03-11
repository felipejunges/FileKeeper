using ErrorOr;
using FileKeeper.Core.Models;
using FileKeeper.Core.Models.Entities;

namespace FileKeeper.Core.Interfaces.UseCases;

public interface ICreateBackupUseCase
{
    Task<ErrorOr<Backup>> ExecuteAsync(IProgress<BackupProgress>? progress, CancellationToken token);
}