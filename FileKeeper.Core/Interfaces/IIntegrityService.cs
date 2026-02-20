using ErrorOr;

namespace FileKeeper.Core.Interfaces;

public interface IIntegrityService
{
    Task<ErrorOr<Success>> VerifyCompressedFilesIntegrityAsync(CancellationToken cancellationToken);
    Task<ErrorOr<Success>> VerifyLocalFilesDifferencesAsync(CancellationToken cancellationToken);
}
