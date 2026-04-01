using FileKeeper.Core.Models.Entities;

namespace FileKeeper.Tests.Core.Models.Entities;

public class SnapshotTests
{
    [Fact]
    public void Snapshot_SnapshotName_ShouldReflectTheId()
    {
        var id = Guid.Parse("019d3a22-9a87-75ae-ac95-7222c74df7c4");
        var expectedName = "019d3a229a87";
        var snapshot = new Snapshot(id, DateTime.UtcNow, []);
        
        Assert.Equal(id, snapshot.Id);
        Assert.Equal(expectedName, snapshot.SnapshotName);
    }
}