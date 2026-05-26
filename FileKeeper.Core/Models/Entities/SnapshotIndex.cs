namespace FileKeeper.Core.Models.Entities;

public class SnapshotIndex
{
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