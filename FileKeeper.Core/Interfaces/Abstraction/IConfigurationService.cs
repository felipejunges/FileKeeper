using FileKeeper.Core.Models;

namespace FileKeeper.Core.Interfaces.Abstraction;

public interface IConfigurationService
{
    Task<Configuration> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(Configuration configuration, CancellationToken cancellationToken);
}