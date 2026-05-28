using System.Text.Json.Serialization;

namespace FileKeeper.Core.Models.Entities;

public class SnapshotIndex
{
    [JsonInclude]
    public IList<Snapshot> Snapshots { get; private set; }

    public SnapshotIndex()
    {
        Snapshots = new List<Snapshot>();
    }

    public SnapshotIndex(IList<Snapshot> snapshots)
    {
        Snapshots = snapshots;
    }

    public static SnapshotIndex Empty() => new SnapshotIndex();
}