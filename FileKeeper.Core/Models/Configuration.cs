using System.Text.Json.Serialization;

namespace FileKeeper.Core.Models;

public class Configuration
{
    [JsonPropertyName("monitored_folders")]
    public List<string> MonitoredFolders { get; set; } = new();

    [JsonPropertyName("database_location")]
    public string DatabaseLocation { get; set; } = "";
    
    [JsonPropertyName("versions_to_keep")]
    public int VersionsToKeep { get; set; } = 5;

    [JsonPropertyName("auto_backup_interval_minutes")]
    public int AutoBackupIntervalMinutes { get; set; } = 0; // TODO: implement this on the Console or Service

    [JsonPropertyName("max_database_size_mb")]
    public long MaxDatabaseSizeMb { get; set; } = 0;

    [JsonPropertyName("enable_compression")]
    public bool EnableCompression { get; set; } = false; // TODO: consider this flag in the UseCases
    
    [JsonPropertyName("ignore_folders")]
    public string? IgnoreFolders { get; set; }
    
    [JsonPropertyName("current_restore_destination")]
    public string? CurrentRestoreDestination { get; set; }

    [JsonPropertyName("last_modified")]
    public DateTime LastModified { get; set; } = DateTime.Now;
    
    public static Configuration DefaultConfiguration()
    {
        return new Configuration
        {
            MonitoredFolders = new List<string>(),
            VersionsToKeep = 5,
            DatabaseLocation = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FileKeeper",
                "filekeeper.db"),
            AutoBackupIntervalMinutes = 0,
            MaxDatabaseSizeMb = 0,
            EnableCompression = false,
            LastModified = DateTime.Now
        };
    }
}