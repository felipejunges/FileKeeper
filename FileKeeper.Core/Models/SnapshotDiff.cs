namespace FileKeeper.Core.Models;

public enum ChangeKind
{
    Added,
    Modified,
    Removed,
}

/// <summary>A single file difference between a snapshot and its predecessor.</summary>
public sealed record FileChange(ChangeKind Kind, string Path, FileRecord? Before, FileRecord? After)
{
    public long Size => (After ?? Before)?.Size ?? 0;
}

public sealed class SnapshotDiff
{
    public SnapshotDiff(IReadOnlyList<FileChange> changes) => Changes = changes;

    public IReadOnlyList<FileChange> Changes { get; }

    public int AddedCount => Changes.Count(c => c.Kind == ChangeKind.Added);
    public int ModifiedCount => Changes.Count(c => c.Kind == ChangeKind.Modified);
    public int RemovedCount => Changes.Count(c => c.Kind == ChangeKind.Removed);

    /// <summary>
    /// Compares <paramref name="current"/> with <paramref name="previous"/> (null for the first snapshot).
    /// A file counts as modified only when its content changed (it was stored again).
    /// </summary>
    public static SnapshotDiff Compute(SnapshotManifest? previous, SnapshotManifest current)
    {
        var before = previous?.Files.ToDictionary(f => f.Path, StringComparer.Ordinal)
                     ?? new Dictionary<string, FileRecord>(StringComparer.Ordinal);
        var changes = new List<FileChange>();

        foreach (var file in current.Files)
        {
            if (!before.Remove(file.Path, out var old))
                changes.Add(new FileChange(ChangeKind.Added, file.Path, null, file));
            else if (old.StoredIn != file.StoredIn || old.Sha256 != file.Sha256)
                changes.Add(new FileChange(ChangeKind.Modified, file.Path, old, file));
        }

        changes.AddRange(before.Values.Select(old => new FileChange(ChangeKind.Removed, old.Path, old, null)));
        changes.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        return new SnapshotDiff(changes);
    }
}
