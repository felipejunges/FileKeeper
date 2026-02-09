namespace FileKeeper.Core.Models;

public class Configuration
{
    public List<string> SourceDirectories { get; set; } = new List<string>();
    public string DestinationDirectory { get; set; } = string.Empty;
    public int KeepMaxBackups { get; set; } = 0; // 0 = Keep All
    public CompressionTypeConfiguration CompressionType { get; set; }
}

public enum CompressionTypeConfiguration
{
    Zip,
    Tar
}