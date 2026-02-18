using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Interfaces.Abstraction;
using FileKeeper.Core.Models;
using FileKeeper.Core.Services;
using FileKeeper.Tests.Builders;
using Moq;
using Spectre.Console;

namespace FileKeeper.Tests.Core.Services;

public class RecycleServiceTests
{
    private readonly RecycleService _sut;

    private readonly Mock<IAnsiConsole> _consoleMock;
    private readonly Mock<ICompressionService> _compressionServiceMock;
    private readonly Mock<IConfigurationService> _configurationServiceMock;
    private readonly Mock<IIndexService> _indexServiceMock;

    public RecycleServiceTests()
    {
        _consoleMock = new Mock<IAnsiConsole>();
        _compressionServiceMock = new Mock<ICompressionService>();
        _configurationServiceMock = new Mock<IConfigurationService>();
        _indexServiceMock = new Mock<IIndexService>();

        var defaultConfiguration = new Configuration()
        {
            DestinationDirectory = "/home/felipe/backups",
            SourceDirectories = new List<string>() { "/var/www/html" },
            KeepMaxBackups = 3
        };

        _configurationServiceMock
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultConfiguration);

        _sut = new RecycleService(
            _consoleMock.Object,
            _configurationServiceMock.Object,
            _compressionServiceMock.Object,
            _indexServiceMock.Object);
    }

    [Fact]
    public async Task IfConfigurationIsZero_RecycleShouldSkip()
    {
        // Arrange
        var configuration = new Configuration()
        {
            DestinationDirectory = "/home/felipe/backups",
            SourceDirectories = new List<string>() { "/var/www/html" },
            KeepMaxBackups = 0
        };

        _configurationServiceMock
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);

        // Act
        await _sut.RecycleBackupsAsync(CancellationToken.None);

        // Assert
        _indexServiceMock
            .Verify(v => v.GetBackupIndexAsync(
                    It.IsAny<CancellationToken>()),
                Times.Never);
        
        _compressionServiceMock
            .Verify(v => v.MoveFileAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>())
                , Times.Never);

        _compressionServiceMock
            .Verify(v => v.RemoveFolderAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

        _indexServiceMock
            .Verify(v => v.SaveBackupIndexAsync(
                    It.IsAny<BackupIndex>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
    }
    
    [Fact]
    public async Task IfConfigurationIsMoreThanCurrentBackups_DoNothing()
    {
        // Arrange
        var configuration = new Configuration()
        {
            DestinationDirectory = "/home/felipe/backups",
            SourceDirectories = new List<string>() { "/var/www/html" },
            KeepMaxBackups = 3
        };

        _configurationServiceMock
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);
        
        var currentBackupName = DateTime.UtcNow.AddMinutes(-3).ToString("yyyyMMdd_HHmmss");

        var currentIndex = new BackupIndex()
        {
            Backups = BackupMetadataBuilder
                .New()
                .AddBackup(DateTime.UtcNow.AddMinutes(-3))
                .AddFile(
                    "file1.txt",
                    "a8512aa711f757608fdac530f82cd972616f8c6d/html/file1.txt",
                    1000,
                    "Hash_File1_V1",
                    DateTime.UtcNow,
                    currentBackupName)
                .Build()
        };
        
        _indexServiceMock
            .Setup(v => v.GetBackupIndexAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentIndex);

        // Act
        await _sut.RecycleBackupsAsync(CancellationToken.None);

        // Assert
        _indexServiceMock
            .Verify(v => v.GetBackupIndexAsync(
                    It.IsAny<CancellationToken>()),
                Times.Once);
        
        _compressionServiceMock
            .Verify(v => v.MoveFileAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>())
                , Times.Never);

        _compressionServiceMock
            .Verify(v => v.RemoveFolderAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

        _indexServiceMock
            .Verify(v => v.SaveBackupIndexAsync(
                    It.IsAny<BackupIndex>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
    }
    
    [Fact]
    public async Task ThreeBackups_OneIsRecycled()
    {
        // Arrange
        var configuration = new Configuration()
        {
            DestinationDirectory = "/home/felipe/backups",
            SourceDirectories = new List<string>() { "/var/www/html" },
            KeepMaxBackups = 2
        };

        _configurationServiceMock
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);
        
        var firstBackupDate = new DateTime(2026, 2, 18, 10, 0, 0, DateTimeKind.Utc);
        var secondBackupDate = new DateTime(2026, 2, 18, 10, 10, 0, DateTimeKind.Utc);
        var thirdBackupDate = new DateTime(2026, 2, 18, 10, 20, 0, DateTimeKind.Utc);

        var currentIndex = new BackupIndex()
        {
            Backups = BackupMetadataBuilder
                .New()
                .AddBackup(firstBackupDate)
                .AddFile(
                    "file1.txt",
                    "a8512aa711f757608fdac530f82cd972616f8c6d/html/file1.txt",
                    1000,
                    "Hash_File1_V1",
                    DateTime.UtcNow,
                    firstBackupDate.ToString("yyyyMMdd_HHmmss"))
                .AddBackup(secondBackupDate)
                .AddFile(
                    "file1.txt",
                    "a8512aa711f757608fdac530f82cd972616f8c6d/html/file1.txt",
                    1000,
                    "Hash_File1_V1",
                    DateTime.UtcNow,
                    firstBackupDate.ToString("yyyyMMdd_HHmmss"))
                .AddFile(
                    "file2.txt",
                    "a8512aa711f757608fdac530f82cd972616f8c6d/html/file2.txt",
                    2000,
                    "Hash_File2_V1",
                    DateTime.UtcNow,
                    secondBackupDate.ToString("yyyyMMdd_HHmmss"))
                .AddBackup(thirdBackupDate)
                .AddFile(
                    "file1.txt",
                    "a8512aa711f757608fdac530f82cd972616f8c6d/html/file1.txt",
                    1000,
                    "Hash_File1_V1",
                    DateTime.UtcNow,
                    firstBackupDate.ToString("yyyyMMdd_HHmmss"))
                .AddFile(
                    "file2.txt",
                    "a8512aa711f757608fdac530f82cd972616f8c6d/html/file2.txt",
                    2000,
                    "Hash_File2_V1",
                    DateTime.UtcNow,
                    secondBackupDate.ToString("yyyyMMdd_HHmmss"))
                .AddFile(
                    "file3.txt",
                    "a8512aa711f757608fdac530f82cd972616f8c6d/html/file3.txt",
                    2000,
                    "Hash_File3_V1",
                    DateTime.UtcNow,
                    thirdBackupDate.ToString("yyyyMMdd_HHmmss"))
                .Build()
        };
        
        _indexServiceMock
            .Setup(v => v.GetBackupIndexAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentIndex);

        BackupIndex? capturedIndex = null;
        _indexServiceMock
            .Setup(s => s.SaveBackupIndexAsync(It.IsAny<BackupIndex>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<BackupIndex, CancellationToken>((index, _) => { capturedIndex = index; });
        
        // Act
        await _sut.RecycleBackupsAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, capturedIndex!.Backups.Count);
        
        var firstBackup = capturedIndex!.Backups[0];
        var secondBackup = capturedIndex!.Backups[1];
        
        Assert.Equal(2, firstBackup.Files.Count);
        Assert.Equal(3, secondBackup.Files.Count);
        
        Assert.Equal(firstBackup.BackupName, firstBackup.Files[0].FoundInBackup);
        Assert.Equal(firstBackup.BackupName, firstBackup.Files[1].FoundInBackup);
        
        Assert.Equal(firstBackup.BackupName, secondBackup.Files[0].FoundInBackup);
        Assert.Equal(firstBackup.BackupName, secondBackup.Files[1].FoundInBackup);
        Assert.Equal(secondBackup.BackupName, secondBackup.Files[2].FoundInBackup);
        
        _indexServiceMock
            .Verify(v => v.SaveBackupIndexAsync(
                    It.IsAny<BackupIndex>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
    }
    
    [Fact]
    public async Task ThreeBackups_OneIsRecycled_ButInThirdFile1IsNewer()
    {
        // Arrange
        var configuration = new Configuration()
        {
            DestinationDirectory = "/home/felipe/backups",
            SourceDirectories = new List<string>() { "/var/www/html" },
            KeepMaxBackups = 2
        };

        _configurationServiceMock
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);
        
        var firstBackupDate = new DateTime(2026, 2, 18, 10, 0, 0, DateTimeKind.Utc);
        var secondBackupDate = new DateTime(2026, 2, 18, 10, 10, 0, DateTimeKind.Utc);
        var thirdBackupDate = new DateTime(2026, 2, 18, 10, 20, 0, DateTimeKind.Utc);

        var currentIndex = new BackupIndex()
        {
            Backups = BackupMetadataBuilder
                .New()
                .AddBackup(firstBackupDate)
                .AddFile(
                    "file1.txt",
                    "a8512aa711f757608fdac530f82cd972616f8c6d/html/file1.txt",
                    1000,
                    "Hash_File1_V1",
                    DateTime.UtcNow,
                    firstBackupDate.ToString("yyyyMMdd_HHmmss"))
                .AddBackup(secondBackupDate)
                .AddFile(
                    "file1.txt",
                    "a8512aa711f757608fdac530f82cd972616f8c6d/html/file1.txt",
                    1000,
                    "Hash_File1_V1",
                    DateTime.UtcNow,
                    firstBackupDate.ToString("yyyyMMdd_HHmmss"))
                .AddFile(
                    "file2.txt",
                    "a8512aa711f757608fdac530f82cd972616f8c6d/html/file2.txt",
                    2000,
                    "Hash_File2_V1",
                    DateTime.UtcNow,
                    secondBackupDate.ToString("yyyyMMdd_HHmmss"))
                .AddBackup(thirdBackupDate)
                .AddFile(
                    "file1.txt",
                    "a8512aa711f757608fdac530f82cd972616f8c6d/html/file1.txt",
                    1000,
                    "Hash_File1_V2",
                    DateTime.UtcNow,
                    thirdBackupDate.ToString("yyyyMMdd_HHmmss"))
                .AddFile(
                    "file2.txt",
                    "a8512aa711f757608fdac530f82cd972616f8c6d/html/file2.txt",
                    2000,
                    "Hash_File2_V1",
                    DateTime.UtcNow,
                    secondBackupDate.ToString("yyyyMMdd_HHmmss"))
                .AddFile(
                    "file3.txt",
                    "a8512aa711f757608fdac530f82cd972616f8c6d/html/file3.txt",
                    2000,
                    "Hash_File3_V1",
                    DateTime.UtcNow,
                    thirdBackupDate.ToString("yyyyMMdd_HHmmss"))
                .Build()
        };
        
        _indexServiceMock
            .Setup(v => v.GetBackupIndexAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentIndex);

        BackupIndex? capturedIndex = null;
        _indexServiceMock
            .Setup(s => s.SaveBackupIndexAsync(It.IsAny<BackupIndex>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<BackupIndex, CancellationToken>((index, _) => { capturedIndex = index; });
        
        // Act
        await _sut.RecycleBackupsAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, capturedIndex!.Backups.Count);
        
        var firstBackup = capturedIndex!.Backups[0];
        var secondBackup = capturedIndex!.Backups[1];
        
        Assert.Equal(2, firstBackup.Files.Count);
        Assert.Equal(3, secondBackup.Files.Count);
        
        Assert.Equal(firstBackup.BackupName, firstBackup.Files[0].FoundInBackup);
        Assert.Equal(firstBackup.BackupName, firstBackup.Files[1].FoundInBackup);
        
        Assert.Equal(secondBackup.BackupName, secondBackup.Files[0].FoundInBackup);
        Assert.Equal(firstBackup.BackupName, secondBackup.Files[1].FoundInBackup);
        Assert.Equal(secondBackup.BackupName, secondBackup.Files[2].FoundInBackup);
        
        _indexServiceMock
            .Verify(v => v.SaveBackupIndexAsync(
                    It.IsAny<BackupIndex>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
    }
}