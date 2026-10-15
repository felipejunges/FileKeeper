using System.Text.Json;
using FileKeeper.Core.Models;

namespace FileKeeper.Core.Services;

/// <summary>Persists <see cref="BackupSettings"/> as JSON.</summary>
public sealed class SettingsStore(string filePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileKeeper", "settings.json");

    public string FilePath { get; } = filePath;

    public BackupSettings Load()
    {
        if (!File.Exists(FilePath))
            return new BackupSettings();

        using var stream = File.OpenRead(FilePath);
        return JsonSerializer.Deserialize<BackupSettings>(stream, JsonOptions) ?? new BackupSettings();
    }

    public void Save(BackupSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temp, FilePath, overwrite: true);
    }
}
