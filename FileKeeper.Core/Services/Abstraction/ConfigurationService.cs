using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Interfaces.Abstraction;
using FileKeeper.Core.Models;
using System.Text.Json;

namespace FileKeeper.Core.Services.Abstraction;

public class ConfigurationService : IConfigurationService
{
    private readonly string _filePath;

    private Configuration? _configuration;
    
    public ConfigurationService()
    {
        _filePath = Path.Combine(AppContext.BaseDirectory, "config.json");
    }

    public async Task<Configuration> LoadAsync(CancellationToken cancellationToken)
    {
        // se já está em cache...
        if (_configuration != null)
            return _configuration;

        // se o arquivo existe em disco, lê o arquivo seta no cache e retorna
        if (File.Exists(_filePath))
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
            _configuration = JsonSerializer.Deserialize<Configuration>(json) ?? new Configuration
            {
                DestinationDirectory = string.Empty,
                SourceDirectories = new List<string>()
            };
        }

        // senão, cria uma nova configuração, seta no cache e retorna
        _configuration = new Configuration
        {
            DestinationDirectory = string.Empty,
            SourceDirectories = new List<string>()
        };

        return _configuration;
    }

    public async Task SaveAsync(Configuration configuration, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);

        _configuration = configuration;
    }
}