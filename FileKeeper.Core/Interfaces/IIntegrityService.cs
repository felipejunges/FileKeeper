using ErrorOr;

namespace FileKeeper.Core.Interfaces;

public interface IIntegrityService
{
    Task<ErrorOr<Success>> VerifyIntegrityAsync(CancellationToken cancellationToken);
}
