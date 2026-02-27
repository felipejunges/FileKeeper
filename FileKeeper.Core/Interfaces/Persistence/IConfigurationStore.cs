using FileKeeper.Core.Models;

namespace FileKeeper.Core.Interfaces.Persistence;

public interface IConfigurationStore
{
    Task<Configuration> LoadConfigurationAsync(CancellationToken token);
    Task SaveConfigurationAsync(Configuration configuration, CancellationToken token);
}