using ErrorOr;
using FileKeeper.Core.Models;

namespace FileKeeper.Core.Interfaces.Services;

public interface IConfigurationService
{
    Task<Configuration> GetConfigurationAsync(CancellationToken token);
    Task<ErrorOr<Success>> ApplyConfigurationAsync(Configuration config, CancellationToken token);
}