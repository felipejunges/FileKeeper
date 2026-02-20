using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Interfaces.Abstraction;
using FileKeeper.Core.Models;
using FileKeeper.Core.Services;
using FileKeeper.Tests.Builders;
using FileKeeper.Tests.Core.Mocks;
using FileKeeper.Tests.Core.Mocks.Models;
using Moq;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.IO;
using Xunit;

namespace FileKeeper.Tests.Core.Services;

public class IntegrityServiceTests
{
    private readonly List<string> _consoleOutputs = new();
    private readonly IntegrityService _sut;
    private readonly List<MockedFile> _mockedFiles = new();
    private readonly IFileSystem _fileSystem;
    private readonly Mock<IAnsiConsole> _consoleMock;
    private readonly Mock<ICompressionService> _compressionServiceMock;
    private readonly Mock<IConfigurationService> _configurationServiceMock;
    private readonly Mock<IIndexService> _indexServiceMock;
    private readonly Mock<IFileSourceService> _fileSourceServiceMock;

    public IntegrityServiceTests()
    {
        _fileSystem = new MockedFileSystem(_mockedFiles);
        _consoleMock = new Mock<IAnsiConsole>();
        _compressionServiceMock = new Mock<ICompressionService>();
        _configurationServiceMock = new Mock<IConfigurationService>();
        _indexServiceMock = new Mock<IIndexService>();
        _fileSourceServiceMock = new Mock<IFileSourceService>();

        var writer = new StringWriter();
        var tempConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer)
        });

        _consoleMock.Setup(c => c.Write(It.IsAny<IRenderable>()))
            .Callback<IRenderable>(r =>
            {
                tempConsole.Write(r);
                var output = writer.ToString();
                _consoleOutputs.Add(output);
                writer.GetStringBuilder().Clear();
            });

        var defaultConfiguration = new Configuration()
        {
            DestinationDirectory = "/home/felipe/backups"
        };

        _configurationServiceMock
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultConfiguration);

        _fileSourceServiceMock
            .Setup(s => s.ScanLocalFolderAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FileMetadata>());

        _sut = new IntegrityService(
            _consoleMock.Object,
            _configurationServiceMock.Object,
            _compressionServiceMock.Object,
            _indexServiceMock.Object,
            _fileSourceServiceMock.Object);
    }

    [Fact]
    public async Task VerifyCompressedFilesIntegrityAsync_WhenIndexMatchesZipEntries_ReturnsSuccessAndReportsSuccess()
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
        var result = await _sut.VerifyCompressedFilesIntegrityAsync(CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.Contains(_consoleOutputs, s => s.Contains("Integrity Verification Successful"));
    }

    [Fact]
    public async Task VerifyCompressedFilesIntegrityAsync_WhenFileIsMissingInZip_ReportsCritical()
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
        var result = await _sut.VerifyCompressedFilesIntegrityAsync(CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.Contains(_consoleOutputs, s => s.Contains("CRITICAL"));
    }

    [Fact]
    public async Task VerifyCompressedFilesIntegrityAsync_WhenExtraFileInZip_ReportsWarning()
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
        var result = await _sut.VerifyCompressedFilesIntegrityAsync(CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.Contains(_consoleOutputs, s => s.Contains("WARNING"));
    }

    [Fact]
    public async Task VerifyLocalFilesDifferencesAsync_WhenLocalFileIsMissing_ReportsMissingLocally()
    {
        // Arrange
        var sourceDir = "/home/felipe/data";
        var sourceDirBase64 = "L2hvbWUvZmVsaXBlL2RhdGE=";
        var config = new Configuration
        {
            DestinationDirectory = "/home/felipe/backups",
            SourceDirectories = new List<string> { sourceDir }
        };
        _configurationServiceMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var index = new BackupIndex
        {
            Backups = new List<BackupMetadata> {
                new BackupMetadata {
                    BackupName = "20240101_100000",
                    CreatedAtUtc = DateTime.UtcNow
                }
            }
        };
        index.Backups[0].AddFile(new FileMetadata { StoredPath = $"{sourceDirBase64}/file1.txt", RelativePath = "file1.txt", FoundInBackup = "20240101_100000" });
        _indexServiceMock.Setup(s => s.GetBackupIndexAsync(It.IsAny<CancellationToken>())).ReturnsAsync(index);

        _fileSourceServiceMock.Setup(s => s.ScanLocalFolderAsync(sourceDir, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FileMetadata>());

        // Act
        var result = await _sut.VerifyLocalFilesDifferencesAsync(CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.Contains(_consoleOutputs, s => s.Contains("Missing locally"));
    }

    [Fact]
    public async Task VerifyLocalFilesDifferencesAsync_WhenLocalFileIsUntracked_ReportsUntrackedLocally()
    {
        // Arrange
        var sourceDir = "/home/felipe/data";
        var sourceDirBase64 = "L2hvbWUvZmVsaXBlL2RhdGE=";
        var config = new Configuration
        {
            DestinationDirectory = "/home/felipe/backups",
            SourceDirectories = new List<string> { sourceDir }
        };
        _configurationServiceMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var index = new BackupIndex
        {
            Backups = new List<BackupMetadata> {
                new BackupMetadata { BackupName = "20240101_100000", CreatedAtUtc = DateTime.UtcNow }
            }
        };
        _indexServiceMock.Setup(s => s.GetBackupIndexAsync(It.IsAny<CancellationToken>())).ReturnsAsync(index);

        _fileSourceServiceMock.Setup(s => s.ScanLocalFolderAsync(sourceDir, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FileMetadata> {
                new FileMetadata { StoredPath = $"{sourceDirBase64}/newfile.txt", RelativePath = "newfile.txt" }
            });

        // Act
        var result = await _sut.VerifyLocalFilesDifferencesAsync(CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.Contains(_consoleOutputs, s => s.Contains("Untracked locally"));
    }

    [Fact]
    public async Task VerifyCompressedFilesIntegrityAsync_WhenDestinationDirectoryNotSet_ReturnsError()
    {
        // Arrange
        var config = new Configuration { DestinationDirectory = "" };
        _configurationServiceMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(config);

        // Act
        var result = await _sut.VerifyCompressedFilesIntegrityAsync(CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains(_consoleOutputs, s => s.Contains("Destination directory not set"));
    }

    [Fact]
    public async Task VerifyCompressedFilesIntegrityAsync_WhenNoBackupsFound_ReturnsSuccess()
    {
        // Arrange
        var index = new BackupIndex { Backups = new List<BackupMetadata>() };
        _indexServiceMock.Setup(s => s.GetBackupIndexAsync(It.IsAny<CancellationToken>())).ReturnsAsync(index);

        // Act
        var result = await _sut.VerifyCompressedFilesIntegrityAsync(CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.Contains(_consoleOutputs, s => s.Contains("No backups found in index"));
    }

    [Fact]
    public async Task VerifyLocalFilesDifferencesAsync_WhenDestinationDirectoryNotSet_ReturnsError()
    {
        // Arrange
        var config = new Configuration { DestinationDirectory = "" };
        _configurationServiceMock.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(config);

        // Act
        var result = await _sut.VerifyLocalFilesDifferencesAsync(CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains(_consoleOutputs, s => s.Contains("Destination directory not set"));
    }

    [Fact]
    public async Task VerifyLocalFilesDifferencesAsync_WhenNoBackupsFound_ReturnsSuccess()
    {
        // Arrange
        var index = new BackupIndex { Backups = new List<BackupMetadata>() };
        _indexServiceMock.Setup(s => s.GetBackupIndexAsync(It.IsAny<CancellationToken>())).ReturnsAsync(index);

        // Act
        var result = await _sut.VerifyLocalFilesDifferencesAsync(CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.Contains(_consoleOutputs, s => s.Contains("No backups found in index"));
    }
}
