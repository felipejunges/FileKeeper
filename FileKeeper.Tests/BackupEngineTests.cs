using System.IO.Compression;
using FileKeeper.Core.Models;
using FileKeeper.Core.Services;

namespace FileKeeper.Tests;

public sealed class BackupEngineTests : IDisposable
{
    private readonly TempWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private BackupEngine Engine(long? maxSize = null, params string[] ignored) =>
        new(_ws.Settings(maxSize, ignored));

    private List<string> ZipEntries()
    {
        using var zip = ZipFile.OpenRead(_ws.Destination);
        return zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ToList();
    }

    private string RestoreToString(BackupEngine engine, int snapshotId, string relative)
    {
        var target = Path.Combine(_ws.Root, "restore-" + Guid.NewGuid().ToString("N"));
        engine.RestoreSnapshot(snapshotId, target);
        return File.ReadAllText(Path.Combine(target, _ws.Key(relative)));
    }

    [Fact]
    public void FirstSnapshot_StoresAllFiles()
    {
        _ws.Write("a.txt", "A");
        _ws.Write("sub/b.txt", "B");

        var result = Engine().CreateSnapshot();

        Assert.NotNull(result.Snapshot);
        Assert.Equal(1, result.Snapshot.Id);
        Assert.Equal(2, result.StoredFileCount);
        Assert.Equal(2, result.Diff!.AddedCount);
        Assert.Contains(ArchiveLayout.BlobPath(1, _ws.Key("a.txt")), ZipEntries());
        Assert.Contains(ArchiveLayout.BlobPath(1, _ws.Key("sub/b.txt")), ZipEntries());
        Assert.Contains(ArchiveLayout.ManifestPath(1), ZipEntries());
    }

    [Fact]
    public void NoChanges_DoesNotCreateSnapshot()
    {
        _ws.Write("a.txt", "A");
        var engine = Engine();
        engine.CreateSnapshot();

        var result = engine.CreateSnapshot();

        Assert.Null(result.Snapshot);
        Assert.Single(engine.LoadSnapshots());
    }

    [Fact]
    public void SecondSnapshot_StoresOnlyNewAndModifiedFiles()
    {
        _ws.Write("unchanged.txt", "same");
        _ws.Write("changed.txt", "v1");
        var engine = Engine();
        engine.CreateSnapshot();

        _ws.Write("changed.txt", "v2");
        _ws.Write("new.txt", "new");
        var result = engine.CreateSnapshot();

        Assert.Equal(2, result.StoredFileCount);
        var snapshot = result.Snapshot!;
        Assert.Equal(1, snapshot.Files.Single(f => f.Path == _ws.Key("unchanged.txt")).StoredIn);
        Assert.Equal(2, snapshot.Files.Single(f => f.Path == _ws.Key("changed.txt")).StoredIn);
        Assert.Equal(2, snapshot.Files.Single(f => f.Path == _ws.Key("new.txt")).StoredIn);
        Assert.DoesNotContain(ArchiveLayout.BlobPath(2, _ws.Key("unchanged.txt")), ZipEntries());

        Assert.Equal(1, result.Diff!.AddedCount);
        Assert.Equal(1, result.Diff.ModifiedCount);
        Assert.Equal(0, result.Diff.RemovedCount);
    }

    [Fact]
    public void TouchedFileWithSameContent_IsNotStoredAgain()
    {
        _ws.Write("a.txt", "A");
        _ws.Write("b.txt", "B");
        var engine = Engine();
        engine.CreateSnapshot();

        _ws.Touch("a.txt");
        _ws.Write("b.txt", "B2");
        var result = engine.CreateSnapshot();

        var a = result.Snapshot!.Files.Single(f => f.Path == _ws.Key("a.txt"));
        Assert.Equal(1, a.StoredIn);
        Assert.Equal(File.GetLastWriteTimeUtc(Path.Combine(_ws.Source, "a.txt")), a.LastWriteTimeUtc);
        Assert.Equal(1, result.StoredFileCount);
        Assert.Equal(1, result.Diff!.ModifiedCount);
    }

    [Fact]
    public void RemovedFile_AppearsInDiff()
    {
        _ws.Write("a.txt", "A");
        _ws.Write("b.txt", "B");
        var engine = Engine();
        engine.CreateSnapshot();

        _ws.Delete("b.txt");
        var result = engine.CreateSnapshot();

        var change = Assert.Single(result.Diff!.Changes);
        Assert.Equal(ChangeKind.Removed, change.Kind);
        Assert.Equal(_ws.Key("b.txt"), change.Path);
        Assert.Equal(0, result.StoredFileCount);
    }

    [Fact]
    public void IgnoredFolders_ByNameAndByPath_AreSkipped()
    {
        _ws.Write("keep/a.txt", "A");
        _ws.Write("node_modules/x.js", "x");
        _ws.Write("deep/node_modules/y.js", "y");
        _ws.Write("private/secret.txt", "s");

        var result = Engine(null, "node_modules", Path.Combine(_ws.Source, "private")).CreateSnapshot();

        var paths = result.Snapshot!.Files.Select(f => f.Path).ToList();
        Assert.Equal([_ws.Key("keep/a.txt")], paths);
    }

    [Fact]
    public void FileWithNonUtf8Name_IsSkippedAndReported()
    {
        if (!OperatingSystem.IsLinux())
            return;
        _ws.Write("ok.txt", "fine");
        // "Instru\xe7oes.txt" in Latin-1: not valid UTF-8, so .NET cannot reopen it by name.
        using (var touch = System.Diagnostics.Process.Start("/bin/sh",
                   ["-c", "printf x > \"$1/Instru$(printf '\\347')oes.txt\"", "sh", _ws.Source]))
            touch.WaitForExit();

        var result = Engine().CreateSnapshot();

        Assert.Equal([_ws.Key("ok.txt")], result.Snapshot!.Files.Select(f => f.Path));
        Assert.Contains(result.Errors, e => e.Contains("invalid characters"));
    }

    [Fact]
    public void DestinationInsideSource_IsNotBackedUp()
    {
        _ws.Write("a.txt", "A");
        var settings = _ws.Settings();
        settings.DestinationFile = Path.Combine(_ws.Source, "backup.zip");
        var engine = new BackupEngine(settings);

        engine.CreateSnapshot();
        var second = engine.CreateSnapshot();

        Assert.Null(second.Snapshot);
        Assert.Single(engine.LoadSnapshots().Single().Files);
    }

    [Fact]
    public void DeleteLatest_RemovesOnlyLatestSnapshot()
    {
        _ws.Write("a.txt", "A");
        var engine = Engine();
        engine.CreateSnapshot();
        _ws.Write("a.txt", "A2");
        engine.CreateSnapshot();

        var deleted = engine.DeleteLatestSnapshot();

        Assert.Equal(2, deleted.Id);
        Assert.Equal([1], engine.LoadSnapshots().Select(s => s.Id));
        Assert.DoesNotContain(ZipEntries(), e => e.StartsWith(ArchiveLayout.SnapshotPrefix(2)));
        Assert.Equal("A", RestoreToString(engine, 1, "a.txt"));

        // The change is detected again against snapshot 1.
        var again = engine.CreateSnapshot();
        Assert.Equal(2, again.Snapshot!.Id);
        Assert.Equal(1, again.StoredFileCount);
    }

    [Fact]
    public void WithoutDestination_ReadsAsEmptyBackup()
    {
        var engine = new BackupEngine(new BackupSettings());

        Assert.Empty(engine.LoadSnapshots());
        Assert.Equal(0, engine.GetArchiveSize());
        Assert.Throws<InvalidOperationException>(() => engine.DeleteLatestSnapshot());
    }

    [Fact]
    public void DeleteLatest_WithoutSnapshots_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Engine().DeleteLatestSnapshot());
    }

    [Fact]
    public void PruneOldest_MergesStillReferencedFilesIntoNextSnapshot()
    {
        _ws.Write("kept.txt", "kept since snapshot 1");
        _ws.Write("changed.txt", "old");
        _ws.Write("removed.txt", "gone later");
        var engine = Engine();
        engine.CreateSnapshot(); // #1

        _ws.Write("changed.txt", "new");
        _ws.Delete("removed.txt");
        engine.CreateSnapshot(); // #2

        _ws.Write("other.txt", "other");
        engine.CreateSnapshot(); // #3, still references kept.txt in #1

        var pruned = engine.PruneOldestSnapshot();

        Assert.Equal(1, pruned);
        var snapshots = engine.LoadSnapshots();
        Assert.Equal([2, 3], snapshots.Select(s => s.Id));
        Assert.All(snapshots.SelectMany(s => s.Files), f => Assert.NotEqual(1, f.StoredIn));
        Assert.Equal([1], snapshots[0].MergedSnapshotIds);

        var entries = ZipEntries();
        Assert.DoesNotContain(entries, e => e.StartsWith(ArchiveLayout.SnapshotPrefix(1)));
        Assert.Contains(ArchiveLayout.BlobPath(2, _ws.Key("kept.txt")), entries);
        Assert.DoesNotContain(entries, e => e.EndsWith("removed.txt"));

        Assert.Equal("kept since snapshot 1", RestoreToString(engine, 2, "kept.txt"));
        Assert.Equal("kept since snapshot 1", RestoreToString(engine, 3, "kept.txt"));
        Assert.Equal("new", RestoreToString(engine, 3, "changed.txt"));
    }

    [Fact]
    public void PruneOldest_TwiceKeepsMergeHistory()
    {
        _ws.Write("a.txt", "A");
        var engine = Engine();
        engine.CreateSnapshot();
        _ws.Write("b.txt", "B");
        engine.CreateSnapshot();
        _ws.Write("c.txt", "C");
        engine.CreateSnapshot();

        engine.PruneOldestSnapshot();
        engine.PruneOldestSnapshot();

        var only = Assert.Single(engine.LoadSnapshots());
        Assert.Equal(3, only.Id);
        Assert.Equal([1, 2], only.MergedSnapshotIds);
        Assert.All(only.Files, f => Assert.Equal(3, f.StoredIn));
        Assert.Equal("A", RestoreToString(engine, 3, "a.txt"));
    }

    [Fact]
    public void PruneOldest_WithSingleSnapshot_Throws()
    {
        _ws.Write("a.txt", "A");
        var engine = Engine();
        engine.CreateSnapshot();

        Assert.Throws<InvalidOperationException>(() => engine.PruneOldestSnapshot());
    }

    [Fact]
    public void ExceedingMaxSize_AutoPrunesOldestSnapshots()
    {
        var random = new Random(42);
        string Noise() => Convert.ToBase64String(Enumerable.Range(0, 30_000).Select(_ => (byte)random.Next()).ToArray());

        _ws.Write("big.txt", Noise());
        Engine().CreateSnapshot();
        _ws.Write("big.txt", Noise());
        Engine().CreateSnapshot();
        var sizeWithTwo = new FileInfo(_ws.Destination).Length;

        // Limit allows roughly two versions of the file, not three.
        _ws.Write("big.txt", Noise());
        var result = Engine(maxSize: sizeWithTwo + 1000).CreateSnapshot();

        Assert.Equal([1], result.PrunedSnapshotIds);
        Assert.False(result.ExceedsMaxSize);
        Assert.True(result.ArchiveSize <= sizeWithTwo + 1000);
        Assert.Equal([2, 3], Engine().LoadSnapshots().Select(s => s.Id));
    }

    [Fact]
    public void ExceedingMaxSize_KeepsAtLeastTheLatestSnapshot()
    {
        _ws.Write("a.txt", new string('x', 10_000));
        var engine = Engine(maxSize: 10);
        engine.CreateSnapshot();
        _ws.Write("a.txt", new string('y', 10_000));

        var result = engine.CreateSnapshot();

        Assert.Equal([1], result.PrunedSnapshotIds);
        Assert.True(result.ExceedsMaxSize);
        Assert.Single(engine.LoadSnapshots());
    }

    [Fact]
    public void ExtractFile_ReturnsRequestedVersion()
    {
        _ws.Write("a.txt", "v1");
        var engine = Engine();
        engine.CreateSnapshot();
        _ws.Write("a.txt", "v2");
        engine.CreateSnapshot();

        var target = Path.Combine(_ws.Root, "out.txt");
        engine.ExtractFile(_ws.Key("a.txt"), 1, target);

        Assert.Equal("v1", File.ReadAllText(target));
    }

    [Fact]
    public void Restore_RecreatesWholeTreeWithTimestamps()
    {
        var original = _ws.Write("dir/a.txt", "A");
        _ws.Write("b.txt", "B");
        var engine = Engine();
        engine.CreateSnapshot();

        var target = Path.Combine(_ws.Root, "restore");
        var count = engine.RestoreSnapshot(1, target);

        Assert.Equal(2, count);
        var restored = Path.Combine(target, _ws.Key("dir/a.txt"));
        Assert.Equal("A", File.ReadAllText(restored));
        Assert.Equal(File.GetLastWriteTimeUtc(original), File.GetLastWriteTimeUtc(restored));
    }

    [Fact]
    public void FailedSnapshot_LeavesArchiveUntouched()
    {
        _ws.Write("a.txt", "A");
        var engine = Engine();
        engine.CreateSnapshot();
        _ws.Write("b.txt", "B");

        using var cts = new CancellationTokenSource();
        var progress = new SyncProgress(msg =>
        {
            if (msg.StartsWith("Storing"))
                cts.Cancel();
        });

        Assert.ThrowsAny<OperationCanceledException>(() => engine.CreateSnapshot(progress, cts.Token));
        Assert.Equal([1], engine.LoadSnapshots().Select(s => s.Id));
        Assert.DoesNotContain(ZipEntries(), e => e.StartsWith(ArchiveLayout.SnapshotPrefix(2)));
    }

    [Fact]
    public void RestoreFiles_WritesChosenVersionsToTargetPaths()
    {
        _ws.Write("docs/a.txt", "old");
        _ws.Write("docs/sub/b.txt", "B");
        var engine = Engine();
        engine.CreateSnapshot();
        _ws.Write("docs/a.txt", "new");
        engine.CreateSnapshot();
        _ws.Delete("docs/sub/b.txt");
        engine.CreateSnapshot();

        var target = Path.Combine(_ws.Root, "restore");
        var count = engine.RestoreFiles(
        [
            new RestoreItem(_ws.Key("docs/a.txt"), 2, "docs/a.txt"),
            new RestoreItem(_ws.Key("docs/sub/b.txt"), 1, "docs/sub/b.txt"),
        ], target);

        Assert.Equal(2, count);
        Assert.Equal("new", File.ReadAllText(Path.Combine(target, "docs", "a.txt")));
        Assert.Equal("B", File.ReadAllText(Path.Combine(target, "docs", "sub", "b.txt")));
    }

    [Fact]
    public void RestoreFiles_RejectsPathsOutsideTarget()
    {
        _ws.Write("a.txt", "A");
        var engine = Engine();
        engine.CreateSnapshot();

        var target = Path.Combine(_ws.Root, "restore");
        Assert.Throws<InvalidDataException>(() =>
            engine.RestoreFiles([new RestoreItem(_ws.Key("a.txt"), 1, "../escaped.txt")], target));
        Assert.False(File.Exists(Path.Combine(_ws.Root, "escaped.txt")));
    }

    [Fact]
    public void RestoreFiles_UnknownVersion_Throws()
    {
        _ws.Write("a.txt", "A");
        var engine = Engine();
        engine.CreateSnapshot();

        Assert.Throws<InvalidOperationException>(() =>
            engine.RestoreFiles([new RestoreItem(_ws.Key("a.txt"), 7, "a.txt")], Path.Combine(_ws.Root, "restore")));
    }

    private sealed class SyncProgress(Action<string> handler) : IProgress<string>
    {
        public void Report(string value) => handler(value);
    }
}
