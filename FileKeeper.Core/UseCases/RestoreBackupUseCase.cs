using ErrorOr;
using FileKeeper.Core.Interfaces.UseCases;

namespace FileKeeper.Core.UseCases;

public class RestoreBackupUseCase : IRestoreBackupUseCase
{
    public Task<ErrorOr<Success>> ExecuteAsync(long backupId, string destinationFolder, CancellationToken token)
    {
        throw new NotImplementedException();
    }
}