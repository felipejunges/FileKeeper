using System.Text.Json.Serialization;

namespace FileKeeper.Core.Models.Entities;

public class SnapshotIndex
{
    public List<SnapshotIndexItem> Items { get; private set; }

    public SnapshotIndex()
    {
        Items = new();
    }

    public SnapshotIndex(List<SnapshotIndexItem> items)
    {
        Items = items;
    }

    public void AddSnapshot(Guid id, DateTime createdAtUtc)
    {
        Items.Add(new(id, createdAtUtc));
    }

    public class SnapshotIndexItem
    {
        [JsonInclude] public Guid Id { get; private set; }

        [JsonInclude] public DateTime CreatedAtUtc { get; private set; }

        public SnapshotIndexItem(Guid id, DateTime createdAtUtc)
        {
            Id = id;
            CreatedAtUtc = createdAtUtc;
        }
    }
}