using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Interfaces.Abstraction;
using FileKeeper.Core.Models;
using FileKeeper.Core.Services;
using FileKeeper.Tests.Builders;
using FileKeeper.Tests.Core.Mocks;
using FileKeeper.Tests.Core.Mocks.Models;
using Moq;
using Spectre.Console;
using Xunit;

namespace FileKeeper.Tests.Core.Services;

public class IntegrityServiceTests
{
    private readonly IntegrityService _sut;
    private readonly List<MockedFile> _mockedFiles = new();
    private readonly IFileSystem _fileSystem;
    private readonly Mock<IAnsiConsole> _consoleMock;
    private readonly Mock<ICompressionService> _compressionServiceMock;
    private readonly Mock<IConfigurationService> _configurationServiceMock;
    private readonly Mock<IIndexService> _indexServiceMock;

    public IntegrityServiceTests()
    {
        _fileSystem = new MockedFileSystem(_mockedFiles);
        _consoleMock = new Mock<IAnsiConsole>();
        _compressionServiceMock = new Mock<ICompressionService>();
        _configurationServiceMock = new Mock<IConfigurationService>();
        _indexServiceMock = new Mock<IIndexService>();

        var defaultConfiguration = new Configuration()
        {
            DestinationDirectory = "/home/felipe/backups"
        };

        _configurationServiceMock
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultConfiguration);

        _sut = new IntegrityService(
            _consoleMock.Object,
            _configurationServiceMock.Object,
            _compressionServiceMock.Object,
            _indexServiceMock.Object,
            _fileSystem);
    }

    [Fact]
    public async Task VerifyIntegrityAsync_WhenIndexMatchesZipEntries_ReturnsSuccessAndReportsSuccess()
    {
        // Arrange
        var backupName = "20240101_100000";
        var index = new BackupIndex
        {
            Backups = BackupMetadataBuilder.New()
                .AddBackup(DateTime.Parse("2024-01-01 10:00:00"))
                .AddFile("file1.txt", "base64path/file1.txt", 100, "hash1", DateTime.Now, backupName)
                .Build()
        };

        _indexServiceMock.Setup(s => s.GetBackupIndexAsync(It.IsAny<CancellationToken>())).ReturnsAsync(index);

        _mockedFiles.Add(new MockedFile("backup.zip", "/home/felipe/backups/backup.zip", 1000, "hashzip"));

        var zipEntries = new List<string> { "20240101_100000/base64path/file1.txt", "index.json" };
        _compressionServiceMock.Setup(s => s.GetEntriesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(zipEntries);

        // Act
        var result = await _sut.VerifyIntegrityAsync(CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        _consoleMock.Verify(c => c.MarkupLine(It.Is<string>(s => s.Contains("Integrity Verification Successful"))), Times.Once);
    }

    [Fact]
    public async Task VerifyIntegrityAsync_WhenFileIsMissingInZip_ReportsCritical()
    {
        // Arrange
        var backupName = "20240101_100000";
        var index = new BackupIndex
        {
            Backups = BackupMetadataBuilder.New()
                .AddBackup(DateTime.Parse("2024-01-01 10:00:00"))
                .AddFile("file1.txt", "base64path/file1.txt", 100, "hash1", DateTime.Now, backupName)
                .Build()
        };

        _indexServiceMock.Setup(s => s.GetBackupIndexAsync(It.IsAny<CancellationToken>())).ReturnsAsync(index);
        _mockedFiles.Add(new MockedFile("backup.zip", "/home/felipe/backups/backup.zip", 1000, "hashzip"));

        var zipEntries = new List<string> { "index.json" }; // file1.txt is missing
        _compressionServiceMock.Setup(s => s.GetEntriesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(zipEntries);

        // Act
        var result = await _sut.VerifyIntegrityAsync(CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        _consoleMock.Verify(c => c.MarkupLine(It.Is<string>(s => s.Contains("CRITICAL"))), Times.Once);
    }

    [Fact]
    public async Task VerifyIntegrityAsync_WhenExtraFileInZip_ReportsWarning()
    {
        // Arrange
        var backupName = "20240101_100000";
        var index = new BackupIndex
        {
            Backups = BackupMetadataBuilder.New()
                .AddBackup(DateTime.Parse("2024-01-01 10:00:00"))
                .AddFile("file1.txt", "base64path/file1.txt", 100, "hash1", DateTime.Now, backupName)
                .Build()
        };

        _indexServiceMock.Setup(s => s.GetBackupIndexAsync(It.IsAny<CancellationToken>())).ReturnsAsync(index);
        _mockedFiles.Add(new MockedFile("backup.zip", "/home/felipe/backups/backup.zip", 1000, "hashzip"));

        var zipEntries = new List<string> {
            "20240101_100000/base64path/file1.txt",
            "20240101_100000/extra.txt", // Extra
            "index.json"
        };
        _compressionServiceMock.Setup(s => s.GetEntriesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(zipEntries);

        // Act
        var result = await _sut.VerifyIntegrityAsync(CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        _consoleMock.Verify(c => c.MarkupLine(It.Is<string>(s => s.Contains("WARNING"))), Times.Once);
    }
}
