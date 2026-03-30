using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using FileKeeper.Core.Models.Entities;

namespace FileKeeper.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<Snapshot> Snapshots { get; } =
        new(CreateMockSnapshots());

    private static IEnumerable<Snapshot> CreateMockSnapshots()
    {
        var snapshotA = Snapshot.Create();
        snapshotA.AddFile(FileEntry.Create(
            relativePath: "docs/report.pdf",
            storedPath: "store/2026/03/report.bin",
            hash: "A1B2C3",
            size: 1_245_760,
            lastModified: DateTime.UtcNow.AddDays(-2),
            snapshotId: snapshotA.SnapshotName));

        snapshotA.AddFile(FileEntry.Create(
            relativePath: "images/logo.png",
            storedPath: "store/2026/03/logo.bin",
            hash: "D4E5F6",
            size: 218_334,
            lastModified: DateTime.UtcNow.AddDays(-2),
            snapshotId: snapshotA.SnapshotName));

        var snapshotB = Snapshot.Create();
        snapshotB.AddFile(FileEntry.Create(
            relativePath: "src/main.cs",
            storedPath: "store/2026/03/main.bin",
            hash: "ABCDEF",
            size: 12_840,
            lastModified: DateTime.UtcNow.AddDays(-1),
            snapshotId: snapshotB.SnapshotName));

        return [snapshotA, snapshotB];
    }
}