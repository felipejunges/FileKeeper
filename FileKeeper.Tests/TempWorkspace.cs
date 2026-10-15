using FileKeeper.Core.Models;

namespace FileKeeper.Tests;

/// <summary>Disposable temp folder with a source tree and a destination zip.</summary>
public sealed class TempWorkspace : IDisposable
{
    private static readonly DateTime BaseTime = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private int _tick;

    public TempWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "FileKeeperTests", Guid.NewGuid().ToString("N"));
        Source = Path.Combine(Root, "source");
        Directory.CreateDirectory(Source);
        Destination = Path.Combine(Root, "backup", "backup.zip");
    }

    public string Root { get; }
    public string Source { get; }
    public string Destination { get; }

    public BackupSettings Settings(long? maxSize = null, params string[] ignored) => new()
    {
        SourceFolders = [Source],
        DestinationFile = Destination,
        IgnoredFolders = [.. ignored],
        MaxBackupSizeBytes = maxSize,
    };

    /// <summary>Writes a file with a strictly increasing modification time.</summary>
    public string Write(string relative, string content)
    {
        var path = Path.Combine(Source, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, BaseTime.AddMinutes(++_tick));
        return path;
    }

    public void Touch(string relative) =>
        File.SetLastWriteTimeUtc(Path.Combine(Source, relative), BaseTime.AddMinutes(++_tick));

    public void Delete(string relative) => File.Delete(Path.Combine(Source, relative));

    /// <summary>Archive path of a file in <see cref="Source"/>.</summary>
    public string Key(string relative) =>
        Core.Services.FileScanner.SourceKey(Source) + "/" + relative.Replace('\\', '/');

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
