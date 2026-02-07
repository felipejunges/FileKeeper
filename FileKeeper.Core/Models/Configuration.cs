namespace FileKeeper.Core.Models;

public class Configuration
{
    public List<string> SourceDirectories { get; set; } = new List<string>();
    public string DestinationDirectory { get; set; } = string.Empty;
    public DateTime? LastBackup { get; set; }
    public int KeepMaxBackups { get; set; } = 0; // 0 = Keep All
    public bool UseCompression { get; set; } = false;
}