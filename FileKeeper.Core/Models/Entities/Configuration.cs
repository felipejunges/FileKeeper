namespace FileKeeper.Core.Models.Entities;

public class Configuration
{
    public string[] SourceDirectories { get; private set; } = Array.Empty<string>();
    public string StorageDirectory { get; private set; } = string.Empty;

    public Configuration()
    {
    }
    
    public Configuration(
        string[] sourceDirectories,
        string storageDirectory)
    {
        SourceDirectories = sourceDirectories;
        StorageDirectory = storageDirectory;
    }
}