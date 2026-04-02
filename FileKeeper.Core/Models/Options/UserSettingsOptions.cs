using FileKeeper.Core.Application;

namespace FileKeeper.Core.Models.Options;

public class UserSettingsOptions
{
    public const string SectionName = "FileKeeper";

    public string[] SourceDirectories { get; set; } = [];
    public string StorageDirectory { get; set; } = SetInitialStorageDirectory();
    public int VersionsToKeep { get; set; }
    public int MaxMbToKeep { get; set; }

    private static string SetInitialStorageDirectory()
    {
        var paths = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FileKeeper",
            "storage"
        };

        if (ApplicationInfo.IsDebug)
            paths.Insert(2, "debug");
        
        return Path.Combine(paths.ToArray());
    }
}