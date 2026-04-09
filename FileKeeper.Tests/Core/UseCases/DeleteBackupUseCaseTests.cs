using ErrorOr;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Wrappers;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.UseCases;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FileKeeper.Tests.Core.UseCases;

public class DeleteBackupUseCaseTests : IAsyncLifetime
{
    private readonly DeleteBackupUseCase _sut;

    private readonly Mock<ISnapshotRepository> _snapshotRepository;
    private readonly Mock<IFileWrapper> _fileWrapper;

    public DeleteBackupUseCaseTests()
    {
        _snapshotRepository = new Mock<ISnapshotRepository>();
        _fileWrapper = new Mock<IFileWrapper>();

        _sut = new DeleteBackupUseCase(
            _snapshotRepository.Object,
            _fileWrapper.Object,
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
    public async Task ExecuteAsync_ShouldFail_IfGetSnapshotFails()
    {
        // Arrange
        var snapshotId = new Guid("C2ECB303-00D8-4AA4-83C9-ADDCBABBEEE8");

        _snapshotRepository
            .Setup(s => s.GetSnapshotAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.Failure(description: "Error getting snapshot"));

        // Act
        var result = await _sut.ExecuteAsync(snapshotId, null, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Error getting snapshot", result.FirstError.Description);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFail_IfGetNextSnapshotFails()
    {
        // Arrange
        var snapshotId = new Guid("C2ECB303-00D8-4AA4-83C9-ADDCBABBEEE8");

        var snapShot = new Snapshot(snapshotId, DateTime.UtcNow, []);

        _snapshotRepository
            .Setup(s => s.GetSnapshotAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapShot);

        _snapshotRepository
            .Setup(s => s.GetNextSnapshotAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.Failure(description: "Error getting next snapshot"));

        // Act
        var result = await _sut.ExecuteAsync(snapshotId, null, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Error getting next snapshot", result.FirstError.Description);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSucceed_WhenNextBackupNotFound()
    {
        // Arrange
        var snapshotId = new Guid("C2ECB303-00D8-4AA4-83C9-ADDCBABBEEE8");

        var snapShot = new Snapshot(snapshotId, DateTime.UtcNow, []);

        _snapshotRepository
            .Setup(s => s.GetSnapshotAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapShot);

        _snapshotRepository
            .Setup(s => s.GetNextSnapshotAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.NotFound(description: "No next backup found"));

        // Act
        var result = await _sut.ExecuteAsync(snapshotId, null, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        _snapshotRepository
            .Verify(s => s.DeleteSnapshotAsync(snapshotId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSucceed_WhenNoNextBackup_SnapshotContainOneFile()
    {
        // Arrange
        var snapshotId = new Guid("C2ECB303-00D8-4AA4-83C9-ADDCBABBEEE8");

        var snapShot = new Snapshot(
            snapshotId,
            DateTime.UtcNow,
            [
                new FileEntry(
                    Guid.NewGuid(),
                    "/home/felipe",
                    "file1.txt",
                    "/home/backup/abc",
                    "abcdefgh",
                    1000,
                    DateTime.Now.AddMinutes(-3),
                    "C2ECB30300D8"
                )
            ]);

        _snapshotRepository
            .Setup(s => s.GetSnapshotAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapShot);

        _snapshotRepository
            .Setup(s => s.GetNextSnapshotAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.NotFound(description: "No next backup found"));

        // Act
        var result = await _sut.ExecuteAsync(snapshotId, null, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        _fileWrapper
            .Verify(f => f.DeleteFile("/home/backup/abc"), Times.Once);

        _snapshotRepository
            .Verify(s => s.DeleteSnapshotAsync(snapshotId, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task ExecuteAsync_ShouldSucceed_WithNextBackup_OneFileForEachType()
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

        _snapshotRepository
            .Setup(s => s.GetSnapshotAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        _snapshotRepository
            .Setup(s => s.GetNextSnapshotAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(nextSnapshot);

        // Act
        var result = await _sut.ExecuteAsync(snapshotId, null, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        _fileWrapper.Verify(f => f.DeleteFile("/home/backup/abc1"), Times.Never);
        _fileWrapper.Verify(f => f.DeleteFile("/home/backup/abc2"), Times.Once);
        _fileWrapper.Verify(f => f.DeleteFile("/home/backup/abc3"), Times.Once);

        _snapshotRepository
            .Verify(s => s.DeleteSnapshotAsync(snapshotId, It.IsAny<CancellationToken>()), Times.Once);
        
        _snapshotRepository
            .Verify(s => s.UpdateSnapshotAsync(nextSnapshot, It.IsAny<CancellationToken>()), Times.Once);
    }
}