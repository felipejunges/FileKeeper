using System.IO.Compression;
using System.Security.Cryptography;
using FileKeeper.Core.Models;

namespace FileKeeper.Core.Services;

/// <summary>
/// Creates, deletes, prunes and restores snapshots stored in a single zip file.
/// Not thread safe: callers must serialize operations on the same destination file.
/// </summary>
public sealed class BackupEngine
{
    private readonly BackupSettings _settings;
    private readonly Func<DateTime> _utcNow;

    public BackupEngine(BackupSettings settings, Func<DateTime>? utcNow = null)
    {
        _settings = settings.Clone();
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    private bool HasDestination => !string.IsNullOrWhiteSpace(_settings.DestinationFile);

    private string Destination => HasDestination
        ? Path.GetFullPath(_settings.DestinationFile)
        : throw new InvalidOperationException("No destination file configured.");

    public IReadOnlyList<SnapshotManifest> LoadSnapshots()
    {
        if (!HasDestination || !File.Exists(Destination))
            return [];

        using var archive = ZipFile.OpenRead(Destination);
        return ArchiveLayout.ReadManifests(archive);
    }

    public long GetArchiveSize() => HasDestination && File.Exists(Destination) ? new FileInfo(Destination).Length : 0;

    /// <summary>
    /// Scans the sources and creates a new snapshot holding only new or modified files.
    /// Returns a result without snapshot when nothing changed. Afterwards the max size is enforced.
    /// </summary>
    public BackupResult CreateSnapshot(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (_settings.SourceFolders.Count == 0)
            throw new InvalidOperationException("No source folder configured.");
        if (string.IsNullOrWhiteSpace(_settings.DestinationFile))
            throw new InvalidOperationException("No destination file configured.");

        progress?.Report("Reading existing snapshots...");
        var previous = LoadSnapshots().LastOrDefault();
        var previousFiles = previous?.Files.ToDictionary(f => f.Path, StringComparer.Ordinal)
                            ?? new Dictionary<string, FileRecord>(StringComparer.Ordinal);
        var newId = (previous?.Id ?? 0) + 1;

        progress?.Report("Scanning source folders...");
        var scanner = new FileScanner(_settings.IgnoredFolders, [Destination]);
        var scanned = scanner.Scan(_settings.SourceFolders, ct).ToList();
        var errors = new List<string>(scanner.Errors);

        // Decide which files must be stored again.
        var records = new List<FileRecord>(scanned.Count);
        var toStore = new List<ScannedFile>();
        for (var i = 0; i < scanned.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var file = scanned[i];
            if (i % 200 == 0)
                progress?.Report($"Checking files ({i}/{scanned.Count})...");

            if (!previousFiles.TryGetValue(file.ArchivePath, out var old))
            {
                toStore.Add(file);
                continue;
            }

            if (old.Size == file.Size && old.LastWriteTimeUtc == file.LastWriteTimeUtc)
            {
                records.Add(old);
                continue;
            }

            string hash;
            try
            {
                hash = ComputeHash(file.FullPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Keep the last known version rather than reporting the file as removed.
                errors.Add($"Cannot read {file.FullPath}: {ex.Message}");
                records.Add(old);
                continue;
            }

            if (hash == old.Sha256)
                records.Add(old with { Size = file.Size, LastWriteTimeUtc = file.LastWriteTimeUtc });
            else
                toStore.Add(file);
        }

        if (toStore.Count == 0 && SameFiles(previous?.Files ?? [], records))
        {
            progress?.Report("No changes since the last snapshot.");
            return new BackupResult { Errors = errors, ArchiveSize = GetArchiveSize() };
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Destination)!);
        long storedBytes = 0;
        var storedCount = 0;
        SnapshotManifest manifest;

        using (var stream = new FileStream(Destination, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update))
        {
            // Leftovers of an interrupted run would collide with the new snapshot.
            ArchiveLayout.DeleteSnapshotEntries(archive, newId);
            try
            {
                for (var i = 0; i < toStore.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var file = toStore[i];
                    progress?.Report($"Storing {i + 1}/{toStore.Count}: {file.ArchivePath}");
                    var blobPath = ArchiveLayout.BlobPath(newId, file.ArchivePath);
                    try
                    {
                        // The hash is taken from the bytes actually stored.
                        var (hash, size) = StoreFile(archive, file, blobPath);
                        records.Add(new FileRecord(file.ArchivePath, size, file.LastWriteTimeUtc, hash, newId));
                        storedBytes += size;
                        storedCount++;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        archive.GetEntry(blobPath)?.Delete();
                        errors.Add($"Cannot read {file.FullPath}: {ex.Message}");
                        if (previousFiles.TryGetValue(file.ArchivePath, out var old))
                            records.Add(old);
                    }
                }

                ct.ThrowIfCancellationRequested();
                records.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
                manifest = new SnapshotManifest
                {
                    Id = newId,
                    CreatedUtc = _utcNow(),
                    SourceFolders = [.. _settings.SourceFolders],
                    Files = records,
                };
                ArchiveLayout.WriteManifest(archive, manifest);
            }
            catch
            {
                // Never leave a half written snapshot behind.
                ArchiveLayout.DeleteSnapshotEntries(archive, newId);
                throw;
            }

            progress?.Report("Writing archive...");
        }

        var diff = SnapshotDiff.Compute(previous, manifest);
        var pruned = EnforceMaxSize(progress);
        var archiveSize = GetArchiveSize();
        return new BackupResult
        {
            Snapshot = manifest,
            Diff = diff,
            StoredFileCount = storedCount,
            StoredBytes = storedBytes,
            Errors = errors,
            PrunedSnapshotIds = pruned,
            ArchiveSize = archiveSize,
            ExceedsMaxSize = _settings.MaxBackupSizeBytes is { } max && archiveSize > max,
        };
    }

    /// <summary>Deletes the most recent snapshot. Older snapshots never depend on it.</summary>
    public SnapshotManifest DeleteLatestSnapshot()
    {
        var latest = LoadSnapshots().LastOrDefault()
                     ?? throw new InvalidOperationException("There is no snapshot to delete.");

        using (var archive = ZipFile.Open(Destination, ZipArchiveMode.Update))
            ArchiveLayout.DeleteSnapshotEntries(archive, latest.Id);

        return latest;
    }

    /// <summary>
    /// Removes the oldest snapshot. Files it stored that are still referenced by the next snapshot
    /// are moved into the next snapshot, so every remaining snapshot stays complete.
    /// </summary>
    public int PruneOldestSnapshot()
    {
        var snapshots = LoadSnapshots();
        if (snapshots.Count < 2)
            throw new InvalidOperationException("At least two snapshots are required to prune the oldest one.");

        var first = snapshots[0];
        var next = snapshots[1];

        using (var archive = ZipFile.Open(Destination, ZipArchiveMode.Update))
        {
            foreach (var file in next.Files.Where(f => f.StoredIn == first.Id))
            {
                ArchiveLayout.MoveEntry(archive,
                    ArchiveLayout.BlobPath(first.Id, file.Path),
                    ArchiveLayout.BlobPath(next.Id, file.Path));
            }

            ArchiveLayout.DeleteSnapshotEntries(archive, first.Id);

            next.MergedSnapshotIds = [.. first.MergedSnapshotIds, first.Id, .. next.MergedSnapshotIds];
            foreach (var snapshot in snapshots.Skip(1))
            {
                var changed = snapshot == next;
                for (var i = 0; i < snapshot.Files.Count; i++)
                {
                    if (snapshot.Files[i].StoredIn != first.Id)
                        continue;
                    snapshot.Files[i] = snapshot.Files[i] with { StoredIn = next.Id };
                    changed = true;
                }

                if (changed)
                    ArchiveLayout.WriteManifest(archive, snapshot);
            }
        }

        return first.Id;
    }

    /// <summary>Prunes the oldest snapshots while the archive is larger than the configured limit.</summary>
    public IReadOnlyList<int> EnforceMaxSize(IProgress<string>? progress = null)
    {
        var pruned = new List<int>();
        if (_settings.MaxBackupSizeBytes is not { } max)
            return pruned;

        while (GetArchiveSize() > max && LoadSnapshots().Count > 1)
        {
            progress?.Report("Backup exceeds the maximum size, pruning the oldest snapshot...");
            pruned.Add(PruneOldestSnapshot());
        }

        return pruned;
    }

    /// <summary>Restores every file of a snapshot under <paramref name="targetFolder"/>.</summary>
    public int RestoreSnapshot(int snapshotId, string targetFolder, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var snapshot = LoadSnapshots().FirstOrDefault(s => s.Id == snapshotId)
                       ?? throw new InvalidOperationException($"Snapshot {snapshotId} not found.");
        var root = Path.GetFullPath(targetFolder);

        using var archive = ZipFile.OpenRead(Destination);
        var count = 0;
        foreach (var file in snapshot.Files)
        {
            ct.ThrowIfCancellationRequested();
            count++;
            progress?.Report($"Restoring {count}/{snapshot.Files.Count}: {file.Path}");
            Extract(archive, file, SafeTarget(root, file.Path));
        }

        return count;
    }

    /// <summary>Restores the given stored versions, each one under <paramref name="targetFolder"/>.</summary>
    public int RestoreFiles(IReadOnlyList<RestoreItem> items, string targetFolder, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var root = Path.GetFullPath(targetFolder);

        using var archive = ZipFile.OpenRead(Destination);
        var records = ArchiveLayout.ReadManifests(archive)
            .SelectMany(s => s.Files)
            .DistinctBy(f => (f.Path, f.StoredIn))
            .ToDictionary(f => (f.Path, f.StoredIn));

        var count = 0;
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            if (!records.TryGetValue((item.Path, item.StoredIn), out var record))
                throw new InvalidOperationException($"Version of {item.Path} stored in snapshot {item.StoredIn} not found.");
            count++;
            progress?.Report($"Restoring {count}/{items.Count}: {item.TargetPath}");
            Extract(archive, record, SafeTarget(root, item.TargetPath));
        }

        return count;
    }

    /// <summary>Extracts one stored version of a file.</summary>
    public void ExtractFile(string path, int storedIn, string targetFile)
    {
        using var archive = ZipFile.OpenRead(Destination);
        var record = LoadRecord(archive, path, storedIn);
        Extract(archive, record, Path.GetFullPath(targetFile));
    }

    private static FileRecord LoadRecord(ZipArchive archive, string path, int storedIn) =>
        ArchiveLayout.ReadManifests(archive)
            .SelectMany(s => s.Files)
            .FirstOrDefault(f => f.Path == path && f.StoredIn == storedIn)
        ?? throw new InvalidOperationException($"Version of {path} stored in snapshot {storedIn} not found.");

    /// <summary>Resolves <paramref name="relative"/> under <paramref name="root"/>, refusing paths that escape it.</summary>
    private static string SafeTarget(string root, string relative)
    {
        var target = Path.GetFullPath(Path.Combine(root, relative));
        if (!target.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsafe path in archive: {relative}");
        return target;
    }

    private static void Extract(ZipArchive archive, FileRecord file, string target)
    {
        var entry = archive.GetEntry(ArchiveLayout.BlobPath(file.StoredIn, file.Path))
                    ?? throw new InvalidDataException($"Missing content for {file.Path} in snapshot {file.StoredIn}.");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using (var input = entry.Open())
        using (var output = File.Create(target))
            input.CopyTo(output);
        File.SetLastWriteTimeUtc(target, file.LastWriteTimeUtc);
    }

    private static (string Hash, long Size) StoreFile(ZipArchive archive, ScannedFile file, string blobPath)
    {
        using var input = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var entry = archive.CreateEntry(blobPath, ArchiveLayout.LevelFor(blobPath));
        entry.LastWriteTime = ArchiveLayout.ZipTimestamp(file.LastWriteTimeUtc);

        using var output = entry.Open();
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long size = 0;
        int read;
        while ((read = input.Read(buffer)) > 0)
        {
            hasher.AppendData(buffer, 0, read);
            output.Write(buffer, 0, read);
            size += read;
        }

        return (Convert.ToHexStringLower(hasher.GetHashAndReset()), size);
    }

    private static string ComputeHash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static bool SameFiles(List<FileRecord> previous, List<FileRecord> current)
    {
        if (previous.Count != current.Count)
            return false;
        var set = previous.ToHashSet();
        return current.All(set.Contains);
    }
}
