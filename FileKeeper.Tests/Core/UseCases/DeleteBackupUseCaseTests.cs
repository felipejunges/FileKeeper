using ErrorOr;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.UseCases;
using FileKeeper.Tests.Core.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FileKeeper.Tests.Core.UseCases;

public class DeleteBackupUseCaseTests : IAsyncLifetime
{
    private readonly DeleteBackupUseCase _sut;
    
    private readonly Mock<ISnapshotRepository> _snapshotRepository;
    private readonly FileWrapperMock _fileWrapper;

    public DeleteBackupUseCaseTests()
    {
        _snapshotRepository = new Mock<ISnapshotRepository>();
        _fileWrapper = new FileWrapperMock();
        
        _sut = new DeleteBackupUseCase(
            _snapshotRepository.Object,
            _fileWrapper,
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
            .Setup(s => s.GetSnapshotAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.GetSnapshotAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapShot);
        
        _snapshotRepository
            .Setup(s => s.GetNextSnapshotAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.GetSnapshotAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapShot);
        
        _snapshotRepository
            .Setup(s => s.GetNextSnapshotAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.NotFound(description: "No next backup found"));
        
        // Act
        var result = await _sut.ExecuteAsync(snapshotId, null, CancellationToken.None);
        
        // Assert
        Assert.False(result.IsError);
    }
}