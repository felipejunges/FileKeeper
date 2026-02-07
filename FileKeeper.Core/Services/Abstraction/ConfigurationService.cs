using System.Text.Json;
using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Models;

namespace FileKeeper.Core.Services.Abstraction;

public class ConfigurationService : IConfigurationService
{
    private readonly string _filePath;

    public ConfigurationService()
    {
        _filePath = Path.Combine(AppContext.BaseDirectory, "config.json");
    }

    public Configuration Load()
    {
        if (!File.Exists(_filePath))
        {
            // Retorna configuração padrão caso não exista
            return new Configuration
            {
                DestinationDirectory = string.Empty,
                SourceDirectories = new System.Collections.Generic.List<string>()
            };
        }

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<Configuration>(json) ?? new Configuration
        {
            DestinationDirectory = string.Empty,
            SourceDirectories = new System.Collections.Generic.List<string>()
        };
    }

    public void Save(Configuration configuration)
    {
        var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}