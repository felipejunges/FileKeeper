namespace FileKeeper.Core.Models.Options;

public class UserSettingsOptions
{
    public const string SectionName = "FileKeeper";

    public string[] SourceDirectories { get; set; } = [];
    public string StorageDirectory { get; set; } = string.Empty;
    public int VersionsToKeep { get; set; }
    public int MaxMbToKeep { get; set; }
}