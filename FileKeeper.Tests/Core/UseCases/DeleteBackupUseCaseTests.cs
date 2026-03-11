using ErrorOr;
using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Models.DMs;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.UseCases;
using Microsoft.Extensions.Logging;
using Moq;

namespace FileKeeper.Tests.Core.UseCases;

public class DeleteBackupUseCaseTests
{
    private readonly DeleteBackupUseCase _sut;

    private readonly Mock<IBackupRepository> _backupRepositoryMock;
    private readonly Mock<IFileRepository> _fileRepositoryMock;
    private readonly Mock<IDatabaseService> _databaseServiceMock;

    public DeleteBackupUseCaseTests()
    {
        _backupRepositoryMock = new Mock<IBackupRepository>();
        _fileRepositoryMock = new Mock<IFileRepository>();
        _databaseServiceMock = new Mock<IDatabaseService>();
        var loggerMock = new Mock<ILogger<DeleteBackupUseCase>>();

        _sut = new DeleteBackupUseCase(
            _backupRepositoryMock.Object,
            _fileRepositoryMock.Object,
            _databaseServiceMock.Object,
            loggerMock.Object);
    }

    [Fact(DisplayName = "01 - ExecuteAsync should fail when backup is not found")]
    public async Task ExecuteAsync_ShouldFail_WhenBackupIsNotFound()
    {
        // Arrange
        var backupId = 1L;

        Mock_BackupRepository_GetByIdAsync(backupId, Error.NotFound(description: "Backup not found."));

        // Act
        var result = await _sut.ExecuteAsync(backupId, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Backup not found.", result.FirstError.Description);

        _databaseServiceMock.Verify(service => service.BeginTransaction(), Times.Once);
        _databaseServiceMock.Verify(service => service.CommitTransaction(), Times.Never);
        _databaseServiceMock.Verify(service => service.RollbackTransaction(), Times.Once);
    }

    [Fact(DisplayName = "02 - ExecuteAsync should fail when next backup fails to get")]
    public async Task ExecuteAsync_ShouldFail_WhenNextBackupFailToGet()
    {
        // Arrange
        var backupId = 1L;
        var backup = new Backup(backupId, new DateTime(2026, 3, 4, 14, 17, 0, DateTimeKind.Utc), 10, 5, 2, 0);
        
        Mock_BackupRepository_GetByIdAsync(backupId, backup);
        Mock_BackupRepository_GetNextBackupByIdAsync(backup.Id, Error.Unexpected(description: "Database error while fetching backup."));

        // Act
        var result = await _sut.ExecuteAsync(backupId, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Database error while fetching backup.", result.FirstError.Description);

        _databaseServiceMock.Verify(service => service.BeginTransaction(), Times.Once);
        _databaseServiceMock.Verify(service => service.CommitTransaction(), Times.Never);
        _databaseServiceMock.Verify(service => service.RollbackTransaction(), Times.Once);
    }

    [Fact(DisplayName = "03 - ExecuteAsync should fail when next backup is prior to the current backup")]
    public async Task ExecuteAsync_ShouldFail_WhenNextBackupIsPriorToTheCurrent()
    {
        // Arrange
        var backupId = 1L;
        var backup = new Backup(backupId, new DateTime(2026, 3, 4, 14, 17, 0, DateTimeKind.Utc), 10, 5, 2, 0);
        var nextBackup = new Backup(backupId, backup.CreatedAt.AddMinutes(-1), 9, 4, 3, 0); // Next backup is prior to the current
        
        Mock_BackupRepository_GetByIdAsync(backupId, backup);
        Mock_BackupRepository_GetNextBackupByIdAsync(backup.Id, nextBackup);
        Mock_FileRepository_GetFilesToDeleteAsync(backupId, Error.Unexpected(description: "Database error while fetching files to delete."));

        // Act
        var result = await _sut.ExecuteAsync(backupId, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.StartsWith("The next backup's creation date is earlier than the current backup's creation date, which is unexpected.", result.FirstError.Description);

        _databaseServiceMock.Verify(service => service.BeginTransaction(), Times.Once);
        _databaseServiceMock.Verify(service => service.CommitTransaction(), Times.Never);
        _databaseServiceMock.Verify(service => service.RollbackTransaction(), Times.Once);
    }
    
    [Fact(DisplayName = "04 - ExecuteAsync should fail when get files to delete fails")]
    public async Task ExecuteAsync_ShouldFail_WhenGetFilesToDeleteFails()
    {
        // Arrange
        var backupId = 1L;
        var backup = new Backup(backupId, new DateTime(2026, 3, 4, 14, 17, 0, DateTimeKind.Utc), 10, 5, 2, 0);
        var nextBackup = new Backup(backupId + 1, backup.CreatedAt.AddMinutes(1), 9, 4, 3, 0);
        
        Mock_BackupRepository_GetByIdAsync(backupId, backup);
        Mock_BackupRepository_GetNextBackupByIdAsync(backup.Id, nextBackup);
        Mock_FileRepository_GetFilesToDeleteAsync(backupId, Error.Unexpected(description: "Database error while fetching files to delete."));

        // Act
        var result = await _sut.ExecuteAsync(backupId, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Database error while fetching files to delete.", result.FirstError.Description);

        _databaseServiceMock.Verify(service => service.BeginTransaction(), Times.Once);
        _databaseServiceMock.Verify(service => service.CommitTransaction(), Times.Never);
        _databaseServiceMock.Verify(service => service.RollbackTransaction(), Times.Once);
    }

    [Fact(DisplayName = "05 - ExecuteAsync should fail when move files to next backup fails")]
    public async Task ExecuteAsync_ShouldFail_WhenMoveFilesToNextBackupFails()
    {
        // Arrange
        var fileId = 1L;
        var backupId = 1L;
        var backup = new Backup(backupId, new DateTime(2026, 3, 4, 14, 17, 0, DateTimeKind.Utc), 10, 5, 2, 0);
        var nextBackup = new Backup(backupId + 1, backup.CreatedAt.AddMinutes(1), 9, 4, 3, 0);

        var filesToDelete = CreateListOfFilesToDelete(fileId, backupId, 10);
        var filesToMove = filesToDelete.Where(f => !f.ExistsInNextBackup).Select(f => f.Id).ToList();

        Mock_BackupRepository_GetByIdAsync(backupId, backup);
        Mock_BackupRepository_GetNextBackupByIdAsync(backup.Id, nextBackup);
        Mock_FileRepository_GetFilesToDeleteAsync(backupId, filesToDelete);
        Mock_FileRepository_MoveVersionsToBackupAsync(filesToMove, backupId + 1,
            Error.Unexpected(description: "Database error while moving files to next backup."));

        // Act
        var result = await _sut.ExecuteAsync(backupId, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Database error while moving files to next backup.", result.FirstError.Description);

        _databaseServiceMock.Verify(service => service.BeginTransaction(), Times.Once);
        _databaseServiceMock.Verify(service => service.CommitTransaction(), Times.Never);
        _databaseServiceMock.Verify(service => service.RollbackTransaction(), Times.Once);
    }
    
    [Fact(DisplayName = "06 - ExecuteAsync should fail when move deleted files to next backup fails")]
    public async Task ExecuteAsync_ShouldFail_WhenMoveDeletedFilesToNextBackupFails()
    {
        // Arrange
        var fileId = 1L;
        var backupId = 1L;
        var backup = new Backup(backupId, new DateTime(2026, 3, 4, 14, 17, 0, DateTimeKind.Utc), 10, 5, 2, 0);
        var nextBackup = new Backup(backupId + 1, backup.CreatedAt.AddMinutes(1), 9, 4, 3, 0);

        var filesToDelete = CreateListOfFilesToDelete(fileId, backupId, 10);
        var filesToMove = filesToDelete.Where(f => !f.ExistsInNextBackup).Select(f => f.Id).ToList();

        Mock_BackupRepository_GetByIdAsync(backupId, backup);
        Mock_BackupRepository_GetNextBackupByIdAsync(backup.Id, nextBackup);
        Mock_FileRepository_GetFilesToDeleteAsync(backupId, filesToDelete);
        Mock_FileRepository_MoveVersionsToBackupAsync(filesToMove, backupId + 1, 5);
        Mock_FileRepository_MoveDeletedFilesToNextBackupAsync(backup.Id, nextBackup.Id, Error.Unexpected(description: "Database error while moving deleted files to next backup."));

        // Act
        var result = await _sut.ExecuteAsync(backupId, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Database error while moving deleted files to next backup.", result.FirstError.Description);

        _databaseServiceMock.Verify(service => service.BeginTransaction(), Times.Once);
        _databaseServiceMock.Verify(service => service.CommitTransaction(), Times.Never);
        _databaseServiceMock.Verify(service => service.RollbackTransaction(), Times.Once);
    }
    
    [Fact(DisplayName = "07 - ExecuteAsync should fail when refresh backup totals and size fails")]
    public async Task ExecuteAsync_ShouldFail_WhenRefreshTotalsAndSizeFails()
    {
        // Arrange
        var fileId = 1L;
        var backupId = 1L;
        var backup = new Backup(backupId, new DateTime(2026, 3, 4, 14, 17, 0, DateTimeKind.Utc), 10, 5, 2, 0);
        var nextBackup = new Backup(backupId + 1, backup.CreatedAt.AddMinutes(1), 9, 4, 3, 0);

        var filesToDelete = CreateListOfFilesToDelete(fileId, backupId, 10);
        var filesToMove = filesToDelete.Where(f => !f.ExistsInNextBackup).Select(f => f.Id).ToList();

        Mock_BackupRepository_GetByIdAsync(backupId, backup);
        Mock_BackupRepository_GetNextBackupByIdAsync(backup.Id, nextBackup);
        Mock_FileRepository_GetFilesToDeleteAsync(backupId, filesToDelete);
        Mock_FileRepository_MoveVersionsToBackupAsync(filesToMove, backupId + 1, 5);
        Mock_FileRepository_MoveDeletedFilesToNextBackupAsync(backup.Id, nextBackup.Id, 8);
        Mock_BackupRepository_RefreshTotalsAndSizeAsync(nextBackup.Id, Error.Unexpected(description: "Database error while refreshing backup totals and size."));

        // Act
        var result = await _sut.ExecuteAsync(backupId, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Database error while refreshing backup totals and size.", result.FirstError.Description);

        _databaseServiceMock.Verify(service => service.BeginTransaction(), Times.Once);
        _databaseServiceMock.Verify(service => service.CommitTransaction(), Times.Never);
        _databaseServiceMock.Verify(service => service.RollbackTransaction(), Times.Once);
    }
    
    [Fact(DisplayName = "08 - ExecuteAsync should fail when delete files in backup fails")]
    public async Task ExecuteAsync_ShouldFail_WhenDeleteFilesInBackupFails()
    {
        // Arrange
        var fileId = 1L;
        var backupId = 1L;
        var backup = new Backup(backupId, new DateTime(2026, 3, 4, 14, 17, 0, DateTimeKind.Utc), 10, 5, 2, 0);
        var nextBackup = new Backup(backupId + 1, backup.CreatedAt.AddMinutes(1), 9, 4, 3, 0);

        var filesToDelete = CreateListOfFilesToDelete(fileId, backupId, 10);
        var filesToMove = filesToDelete.Where(f => !f.ExistsInNextBackup).Select(f => f.Id).ToList();

        Mock_BackupRepository_GetByIdAsync(backupId, backup);
        Mock_BackupRepository_GetNextBackupByIdAsync(backup.Id, nextBackup);
        Mock_FileRepository_GetFilesToDeleteAsync(backupId, filesToDelete);
        Mock_FileRepository_MoveVersionsToBackupAsync(filesToMove, backupId + 1, 5);
        Mock_FileRepository_MoveDeletedFilesToNextBackupAsync(backup.Id, nextBackup.Id, 8);
        Mock_BackupRepository_RefreshTotalsAndSizeAsync(nextBackup.Id, nextBackup);
        Mock_FileRepository_DeleteAllVersionsInBackupAsync(backupId, Error.Unexpected(description: "Database error while deleting files from backup."));

        // Act
        var result = await _sut.ExecuteAsync(backupId, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Database error while deleting files from backup.", result.FirstError.Description);

        _databaseServiceMock.Verify(service => service.BeginTransaction(), Times.Once);
        _databaseServiceMock.Verify(service => service.CommitTransaction(), Times.Never);
        _databaseServiceMock.Verify(service => service.RollbackTransaction(), Times.Once);
    }

    [Fact(DisplayName = "09 - ExecuteAsync should fail when delete files without version fails")]
    public async Task ExecuteAsync_ShouldFail_WhenDeleteFilesWithoutVersionFails()
    {
        // Arrange
        var fileId = 1L;
        var backupId = 1L;
        var backup = new Backup(backupId, new DateTime(2026, 3, 4, 14, 17, 0, DateTimeKind.Utc), 10, 5, 2, 0);
        var nextBackup = new Backup(backupId + 1, backup.CreatedAt.AddMinutes(1), 9, 4, 3, 0);

        var filesToDelete = CreateListOfFilesToDelete(fileId, backupId, 10);
        var filesToMove = filesToDelete.Where(f => !f.ExistsInNextBackup).Select(f => f.Id).ToList();

        Mock_BackupRepository_GetByIdAsync(backupId, backup);
        Mock_BackupRepository_GetNextBackupByIdAsync(backup.Id, nextBackup);
        Mock_FileRepository_GetFilesToDeleteAsync(backupId, filesToDelete);
        Mock_FileRepository_MoveVersionsToBackupAsync(filesToMove, backupId + 1, 5);
        Mock_FileRepository_MoveDeletedFilesToNextBackupAsync(backup.Id, nextBackup.Id, 8);
        Mock_BackupRepository_RefreshTotalsAndSizeAsync(nextBackup.Id, nextBackup);
        Mock_FileRepository_DeleteAllVersionsInBackupAsync(backupId, 3);
        Mock_FileRepository_DeleteFilesWithoutVersionsAsync(Error.Unexpected(description: "Database error while deleting files without versions."));

        // Act
        var result = await _sut.ExecuteAsync(backupId, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Database error while deleting files without versions.", result.FirstError.Description);

        _databaseServiceMock.Verify(service => service.BeginTransaction(), Times.Once);
        _databaseServiceMock.Verify(service => service.CommitTransaction(), Times.Never);
        _databaseServiceMock.Verify(service => service.RollbackTransaction(), Times.Once);
    }
    
    [Fact(DisplayName = "10 - ExecuteAsync should fail when delete backup fails")]
    public async Task ExecuteAsync_ShouldFail_WhenDeleteBackupFails()
    {
        // Arrange
        var fileId = 1L;
        var backupId = 1L;
        var backup = new Backup(backupId, new DateTime(2026, 3, 4, 14, 17, 0, DateTimeKind.Utc), 10, 5, 2, 0);
        var nextBackup = new Backup(backupId + 1, backup.CreatedAt.AddMinutes(1), 9, 4, 3, 0);

        var filesToDelete = CreateListOfFilesToDelete(fileId, backupId, 10);
        var filesToMove = filesToDelete.Where(f => !f.ExistsInNextBackup).Select(f => f.Id).ToList();

        Mock_BackupRepository_GetByIdAsync(backupId, backup);
        Mock_BackupRepository_GetNextBackupByIdAsync(backup.Id, nextBackup);
        Mock_FileRepository_GetFilesToDeleteAsync(backupId, filesToDelete);
        Mock_FileRepository_MoveVersionsToBackupAsync(filesToMove, backupId + 1, 5);
        Mock_FileRepository_MoveDeletedFilesToNextBackupAsync(backup.Id, nextBackup.Id, 8);
        Mock_BackupRepository_RefreshTotalsAndSizeAsync(nextBackup.Id, nextBackup);
        Mock_FileRepository_DeleteAllVersionsInBackupAsync(backupId, 3);
        Mock_FileRepository_DeleteFilesWithoutVersionsAsync(0);
        Mock_BackupRepository_DeleteAsync(backupId, Error.Unexpected(description: "Database error while deleting backup."));

        // Act
        var result = await _sut.ExecuteAsync(backupId, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Database error while deleting backup.", result.FirstError.Description);

        _databaseServiceMock.Verify(service => service.BeginTransaction(), Times.Once);
        _databaseServiceMock.Verify(service => service.CommitTransaction(), Times.Never);
        _databaseServiceMock.Verify(service => service.RollbackTransaction(), Times.Once);
    }

    [Fact(DisplayName = "11 - ExecuteAsync should succeed when backup has next backup")]
    public async Task ExecuteAsync_ShouldSucceed_WhenBackupHasNextBackup()
    {
        // Arrange
        var fileId = 1L;
        var backupId = 1L;
        var backup = new Backup(backupId, new DateTime(2026, 3, 4, 14, 17, 0, DateTimeKind.Utc), 10, 5, 2, 0);
        var nextBackup = new Backup(backupId + 1, backup.CreatedAt.AddMinutes(1), 9, 4, 3, 0);

        var filesToDelete = CreateListOfFilesToDelete(fileId, backupId, 10);
        var filesToMove = filesToDelete.Where(f => !f.ExistsInNextBackup).Select(f => f.Id).ToList();

        Mock_BackupRepository_GetByIdAsync(backupId, backup);
        Mock_BackupRepository_GetNextBackupByIdAsync(backup.Id, nextBackup);
        Mock_FileRepository_GetFilesToDeleteAsync(backupId, filesToDelete);
        Mock_FileRepository_MoveVersionsToBackupAsync(filesToMove, backupId + 1, 5);
        Mock_FileRepository_MoveDeletedFilesToNextBackupAsync(backup.Id, nextBackup.Id, 8);
        Mock_BackupRepository_RefreshTotalsAndSizeAsync(nextBackup.Id, nextBackup);
        Mock_FileRepository_DeleteAllVersionsInBackupAsync(backupId, 3);
        Mock_FileRepository_DeleteFilesWithoutVersionsAsync(0);
        Mock_BackupRepository_DeleteAsync(backupId, 1);

        // Act
        var result = await _sut.ExecuteAsync(backupId, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        _databaseServiceMock.Verify(service => service.BeginTransaction(), Times.Once);
        _databaseServiceMock.Verify(service => service.CommitTransaction(), Times.Once);
        _databaseServiceMock.Verify(service => service.RollbackTransaction(), Times.Never);
    }

    [Fact(DisplayName = "12 - ExecuteAsync should succeed when next backup returns NotFound")]
    public async Task ExecuteAsync_ShouldSucceed_WhenNextBackupReturnsNotFound()
    {
        // Arrange
        var fileId = 1L;
        var backupId = 1L;
        var backup = new Backup(backupId, new DateTime(2026, 3, 4, 14, 17, 0, DateTimeKind.Utc), 10, 5, 2, 0);

        var filesToDelete = CreateListOfFilesToDelete(fileId, backupId, 10);
        var filesToMove = filesToDelete.Where(f => !f.ExistsInNextBackup).Select(f => f.Id).ToList();

        Mock_BackupRepository_GetByIdAsync(backupId, backup);
        Mock_BackupRepository_GetNextBackupByIdAsync(backup.Id, Error.NotFound("Backup não localizado."));
        Mock_FileRepository_GetFilesToDeleteAsync(backupId, filesToDelete);
        Mock_FileRepository_MoveVersionsToBackupAsync(filesToMove, backupId + 1, 5);
        Mock_FileRepository_DeleteAllVersionsInBackupAsync(backupId, 3);
        Mock_FileRepository_DeleteFilesWithoutVersionsAsync(0);
        Mock_BackupRepository_DeleteAsync(backupId, 1);

        // Act
        var result = await _sut.ExecuteAsync(backupId, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        _databaseServiceMock.Verify(service => service.BeginTransaction(), Times.Once);
        _databaseServiceMock.Verify(service => service.CommitTransaction(), Times.Once);
        _databaseServiceMock.Verify(service => service.RollbackTransaction(), Times.Never);
    }
    
    [Fact(DisplayName = "13 - ExecuteAsync should succeed - Only move to next backup files it doesn't have")]
    public async Task ExecuteAsync_ShouldSucceed_OnlyMoveToNextBackupFilesItDoesntHave()
    {
        // Arrange
        var fileId = 1L;
        var backupId = 1L;
        var backup = new Backup(backupId, new DateTime(2026, 3, 4, 14, 17, 0, DateTimeKind.Utc), 10, 5, 2, 0);
        var nextBackup = new Backup(backupId + 1, backup.CreatedAt.AddMinutes(1), 9, 4, 3, 0);
        
        var filesToDelete = CreateListOfFilesToDelete(fileId, backupId, 10);
        var filesToMove = filesToDelete.Where(f => !f.ExistsInNextBackup).Select(f => f.Id).ToList();

        Mock_BackupRepository_GetByIdAsync(backupId, backup);
        Mock_BackupRepository_GetNextBackupByIdAsync(backup.Id, nextBackup);
        Mock_FileRepository_GetFilesToDeleteAsync(backupId, filesToDelete);

        MockCallback_FileRepository_MoveVersionsToBackupAsync(
            backupId + 1,
            new InvocationAction(invocation =>
            {
                var passedIds = invocation.Arguments[0] as List<long>;
                Assert.NotNull(passedIds);
                Assert.Equal(filesToMove.Count, passedIds.Count);
                Assert.All(passedIds, id => Assert.Contains(id, filesToMove));
            }));
        
        Mock_FileRepository_MoveDeletedFilesToNextBackupAsync(backup.Id, nextBackup.Id, 8);
        Mock_BackupRepository_RefreshTotalsAndSizeAsync(nextBackup.Id, nextBackup);
        Mock_FileRepository_DeleteAllVersionsInBackupAsync(backupId, 3);
        Mock_FileRepository_DeleteFilesWithoutVersionsAsync(0);
        Mock_BackupRepository_DeleteAsync(backupId, 1);

        // Act
        var result = await _sut.ExecuteAsync(backupId, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        
        _databaseServiceMock.Verify(service => service.BeginTransaction(), Times.Once);
        _databaseServiceMock.Verify(service => service.CommitTransaction(), Times.Once);
        _databaseServiceMock.Verify(service => service.RollbackTransaction(), Times.Never);
    }

    private void Mock_BackupRepository_GetByIdAsync(long backupId, ErrorOr<Backup> expectedResult)
    {
        _backupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(backupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);
    }

    private void Mock_BackupRepository_GetNextBackupByIdAsync(long backupId, ErrorOr<Backup> expectedResult)
    {
        _backupRepositoryMock
            .Setup(s => s.GetNextBackupByIdAsync(backupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);
    }

    private void Mock_FileRepository_GetFilesToDeleteAsync(long backupId, ErrorOr<IEnumerable<FileToDeleteDM>> expectedResult)
    {
        _fileRepositoryMock
            .Setup(s => s.GetFilesToDeleteAsync(backupId, It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);
    }

    private void Mock_FileRepository_MoveVersionsToBackupAsync(List<long> idsVersionsToMove, long backupId, ErrorOr<int> expectedResult)
    {
        _fileRepositoryMock
            .Setup(s => s.MoveVersionsToBackupAsync(idsVersionsToMove, backupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);
    }
    
    private void MockCallback_FileRepository_MoveVersionsToBackupAsync(long backupId, InvocationAction callbackExpression)
    {
        _fileRepositoryMock
            .Setup(s => s.MoveVersionsToBackupAsync(It.IsAny<List<long>>(), backupId, It.IsAny<CancellationToken>()))
            .Callback(callbackExpression);
    }

    private void Mock_FileRepository_DeleteAllVersionsInBackupAsync(long backupId, ErrorOr<int> expectedResult)
    {
        _fileRepositoryMock
            .Setup(s => s.DeleteAllVersionsInBackupAsync(backupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);
    }

    private void Mock_FileRepository_MoveDeletedFilesToNextBackupAsync(long sourceBackupId, long destinationBackupId, ErrorOr<int> expectedResult)
    {
        _fileRepositoryMock
            .Setup(s => s.MoveDeletedFilesToNextBackupAsync(sourceBackupId, destinationBackupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);
    }

    private void Mock_BackupRepository_RefreshTotalsAndSizeAsync(long backupId, ErrorOr<Backup> expectedResult)
    {
        _backupRepositoryMock
            .Setup(s => s.RefreshTotalsAndSizeAsync(backupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);
    }
    
    private void Mock_FileRepository_DeleteFilesWithoutVersionsAsync(ErrorOr<int> expectedResult)
    {
        _fileRepositoryMock
            .Setup(s => s.DeleteFilesWithoutVersionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);
    }

    private void Mock_BackupRepository_DeleteAsync(long backupId, ErrorOr<int> expectedResult)
    {
        _backupRepositoryMock
            .Setup(s => s.DeleteAsync(backupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);
    }

    private List<FileToDeleteDM> CreateListOfFilesToDelete(long fileId, long backupId, int count)
    {
        var filesToDelete = Enumerable.Range(0, count)
            .Select(ix => new FileToDeleteDM()
            {
                Id = ix + 1,
                BackupId = backupId,
                FileId = fileId,
                ExistsInNextBackup = ix % 2 == 0,
                BackupPath = "/home/user/backup",
                RelativePath = $"folder{ix}/",
                FileName = $"file{ix}.txt"
            })
            .ToList();

        return filesToDelete;
    }
}