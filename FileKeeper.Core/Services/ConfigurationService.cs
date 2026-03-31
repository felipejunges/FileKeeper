using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Models.Entities;

namespace FileKeeper.Core.Services;

public class ConfigurationService : IConfigurationService
{
    public Task<Configuration> GetConfigurationAsync(CancellationToken token)
    {
        throw new NotImplementedException();
    }
}