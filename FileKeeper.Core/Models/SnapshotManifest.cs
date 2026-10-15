namespace FileKeeper.Core.Models;

/// <summary>
/// One file as it existed when a snapshot was taken.
/// </summary>
/// <param name="Path">Archive path of the file ('/' separated, prefixed with its source folder key).</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="LastWriteTimeUtc">Last modification time of the original file.</param>
/// <param name="Sha256">Lowercase hex SHA-256 of the content.</param>
/// <param name="StoredIn">Id of the snapshot whose folder physically holds this content in the archive.</param>
public sealed record FileRecord(string Path, long Size, DateTime LastWriteTimeUtc, string Sha256, int StoredIn);

/// <summary>
/// Describes the full state of the source folders at the moment the snapshot was taken.
/// Only new or modified files are physically stored inside the snapshot folder; unchanged
/// files point (through <see cref="FileRecord.StoredIn"/>) to an older snapshot.
/// </summary>
public sealed class SnapshotManifest
{
    public int Id { get; set; }

    public DateTime CreatedUtc { get; set; }

    public List<string> SourceFolders { get; set; } = [];

    public List<FileRecord> Files { get; set; } = [];

    /// <summary>Ids of older snapshots that were pruned and merged into this one.</summary>
    public List<int> MergedSnapshotIds { get; set; } = [];

    public long TotalSize => Files.Sum(f => f.Size);

    public IEnumerable<FileRecord> StoredFiles => Files.Where(f => f.StoredIn == Id);
}
