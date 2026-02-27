using FileKeeper.Core.Application;
using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileKeeper.Core.Persistence;

public class ConfigurationStore : IConfigurationStore
{
    private readonly string _configFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    private Configuration? _cachedConfiguration;

    public ConfigurationStore()
    {
        var paths = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FileKeeper",
            "config.json"
        };

        if (ApplicationInfo.IsDebug)
            paths.Insert(2, "debug");

        _configFilePath = Path.Combine(paths.ToArray());
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<Configuration> LoadConfigurationAsync(CancellationToken token)
    {
        if (_cachedConfiguration != null)
        {
            return _cachedConfiguration;
        }

        if (File.Exists(_configFilePath))
        {
            using var fileStream = new FileStream(_configFilePath, FileMode.Open, FileAccess.Read);
            var config = await JsonSerializer.DeserializeAsync<Configuration>(fileStream, _jsonOptions, token);

            if (config != null)
            {
                _cachedConfiguration = config;
                return config;
            }
        }

        _cachedConfiguration = Configuration.DefaultConfiguration();

        return _cachedConfiguration;
    }

    public async Task SaveConfigurationAsync(Configuration configuration, CancellationToken token)
    {
        // Ensure the directory exists
        string? directory = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Serialize and save the configuration to the file
        using var fileStream = new FileStream(_configFilePath, FileMode.Create, FileAccess.Write);
        await JsonSerializer.SerializeAsync(fileStream, configuration, _jsonOptions, token);
        
        _cachedConfiguration = configuration;

        Console.WriteLine($"Configuration saved successfully to {_configFilePath}");
    }
}