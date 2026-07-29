using ErrorOr;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Models.DTOs;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.UseCases;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FileKeeper.Tests.Core.UseCases;

public class DeleteBackupUseCaseTests : IAsyncLifetime
{
    private readonly DeleteBackupUseCase _sut;

    private readonly Mock<ISnapshotService> _snapshotService;

    public DeleteBackupUseCaseTests()
    {
        _snapshotService = new Mock<ISnapshotService>();

        _sut = new DeleteBackupUseCase(
            _snapshotService.Object,
            new NullLogger<DeleteBackupUseCase>());
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFail_IfGetIndexFails()
    {
        // Arrange
        var snapshotId = new Guid("C2ECB303-00D8-4AA4-83C9-ADDCBABBEEE8");

        _snapshotService
            .Setup(s => s.GetIndexAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.Failure(description: "Error getting snapshots index"));

        // Act
        var result = await _sut.ExecuteAsync(snapshotId, null, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Error getting snapshots index", result.FirstError.Description);

        _snapshotService.Verify(v =>
            v.SaveIndexAsync(It.IsAny<SnapshotIndex>(), It.IsAny<CancellationToken>()), Times.Never);

        _snapshotService.Verify(v =>
            v.DeleteFilesAsync(It.IsAny<List<FileToDelete>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFail_IfIndexDoesntContainSnapshot()
    {
        // Arrange
        var snapshotId = new Guid("C2ECB303-00D8-4AA4-83C9-ADDCBABBEEE8");

        var index = new SnapshotIndex(
            snapshots: new List<Snapshot>());

        _snapshotService
            .Setup(s => s.GetIndexAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(index);

        // Act
        var result = await _sut.ExecuteAsync(snapshotId, null, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal($"Snapshot {snapshotId} doesn't exist.", result.FirstError.Description);

        _snapshotService.Verify(v =>
            v.SaveIndexAsync(It.IsAny<SnapshotIndex>(), It.IsAny<CancellationToken>()), Times.Never);

        _snapshotService.Verify(v =>
            v.DeleteFilesAsync(It.IsAny<List<FileToDelete>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSucceed_WhenNoNextBackup_ShouldDeleteAllFilesThatBelongsToIt()
    {
        // Arrange
        var snapshotId = new Guid("C2ECB303-00D8-4AA4-83C9-ADDCBABBEEE8");
        var snapshotName = snapshotId.ToString("N")[..12];

        var priorSnapshotId = new Guid("D8CDA611-23E5-4BBC-8EFD-354A77E28F57");
        var priorSnapshotName = priorSnapshotId.ToString("N")[..12];
        
        var snapshot = new Snapshot(
            snapshotId,
            DateTime.UtcNow,
            new List<FileEntry>()
            {
                new FileEntry(
                    id: Guid.CreateVersion7(),
                    sourceDirectory: "/home/felipe",
                    relativePath: "file1.txt",
                    storedPath: "abcd/abcdefghijkl1",
                    "k8vfVcLU9Ts4e9YMT9IEpukdcL877GL+UIiRWC+Qi40=",
                    size: 100,
                    lastModified: DateTime.UtcNow,
                    snapshotName),
                new FileEntry(
                    id: Guid.CreateVersion7(),
                    sourceDirectory: "/home/felipe",
                    relativePath: "file2.txt",
                    storedPath: "abcd/abcdefghijkl2",
                    "UqQm+33HyANVVbmXykthdNWI1PIAFWLuGjt9oHeVsp0=",
                    size: 100,
                    lastModified: DateTime.UtcNow,
                    snapshotName),
                new FileEntry(
                    id: Guid.CreateVersion7(),
                    sourceDirectory: "/home/felipe",
                    relativePath: "file_other.txt",
                    storedPath: "abcd/abcdefghijkl3",
                    "UqQm+33HyANVVbmXykthdNWI1PIAFWLuGjt9oHeVsg0=",
                    size: 100,
                    lastModified: DateTime.UtcNow,
                    priorSnapshotName)
            });

        var index = new SnapshotIndex(
            snapshots: new List<Snapshot>()
            {
                snapshot
            });

        _snapshotService
            .Setup(s => s.GetIndexAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(index);
        
        List<FileToDelete>? capturedDeletedFiles = null;

        _snapshotService
            .Setup(v => v.DeleteFilesAsync(It.IsAny<IEnumerable<FileToDelete>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<FileToDelete>, CancellationToken>((files, _) =>
            {
                capturedDeletedFiles = files?.ToList();
            });

        // Act
        var result = await _sut.ExecuteAsync(snapshotId, null, CancellationToken.None);

        // Assert
        var filesDeleted = 2;
        
        Assert.False(result.IsError);

        _snapshotService.Verify(v =>
            v.SaveIndexAsync(It.IsAny<SnapshotIndex>(), It.IsAny<CancellationToken>()), Times.Once);

        _snapshotService.Verify(v =>
            v.DeleteFilesAsync(It.IsAny<List<FileToDelete>>(), It.IsAny<CancellationToken>()), Times.Once);
        
        Assert.NotNull(capturedDeletedFiles);
        Assert.Equal(filesDeleted, capturedDeletedFiles!.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSucceed_WithNextBackup_ShouldDeleteTwoFiles()
    {
        // Arrange
        var snapshotId = new Guid("C2ECB303-00D8-4AA4-83C9-ADDCBABBEEE8");
        var nextSnapshotId = new Guid("90375B1A-170E-47D2-A032-9F1CC9F28A02");
        
        var priorSnapshotId = new Guid("D8CDA611-23E5-4BBC-8EFD-354A77E28F57");
        var priorSnapshotName = priorSnapshotId.ToString("N")[..12];

        var snapshot = new Snapshot(
            snapshotId,
            DateTime.UtcNow,
            [
                new FileEntry(
                    Guid.NewGuid(),
                    "/home/felipe",
                    "file1.txt",
                    "/home/backup/abc1",
                    "abcdefgh1",
                    1000,
                    DateTime.Now.AddMinutes(-3),
                    priorSnapshotName
                ),
                new FileEntry(
                    Guid.NewGuid(),
                    "/home/felipe",
                    "file2.txt",
                    "/home/backup/abc2",
                    "abcdefgh2",
                    1000,
                    DateTime.Now.AddMinutes(-3),
                    "c2ecb30300d8"
                ),
                new FileEntry(
                    Guid.NewGuid(),
                    "/home/felipe",
                    "file3.txt",
                    "/home/backup/abc3",
                    "abcdefgh3",
                    1000,
                    DateTime.Now.AddMinutes(-3),
                    "c2ecb30300d8"
                ),
                new FileEntry(
                    Guid.NewGuid(),
                    "/home/felipe",
                    "file4.txt",
                    "/home/backup/abc4",
                    "abcdefgh4",
                    1000,
                    DateTime.Now.AddMinutes(-3),
                    "c2ecb30300d8"
                )
            ]);

        var nextSnapshot = new Snapshot(
            nextSnapshotId,
            DateTime.UtcNow,
            [
                new FileEntry( // same file, point to prior snapshot (should be kept)
                    Guid.NewGuid(),
                    "/home/felipe",
                    "file1.txt",
                    "/home/backup/abc1",
                    "abcdefgh1",
                    1000,
                    DateTime.Now.AddMinutes(-3),
                    priorSnapshotName
                ),
                new FileEntry( // same file, point to current snapshot (should be kept)
                    Guid.NewGuid(),
                    "/home/felipe",
                    "file2.txt",
                    "/home/backup/abc2",
                    "abcdefgh2",
                    1000,
                    DateTime.Now.AddMinutes(-3),
                    "c2ecb30300d8"
                ),
                new FileEntry( // same file, but different hash and point to new snapshot, will be deleted
                    Guid.NewGuid(),
                    "/home/felipe",
                    "file3.txt",
                    "/home/backup/abc3",
                    "abcdefgh3v2",
                    1000,
                    DateTime.Now.AddMinutes(-3),
                    "90375b1a170e"
                )
                // no third file, will be deleted
            ]);

        var index = new SnapshotIndex(
            snapshots: new List<Snapshot>()
            {
                snapshot,
                nextSnapshot
            });

        _snapshotService
            .Setup(s => s.GetIndexAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(index);
        
        List<FileToDelete>? capturedDeletedFiles = null;

        _snapshotService
            .Setup(v => v.DeleteFilesAsync(It.IsAny<IEnumerable<FileToDelete>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<FileToDelete>, CancellationToken>((files, _) =>
            {
                capturedDeletedFiles = files?.ToList();
            });

        // Act
        var result = await _sut.ExecuteAsync(snapshotId, null, CancellationToken.None);

        // Assert
        var filesDeleted = 2;
        
        Assert.False(result.IsError);

        _snapshotService.Verify(v =>
            v.SaveIndexAsync(It.IsAny<SnapshotIndex>(), It.IsAny<CancellationToken>()), Times.Once);

        _snapshotService.Verify(v =>
            v.DeleteFilesAsync(It.IsAny<List<FileToDelete>>(), It.IsAny<CancellationToken>()), Times.Once);
        
        Assert.NotNull(capturedDeletedFiles);
        Assert.Equal(filesDeleted, capturedDeletedFiles!.Count);
    }

    [Fact]
    public async Task ExecuteAsync_DeletingTwoOldestSnapshotsSequentially_ShouldNotDeleteFileStillReferencedByLaterSnapshots()
    {
        // Arrange
        // Chain S1 -> S2 -> S3 -> S4, all sharing "shared.txt" which has never changed
        // since S1. This mirrors exactly what CreateBackupUseCase produces for an
        // unchanged file: every later snapshot's FileEntry keeps FoundInSnapshot pointing
        // at S1 (the original owner) because unchanged files are carried forward without
        // ever being "re-stamped" with the current snapshot's name.
        var s1Id = new Guid("11111111-1111-1111-1111-111111111111");
        var s2Id = new Guid("22222222-2222-2222-2222-222222222222");
        var s3Id = new Guid("33333333-3333-3333-3333-333333333333");
        var s4Id = new Guid("44444444-4444-4444-4444-444444444444");

        var s1Name = s1Id.ToString("N")[..12];

        const string sharedStoredPath = "ab/shared-blob";
        const string sharedHash = "shared-hash==";

        FileEntry SharedEntry() => new(
            Guid.NewGuid(),
            "/home/felipe",
            "shared.txt",
            sharedStoredPath,
            sharedHash,
            100,
            DateTime.UtcNow,
            s1Name); // every snapshot's copy still says "found in S1": never re-stamped while unchanged

        var s1 = new Snapshot(s1Id, DateTime.UtcNow, new List<FileEntry> { SharedEntry() });
        var s2 = new Snapshot(s2Id, DateTime.UtcNow, new List<FileEntry> { SharedEntry() });
        var s3 = new Snapshot(s3Id, DateTime.UtcNow, new List<FileEntry> { SharedEntry() });
        var s4 = new Snapshot(s4Id, DateTime.UtcNow, new List<FileEntry> { SharedEntry() });

        var index = new SnapshotIndex(new List<Snapshot> { s1, s2, s3, s4 });

        _snapshotService
            .Setup(s => s.GetIndexAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(index); // same mutable instance every call, like a real index would be after being reloaded from disk

        var deleteCalls = new List<List<FileToDelete>>();

        _snapshotService
            .Setup(v => v.DeleteFilesAsync(It.IsAny<IEnumerable<FileToDelete>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<FileToDelete>, CancellationToken>((files, _) => deleteCalls.Add(files.ToList()));

        // Act: delete the two oldest snapshots in sequence, exactly like a user pruning old backups.
        var deleteS1Result = await _sut.ExecuteAsync(s1Id, null, CancellationToken.None);
        var deleteS2Result = await _sut.ExecuteAsync(s2Id, null, CancellationToken.None);

        // Assert
        Assert.False(deleteS1Result.IsError);
        Assert.False(deleteS2Result.IsError);

        // Sanity check: S3 and S4 are still alive and still list "shared.txt" pointing at the same blob.
        Assert.Contains(index.Snapshots, s => s.Id == s3Id);
        Assert.Contains(index.Snapshots, s => s.Id == s4Id);
        Assert.Contains(s3.Files, f => f.StoredPath == sharedStoredPath);
        Assert.Contains(s4.Files, f => f.StoredPath == sharedStoredPath);

        // Desired/correct behavior: since S3/S4 still reference the shared blob, deleting
        // S1 and then S2 must NEVER delete its bytes from the store.
        var deletedStoredPaths = deleteCalls.SelectMany(c => c.Select(f => f.StoredPath)).ToList();
        Assert.DoesNotContain(sharedStoredPath, deletedStoredPaths);
    }
}