using ErrorOr;
using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Persistence.Repositories;
using FileKeeper.Core.UseCases;
using FileKeeper.Tests.Integrations.TestDoubles;
using Microsoft.Extensions.Logging;
using Moq;

namespace FileKeeper.Tests.Integrations.Core.UseCases;

public class DeleteBackupUseCaseIntegrationTests
{
    private readonly IBackupRepository _backupRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IDatabaseService _databaseService;
    private readonly Mock<ILogger<DeleteBackupUseCase>> _loggerMock;
    
    private readonly DeleteBackupUseCase _sut;

    public DeleteBackupUseCaseIntegrationTests()
    {
        _databaseService = new InMemorySqliteDatabaseService();
        _databaseService.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult(); // TODO: can this be a problem?
        
        _backupRepository = new BackupRepository(_databaseService);
        _fileRepository = new FileRepository(_databaseService);
        _loggerMock = new Mock<ILogger<DeleteBackupUseCase>>();

        _sut = new DeleteBackupUseCase(
            _backupRepository,
            _fileRepository,
            _databaseService,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBackupDoesNotExist_ReturnsError()
    {
        // Arrange
        var nonExistentBackupId = 999L;
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _sut.ExecuteAsync(nonExistentBackupId, cancellationToken);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoNextBackup_DeletesBackupAndAllVersions()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        var backup = new Backup(1, DateTime.UtcNow, 1, 0, 0, 1024);
        var file = FileModel.CreateNew("/backup", "file.txt", "file.txt");
        var fileVersion = FileVersion.CreateNew(1, 1, true, 512, "hash1", []);

        await _backupRepository.InsertAsync(backup, cancellationToken);
        await _fileRepository.InsertAsync(file, cancellationToken);
        await _fileRepository.InsertVersionAsync(fileVersion, cancellationToken);

        // Act
        var result = await _sut.ExecuteAsync(backup.Id, cancellationToken);
        var deletedBackup = await _backupRepository.GetByIdAsync(backup.Id, cancellationToken);

        // Assert
        Assert.False(result.IsError);
        Assert.True(deletedBackup.IsError);
        Assert.Equal(ErrorType.NotFound, deletedBackup.FirstError.Type);
    }

    [Fact]
    public async Task ExecuteAsync_WithNextBackup_MovesNonExistentVersions()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        var currentTime = DateTime.UtcNow;
        var backup1 = new Backup(1, currentTime, 2, 0, 0, 2048);
        var backup2 = new Backup(2, currentTime.AddHours(1), 0, 0, 0, 0);

        var file1 = FileModel.CreateNew("/backup", "file1.txt", "file1.txt");
        var file2 = FileModel.CreateNew("/backup", "file2.txt", "file2.txt");

        var version1Backup1 = FileVersion.CreateNew(1, 1, true, 512, "hash1", []);
        var version2Backup1 = FileVersion.CreateNew(2, 1, true, 512, "hash2", []);
        var version1Backup2 = FileVersion.CreateNew(1, 2, false, 512, "hash1", []); // Same file, different backup

        await _backupRepository.InsertAsync(backup1, cancellationToken);
        await _backupRepository.InsertAsync(backup2, cancellationToken);
        await _fileRepository.InsertAsync(file1, cancellationToken);
        await _fileRepository.InsertAsync(file2, cancellationToken);
        await _fileRepository.InsertVersionAsync(version1Backup1, cancellationToken);
        await _fileRepository.InsertVersionAsync(version2Backup1, cancellationToken);
        await _fileRepository.InsertVersionAsync(version1Backup2, cancellationToken);

        // Act
        var result = await _sut.ExecuteAsync(backup1.Id, cancellationToken);

        // Assert
        Assert.False(result.IsError);
        var deletedBackup = await _backupRepository.GetByIdAsync(backup1.Id, cancellationToken);
        Assert.True(deletedBackup.IsError);
        Assert.Equal(ErrorType.NotFound, deletedBackup.FirstError.Type);
    }

    [Fact]
    public async Task ExecuteAsync_WithDeletedFilesInCurrentBackup_MovesDeletedFilesToNextBackup()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        var currentTime = DateTime.UtcNow;
        var backup1 = new Backup(1, currentTime, 1, 0, 1, 1024);
        var backup2 = new Backup(2, currentTime.AddHours(1), 0, 0, 0, 0);

        var file = FileModel.CreateNew("/backup", "deletedfile.txt", "deletedfile.txt");
        var fileVersion = FileVersion.CreateNew(1, 1, true, 512, "hash1", []);
        var fileVersion3 = FileVersion.CreateNew(1, 3, true, 512, "hash1", []);

        await _backupRepository.InsertAsync(backup1, cancellationToken);
        await _backupRepository.InsertAsync(backup2, cancellationToken);
        await _fileRepository.InsertAsync(file, cancellationToken);
        await _fileRepository.InsertVersionAsync(fileVersion, cancellationToken);
        await _fileRepository.InsertVersionAsync(fileVersion3, cancellationToken);

        // Act
        var result = await _sut.ExecuteAsync(backup1.Id, cancellationToken);

        // Assert
        Assert.False(result.IsError);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithNoNextBackup_WithAddedUpdatedDeletedFiles_ShouldWork()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        var currentTime = DateTime.UtcNow;
        var backup1 = new Backup(1, currentTime, 2, 0, 0, 2048);
        var backup2 = new Backup(2, currentTime, 2, 0, 0, 2048);

        var file1 = FileModel.CreateNew("/backup", ".", "file1.txt");
        var file2 = FileModel.CreateNew("/backup", ".", "file2.txt");
        
        var deletedFile1 = FileModel.CreateNew("/backup", ".", "file3.txt");
        deletedFile1.UpdateId(3);
        deletedFile1.UpdateDeletedAt(backup2.Id);

        var file1Version1 = FileVersion.CreateNew(1, backup1.Id, true, 512, "hash1", []);
        var file2Version1 = FileVersion.CreateNew(2, backup1.Id, true, 512, "hash2", []);
        var file1Version2 = FileVersion.CreateNew(1, backup2.Id, false, 512, "hash1", []); // Same file, different backup

        var deletedFileVersion1 = FileVersion.CreateNew(deletedFile1.Id, 1, true, 512, "hash1", []);

        await _backupRepository.InsertAsync(backup1, cancellationToken);
        await _backupRepository.InsertAsync(backup2, cancellationToken);
        
        await _fileRepository.InsertAsync(file1, cancellationToken);
        await _fileRepository.InsertAsync(file2, cancellationToken);
        await _fileRepository.InsertAsync(deletedFile1, cancellationToken);
        
        await _fileRepository.InsertVersionAsync(file1Version1, cancellationToken);
        await _fileRepository.InsertVersionAsync(file2Version1, cancellationToken);
        await _fileRepository.InsertVersionAsync(file1Version2, cancellationToken);
        await _fileRepository.InsertVersionAsync(deletedFileVersion1, cancellationToken);

        // Act
        var result = await _sut.ExecuteAsync(backup2.Id, cancellationToken);
        var deletedBackup = await _backupRepository.GetByIdAsync(backup2.Id, cancellationToken);

        // Assert
        Assert.False(result.IsError);
        Assert.True(deletedBackup.IsError);
        Assert.Equal(ErrorType.NotFound, deletedBackup.FirstError.Type);
    }
}