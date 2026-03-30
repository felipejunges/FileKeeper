using System.Text.Json.Serialization;

namespace FileKeeper.Core.Models.Entities;

public class Snapshot
{
    [JsonInclude]
    public Guid Id { get; private set; }

    [JsonInclude]
    public DateTime CreatedAtUtc { get; private set; }

    [JsonInclude]
    public ICollection<FileEntry> Files { get; private set; } = [];

    public string SnapshotName => Id.ToString()[..8];
    public long TotalSize => Files.Sum(f => f.Size);
    public int FileCount => Files.Count();

    public Snapshot()
    {
    }

    public Snapshot(Guid id, DateTime createdAtUtc, ICollection<FileEntry> files)
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
            new List<FileEntry>());
    }

    public void AddFile(FileEntry file)
    {
        Files.Add(file);
    }
}