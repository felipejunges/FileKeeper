using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Persistence.Repositories;
using Moq;

namespace FileKeeper.Tests.Core.Persistence.Repositories;

public class BackupRepositoryBehaviorTests
{
    [Fact(DisplayName = "01 - GetByIdAsync should propagate query failure when connection fails")]
    public async Task GetByIdAsync_ShouldPropagateQueryFailure_WhenConnectionFails()
    {
        var databaseServiceMock = new Mock<IDatabaseService>();
        databaseServiceMock
            .Setup(service => service.GetConnection())
            .Throws(new InvalidOperationException("Connection failed."));

        var sut = new BackupRepository(databaseServiceMock.Object);

        var result = await sut.GetByIdAsync(1, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("Query execution failed", result.FirstError.Description);
    }

    [Fact(DisplayName = "02 - InsertAsync should propagate query failure and not update Backup Id")]
    public async Task InsertAsync_ShouldPropagateQueryFailureAndKeepBackupId_WhenConnectionFails()
    {
        var databaseServiceMock = new Mock<IDatabaseService>();
        databaseServiceMock
            .Setup(service => service.GetConnection())
            .Throws(new InvalidOperationException("Connection failed."));

        var sut = new BackupRepository(databaseServiceMock.Object);
        var backup = new Backup(0, new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc), 2, 0, 0);

        var result = await sut.InsertAsync(backup, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(0, backup.Id);
    }

    [Fact(DisplayName = "03 - GetNextBackupAfterAsync should propagate query failure when connection fails")]
    public async Task GetNextBackupAfterAsync_ShouldPropagateQueryFailure_WhenConnectionFails()
    {
        var databaseServiceMock = new Mock<IDatabaseService>();
        databaseServiceMock
            .Setup(service => service.GetConnection())
            .Throws(new InvalidOperationException("Connection failed."));

        var sut = new BackupRepository(databaseServiceMock.Object);

        var result = await sut.GetNextBackupAfterAsync(
            new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("Query execution failed", result.FirstError.Description);
    }

    [Fact(DisplayName = "04 - GetOldestAsync should propagate query failure when connection fails")]
    public async Task GetOldestAsync_ShouldPropagateQueryFailure_WhenConnectionFails()
    {
        var databaseServiceMock = new Mock<IDatabaseService>();
        databaseServiceMock
            .Setup(service => service.GetConnection())
            .Throws(new InvalidOperationException("Connection failed."));

        var sut = new BackupRepository(databaseServiceMock.Object);

        var result = await sut.GetOldestAsync(CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("Query execution failed", result.FirstError.Description);
    }
}
