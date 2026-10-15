using FileKeeper.Core;
using FileKeeper.Core.Models;
using FileKeeper.Core.Services;

namespace FileKeeper.Tests;

public class SnapshotDiffTests
{
    private static readonly DateTime T = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static SnapshotManifest Snapshot(int id, params FileRecord[] files) => new() { Id = id, Files = [.. files] };

    [Fact]
    public void FirstSnapshot_EverythingIsAdded()
    {
        var diff = SnapshotDiff.Compute(null, Snapshot(1, new FileRecord("a", 1, T, "h1", 1)));

        Assert.Equal(ChangeKind.Added, Assert.Single(diff.Changes).Kind);
    }

    [Fact]
    public void DetectsAddedModifiedRemoved_AndIgnoresMetadataOnlyChanges()
    {
        var previous = Snapshot(1,
            new FileRecord("same", 1, T, "h", 1),
            new FileRecord("touched", 1, T, "h", 1),
            new FileRecord("modified", 1, T, "h1", 1),
            new FileRecord("removed", 1, T, "h", 1));
        var current = Snapshot(2,
            new FileRecord("same", 1, T, "h", 1),
            new FileRecord("touched", 1, T.AddDays(1), "h", 1),
            new FileRecord("modified", 2, T, "h2", 2),
            new FileRecord("added", 1, T, "h", 2));

        var diff = SnapshotDiff.Compute(previous, current);

        Assert.Equal(
            [("added", ChangeKind.Added), ("modified", ChangeKind.Modified), ("removed", ChangeKind.Removed)],
            diff.Changes.Select(c => (c.Path, c.Kind)));
    }
}

public class FileHistoryTests
{
    private static readonly DateTime T = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GroupsSnapshotsByStoredVersion()
    {
        var snapshots = new List<SnapshotManifest>
        {
            new() { Id = 1, Files = [new FileRecord("a", 1, T, "h1", 1)] },
            new() { Id = 2, Files = [new FileRecord("a", 1, T.AddHours(1), "h1", 1)] },
            new() { Id = 3, Files = [new FileRecord("a", 2, T, "h2", 3), new FileRecord("b", 1, T, "x", 3)] },
            new() { Id = 4, Files = [new FileRecord("b", 1, T, "x", 3)] },
        };

        var histories = FileHistory.Build(snapshots);

        var a = histories.Single(h => h.Path == "a");
        Assert.False(a.ExistsInLatest);
        Assert.Equal(2, a.Versions.Count);
        Assert.Equal([1, 2], a.Versions[0].SnapshotIds);
        Assert.Equal(T.AddHours(1), a.Versions[0].LastWriteTimeUtc);
        Assert.Equal([3], a.Versions[1].SnapshotIds);

        var b = histories.Single(h => h.Path == "b");
        Assert.True(b.ExistsInLatest);
        Assert.Equal([3, 4], Assert.Single(b.Versions).SnapshotIds);
    }
}

public class FileScannerTests
{
    [Theory]
    [InlineData("/home/me/Docs", "home/me/Docs")]
    [InlineData("/home/me/Docs/", "home/me/Docs")]
    [InlineData("/", "root")]
    public void SourceKey_IsFullPathWithoutRoot(string folder, string expected)
    {
        if (OperatingSystem.IsWindows())
            return;
        Assert.Equal(expected, FileScanner.SourceKey(folder));
    }

    [Fact]
    public void IgnoreRules_MatchNamesAndPathPrefixes()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fk-scan"));
        var scanner = new FileScanner(["bin", Path.Combine(root, "private")]);

        Assert.True(scanner.IsIgnored(Path.Combine(root, "project", "bin")));
        Assert.True(scanner.IsIgnored(Path.Combine(root, "private")));
        Assert.True(scanner.IsIgnored(Path.Combine(root, "private", "deep")));
        Assert.False(scanner.IsIgnored(Path.Combine(root, "private2")));
        Assert.False(scanner.IsIgnored(Path.Combine(root, "binary")));
    }

    [Fact]
    public void MissingSource_IsReportedAsError()
    {
        var scanner = new FileScanner([]);
        var files = scanner.Scan([Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())]).ToList();

        Assert.Empty(files);
        Assert.Single(scanner.Errors);
    }
}

public class FormattingTests
{
    [Theory]
    [InlineData(new[] { 1, 2, 3, 5 }, "#1–#3, #5")]
    [InlineData(new[] { 4 }, "#4")]
    [InlineData(new int[0], "")]
    [InlineData(new[] { 7, 6, 9, 8 }, "#6–#9")]
    public void SnapshotRanges(int[] ids, string expected) =>
        Assert.Equal(expected, Formatting.SnapshotRanges(ids));

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(5L * 1024 * 1024, "5 MB")]
    public void Bytes(long value, string expected) =>
        Assert.Equal(expected, Formatting.Bytes(value).Replace(',', '.'));
}

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "FileKeeperTests", Guid.NewGuid() + ".json");

    public void Dispose() => File.Delete(_file);

    [Fact]
    public void MissingFile_ReturnsDefaults()
    {
        var settings = new SettingsStore(_file).Load();

        Assert.Empty(settings.SourceFolders);
        Assert.Null(settings.MaxBackupSizeBytes);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var store = new SettingsStore(_file);
        store.Save(new BackupSettings
        {
            SourceFolders = ["/a", "/b"],
            DestinationFile = "/backup.zip",
            IgnoredFolders = ["node_modules"],
            MaxBackupSizeBytes = 1234,
        });

        var loaded = store.Load();

        Assert.Equal(["/a", "/b"], loaded.SourceFolders);
        Assert.Equal("/backup.zip", loaded.DestinationFile);
        Assert.Equal(["node_modules"], loaded.IgnoredFolders);
        Assert.Equal(1234, loaded.MaxBackupSizeBytes);
    }
}

public class PathDisplayTests
{
    [Fact]
    public void ReplacesSourceKeyWithFolderName_UnlessAmbiguous()
    {
        if (OperatingSystem.IsWindows())
            return;
        var display = new PathDisplay(["/home/me/Documents", "/a/Photos", "/b/Photos"]);

        Assert.Equal("Documents/x/y.txt", display.Shorten("home/me/Documents/x/y.txt"));
        Assert.Equal("a/Photos/p.jpg", display.Shorten("a/Photos/p.jpg"));
        Assert.Equal("other/file", display.Shorten("other/file"));
        Assert.Equal("home/me/Documents2/z", display.Shorten("home/me/Documents2/z"));
    }
}
