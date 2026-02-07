using FileKeeper.Core.Models;

namespace FileKeeper.Core.Interfaces;

public interface IConfigurationService
{
    Configuration Load();
    void Save(Configuration configuration);
}