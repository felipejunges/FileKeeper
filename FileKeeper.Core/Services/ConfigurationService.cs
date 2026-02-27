using ErrorOr;
using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Models;

namespace FileKeeper.Core.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly IConfigurationStore _store;
    
    public ConfigurationService(IConfigurationStore store)
    {
        _store = store;
    }
    
    private ErrorOr<Success> ValidateConfiguration(Configuration configuration)
    {
        var errors = new List<Error>();

        if (configuration.MonitoredFolders.Count == 0)
        {
            errors.Add(Error.Validation(description: "At least one monitored folder must be specified."));
        }
        else
        {
            foreach (var folder in configuration.MonitoredFolders)
            {
                if (!Directory.Exists(folder))
                {
                    errors.Add(Error.Validation(description: $"Monitored folder does not exist: {folder}"));
                }
            }
        }

        if (configuration.VersionsToKeep <= 0)
        {
            errors.Add(Error.Validation(description: "Versions to keep must be greater than 0."));
        }

        if (string.IsNullOrWhiteSpace(configuration.DatabaseLocation))
        {
            errors.Add(Error.Validation(description: "Database location must be specified."));
        }

        if (configuration.AutoBackupIntervalMinutes < 0)
        {
            errors.Add(Error.Validation(description: "Auto backup interval cannot be negative."));
        }

        if (configuration.MaxDatabaseSizeMb < 0)
        {
            errors.Add(Error.Validation(description: "Maximum database size cannot be negative."));
        }

        if (errors.Count > 0)
            return errors;

        return Result.Success;
    }
    
    public async Task<Configuration> GetConfigurationAsync(CancellationToken token)
    {
        return await _store.LoadConfigurationAsync(token);
    }
    
    public async Task<ErrorOr<Success>> ApplyConfigurationAsync(Configuration config, CancellationToken token)
    {
        var validationResult = ValidateConfiguration(config);
        if (validationResult.IsError)
            return validationResult.Errors;

        config.LastModified = DateTime.Now;
        
        await _store.SaveConfigurationAsync(config, token);
        
        return Result.Success;
    }
}