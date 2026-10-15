namespace FileKeeper.Core.Models;

public sealed class BackupResult
{
    /// <summary>The created snapshot, or null when nothing changed since the last one.</summary>
    public SnapshotManifest? Snapshot { get; init; }

    public SnapshotDiff? Diff { get; init; }

    public int StoredFileCount { get; init; }

    public long StoredBytes { get; init; }

    /// <summary>Files or folders that could not be read.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>Snapshots removed by the automatic prune.</summary>
    public IReadOnlyList<int> PrunedSnapshotIds { get; init; } = [];

    public long ArchiveSize { get; init; }

    /// <summary>True when the archive is still above the configured limit (only one snapshot left).</summary>
    public bool ExceedsMaxSize { get; init; }
}
