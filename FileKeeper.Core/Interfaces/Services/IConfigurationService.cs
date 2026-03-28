using FileKeeper.Core.Models.Entities;

namespace FileKeeper.Core.Interfaces.Services;

public interface IConfigurationService
{
    Task<Configuration> GetConfigurationAsync(CancellationToken token);
}