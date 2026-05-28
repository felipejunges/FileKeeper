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
    public async Task ExecuteAsync_ShouldSucceed_WhenNoNextBackup_ShouldDeleteAllFiles()
    {
        // Arrange
        var snapshotId = new Guid("C2ECB303-00D8-4AA4-83C9-ADDCBABBEEE8");
        var snapshotName = snapshotId.ToString("N")[..12];

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
                    "k8vfVcLU9Ts4e9YMT9IEpukdcL877GL+UIiRWC+Qi40=", // same hash as '"Content of file 1"'
                    size: 100,
                    lastModified: DateTime.UtcNow,
                    snapshotName),
                new FileEntry(
                    id: Guid.CreateVersion7(),
                    sourceDirectory: "/home/felipe",
                    relativePath: "file2.txt",
                    storedPath: "abcd/abcdefghijkl2",
                    "UqQm+33HyANVVbmXykthdNWI1PIAFWLuGjt9oHeVsp0=", // same hash as '"Content of file 2"'
                    size: 100,
                    lastModified: DateTime.UtcNow,
                    snapshotName)
            });

        var index = new SnapshotIndex(
            snapshots: new List<Snapshot>()
            {
                snapshot
            });

        _snapshotService
            .Setup(s => s.GetIndexAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(index);

        // Act
        var result = await _sut.ExecuteAsync(snapshotId, null, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        _snapshotService.Verify(v =>
            v.SaveIndexAsync(It.IsAny<SnapshotIndex>(), It.IsAny<CancellationToken>()), Times.Once);

        _snapshotService.Verify(v =>
            v.DeleteFilesAsync(It.IsAny<List<FileToDelete>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSucceed_WithNextBackup_ShouldDeleteOnlyOneFile()
    {
        // Arrange
        var snapshotId = new Guid("C2ECB303-00D8-4AA4-83C9-ADDCBABBEEE8");
        var nextSnapshotId = new Guid("90375B1A-170E-47D2-A032-9F1CC9F28A02");

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
                    "c2ecb30300d8"
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
                )
            ]);

        var nextSnapshot = new Snapshot(
            nextSnapshotId,
            DateTime.UtcNow,
            [
                new FileEntry( // same file, point to current snapshot (should be kept)
                    Guid.NewGuid(),
                    "/home/felipe",
                    "file1.txt",
                    "/home/backup/abc1",
                    "abcdefgh1",
                    1000,
                    DateTime.Now.AddMinutes(-3),
                    "c2ecb30300d8"
                ),
                new FileEntry( // same file, but different hash and point to new snapshot, will be deleted
                    Guid.NewGuid(),
                    "/home/felipe",
                    "file2.txt",
                    "/home/backup/abc2",
                    "abcdefgh2v2",
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

        // Act
        var result = await _sut.ExecuteAsync(snapshotId, null, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        _snapshotService.Verify(v =>
            v.SaveIndexAsync(It.IsAny<SnapshotIndex>(), It.IsAny<CancellationToken>()), Times.Once);

        _snapshotService.Verify(v =>
            v.DeleteFilesAsync(It.IsAny<List<FileToDelete>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}