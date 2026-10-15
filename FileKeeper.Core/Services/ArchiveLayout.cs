using System.IO.Compression;
using System.Text.Json;
using FileKeeper.Core.Models;

namespace FileKeeper.Core.Services;

/// <summary>
/// Layout of the backup zip:
/// <code>
/// snapshots/1/manifest.json        full file list of snapshot 1
/// snapshots/1/files/&lt;path&gt;     content stored by snapshot 1
/// snapshots/2/manifest.json
/// snapshots/2/files/&lt;path&gt;     only new / modified files
/// </code>
/// </summary>
internal static class ArchiveLayout
{
    private const string Root = "snapshots/";
    private const string ManifestName = "manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly HashSet<string> AlreadyCompressed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".7z", ".rar", ".gz", ".bz2", ".xz", ".zst", ".jpg", ".jpeg", ".png", ".gif", ".webp",
        ".heic", ".mp3", ".aac", ".ogg", ".flac", ".mp4", ".mkv", ".avi", ".mov", ".webm", ".docx",
        ".xlsx", ".pptx", ".odt", ".ods", ".jar", ".apk", ".pdf",
    };

    public static string SnapshotPrefix(int id) => $"{Root}{id}/";

    public static string ManifestPath(int id) => SnapshotPrefix(id) + ManifestName;

    public static string BlobPath(int id, string path) => SnapshotPrefix(id) + "files/" + path;

    public static CompressionLevel LevelFor(string path) =>
        AlreadyCompressed.Contains(Path.GetExtension(path)) ? CompressionLevel.NoCompression : CompressionLevel.Optimal;

    public static List<SnapshotManifest> ReadManifests(ZipArchive archive)
    {
        var manifests = new List<SnapshotManifest>();
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith(Root, StringComparison.Ordinal) ||
                !entry.FullName.EndsWith("/" + ManifestName, StringComparison.Ordinal) ||
                entry.FullName.Count(c => c == '/') != 2)
                continue;

            using var stream = entry.Open();
            var manifest = JsonSerializer.Deserialize<SnapshotManifest>(stream, JsonOptions);
            if (manifest is not null)
                manifests.Add(manifest);
        }

        manifests.Sort((a, b) => a.Id.CompareTo(b.Id));
        return manifests;
    }

    public static void WriteManifest(ZipArchive archive, SnapshotManifest manifest)
    {
        var path = ManifestPath(manifest.Id);
        archive.GetEntry(path)?.Delete();
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, manifest, JsonOptions);
    }

    public static void DeleteSnapshotEntries(ZipArchive archive, int id)
    {
        var prefix = SnapshotPrefix(id);
        foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            entry.Delete();
    }

    /// <summary>Renames an entry (zip has no rename, so the content is copied and the original deleted).</summary>
    public static void MoveEntry(ZipArchive archive, string from, string to)
    {
        var source = archive.GetEntry(from) ?? throw new InvalidDataException($"Archive entry not found: {from}");
        archive.GetEntry(to)?.Delete();
        var target = archive.CreateEntry(to, LevelFor(to));
        target.LastWriteTime = source.LastWriteTime;
        using (var input = source.Open())
        using (var output = target.Open())
            input.CopyTo(output);
        source.Delete();
    }

    /// <summary>Zip timestamps only support 1980-2107.</summary>
    public static DateTimeOffset ZipTimestamp(DateTime utc)
    {
        var min = new DateTime(1980, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var max = new DateTime(2107, 12, 30, 0, 0, 0, DateTimeKind.Utc);
        var clamped = utc < min ? min : utc > max ? max : utc;
        return new DateTimeOffset(DateTime.SpecifyKind(clamped, DateTimeKind.Utc));
    }
}
