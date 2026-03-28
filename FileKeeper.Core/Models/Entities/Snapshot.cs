using System.Text.Json.Serialization;

namespace FileKeeper.Core.Models.Entities;

public class Snapshot
{
    [JsonInclude]
    public Guid Id { get; private set; }

    [JsonInclude]
    public DateTime CreatedAtUtc { get; private set; }

    [JsonInclude]
    public ICollection<FileEntry> Files { get; private set; } = Array.Empty<FileEntry>();

    public string SnapshotName => Id.ToString()[..8];
    public long TotalSize => Files.Sum(f => f.Size);
    public int FileCount => Files.Count();

    public Snapshot()
    {
    }

    public Snapshot(Guid id, DateTime createdAtUtc, int fileCount, long totalSize, FileEntry[] files)
    {
        Id = id;
        CreatedAtUtc = createdAtUtc;
        Files = files;
    }

    public static Snapshot Create()
    {
        return new Snapshot(
            Guid.CreateVersion7(),
            DateTime.UtcNow,
            0,
            0,
            Array.Empty<FileEntry>());
    }

    public void AddFile(FileEntry file)
    {
        Files.Add(file);
    }
}