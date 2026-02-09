using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Models;
using System.Text.Json;

namespace FileKeeper.Core.Services.Abstraction;

public class ConfigurationService : IConfigurationService
{
    private readonly string _filePath;

    public ConfigurationService()
    {
        _filePath = Path.Combine(AppContext.BaseDirectory, "config.json");
    }

    public async Task<Configuration> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new Configuration
            {
                DestinationDirectory = string.Empty,
                SourceDirectories = new List<string>()
            };
        }

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
        return JsonSerializer.Deserialize<Configuration>(json) ?? new Configuration
        {
            DestinationDirectory = string.Empty,
            SourceDirectories = new List<string>()
        };
    }

    public async Task SaveAsync(Configuration configuration, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }
}