using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Interfaces.Abstraction;
using FileKeeper.Core.Interfaces.Abstraction.Info;
using FileKeeper.Core.Models;
using FileKeeper.Core.Services;
using FileKeeper.Tests.Core.Mocks;
using FileKeeper.Tests.Core.Mocks.Models;
using Moq;
using Spectre.Console;

namespace FileKeeper.Tests.Core.Services;

public class BackupServiceTests
{
    private readonly BackupService _sut;

    private List<MockedFile> _mockedFiles = new();

    private readonly IFileSystem _fileSystem;
    private readonly IFileInfoBuilder _fileInfoBuilder;
    private readonly IHashingService _hashingService;

    private readonly Mock<IAnsiConsole> _consoleMock;
    private readonly Mock<ICompressionService> _compressionServiceMock;
    private readonly Mock<IRecycleService> _recycleServiceMock;
    private readonly Mock<IConfigurationService> _configurationServiceMock;
    private readonly Mock<IIndexService> _indexServiceMock;

    public BackupServiceTests()
    {
        _fileSystem = new MockedFileSystem(_mockedFiles);
        _fileInfoBuilder = new MockedFileInfoBuilder(_mockedFiles);
        _hashingService = new MockedHashingService(_mockedFiles);

        _consoleMock = new Mock<IAnsiConsole>();
        _compressionServiceMock = new Mock<ICompressionService>();
        _recycleServiceMock = new Mock<IRecycleService>();
        _configurationServiceMock = new Mock<IConfigurationService>();
        _indexServiceMock = new Mock<IIndexService>();

        var defaultConfiguration = new Configuration()
        {
            DestinationDirectory = "/home/felipe/backups",
            SourceDirectories = new List<string>() { "/var/www/html" }
        };

        _configurationServiceMock
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultConfiguration);

        _sut = new BackupService(
            _consoleMock.Object,
            _configurationServiceMock.Object,
            _fileSystem,
            _hashingService,
            _compressionServiceMock.Object,
            _recycleServiceMock.Object,
            _fileInfoBuilder,
            _indexServiceMock.Object);
    }

    [Fact]
    public async Task SemBackup_ETem1ArquivoNaPasta_ResultadoDeveConterOArquivo()
    {
        // Arrange
        BackupIndex? capturedIndex = null;
        string[]? capturedCompressesFiles = null;

        var mockedFiles = new List<MockedFile>
        {
            new MockedFile(
                "file1.txt",
                "/var/www/html/file1.txt",
                1000,
                "Hash_File1_V1")
        };

        _mockedFiles.Clear();
        _mockedFiles.AddRange(mockedFiles);

        _indexServiceMock
            .Setup(s => s.GetBackupIndexAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BackupIndex());

        _indexServiceMock
            .Setup(s => s.SaveBackupIndexAsync(It.IsAny<BackupIndex>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<BackupIndex, CancellationToken>((index, _) => { capturedIndex = index; });

        _compressionServiceMock
            .Setup(s => s.ReadFileContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _compressionServiceMock
            .Setup(s => s.CompressFilesAsync(It.IsAny<IList<(string FullPath, string StoredPath)>>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<IList<(string FullPath, string StoredPath)>, string, string, CancellationToken>((files, backupPath, backupName, ct) =>
            {
                capturedCompressesFiles = files.Select(f => f.FullPath).ToArray();
            });
        
        // Act
        var result = await _sut.CreateBackupAsync(CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        Assert.NotNull(capturedIndex);
        Assert.Single(capturedIndex.Backups);
        Assert.Single(capturedIndex.Backups.First().Files);
        
        Assert.Single(capturedCompressesFiles!);
        Assert.Contains("/var/www/html/file1.txt", capturedCompressesFiles!);
    }

    [Fact]
    public async Task BackupCom1Arquivo_NaPastaOutroEhModificado_ResultadoDeveConterOsDois()
    {
        // Arrange
        var currentBackupName = DateTime.UtcNow.AddMinutes(-3).ToString("yyyyMMdd_HHmmss");

        var currentIndex = new BackupIndex()
        {
            Backups = new List<BackupMetadata>()
            {
                new BackupMetadata()
                {
                    BackupName = currentBackupName,
                    CreatedAtUtc = new DateTime(2024, 1, 1, 12, 0, 0),
                    FileCount = 1,
                    TotalSize = 1000,
                    FilesSerialization = new List<FileMetadata>()
                    {
                        new FileMetadata()
                        {
                            RelativePath = "file1.txt",
                            StoredPath = "a8512aa711f757608fdac530f82cd972616f8c6d/html/file1.txt",
                            Hash = "Hash_File1_V1",
                            Size = 1000,
                            LastWriteTimeUtc = new DateTime(2024, 1, 1, 11, 0, 0),
                            FoundInBackup = currentBackupName
                        }
                    }
                }
            }
        };

        BackupIndex? capturedIndex = null;
        string[]? capturedCompressesFiles = null;

        var mockedFiles = new List<MockedFile>
        {
            new MockedFile(
                "file1.txt",
                "/var/www/html/file1.txt",
                1000,
                "Hash_File1_V1"),
            new MockedFile(
                "file2.txt",
                "/var/www/html/file2.txt",
                2000,
                "Hash_File2_V1"),
        };

        _mockedFiles.Clear();
        _mockedFiles.AddRange(mockedFiles);

        _indexServiceMock
            .Setup(s => s.GetBackupIndexAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentIndex);

        _indexServiceMock
            .Setup(s => s.SaveBackupIndexAsync(It.IsAny<BackupIndex>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<BackupIndex, CancellationToken>((index, _) => { capturedIndex = index; });

        _compressionServiceMock
            .Setup(s => s.ReadFileContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _compressionServiceMock
            .Setup(s => s.CompressFilesAsync(It.IsAny<IList<(string FullPath, string StoredPath)>>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<IList<(string FullPath, string StoredPath)>, string, string, CancellationToken>((files, backupPath, backupName, ct) =>
            {
                capturedCompressesFiles = files.Select(f => f.FullPath).ToArray();
            });

        // Act
        var result = await _sut.CreateBackupAsync(CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.NotNull(capturedIndex);

        Assert.Equal(2, capturedIndex.Backups.Count);
        
        var firstBackup = capturedIndex!.Backups[0];
        var secondBackup = capturedIndex!.Backups[1];

        Assert.Single(firstBackup.Files);
        Assert.Equal(2, secondBackup.Files.Count);

        Assert.Equal(firstBackup.BackupName, firstBackup.Files[0].FoundInBackup);
        Assert.Equal(firstBackup.BackupName, secondBackup.Files[0].FoundInBackup);
        Assert.Equal(secondBackup.BackupName, secondBackup.Files[1].FoundInBackup);

        Assert.Single(capturedCompressesFiles!);
        Assert.Contains("/var/www/html/file2.txt", capturedCompressesFiles!);
    }
    
    [Fact]
    public async Task BackupCom2Arquivos_NaPasta1Novo1Modificado1Igual_ResultadoDeveConterOsTresMasDoisModificados()
    {
        // Arrange
        var oldBackupName = DateTime.UtcNow.AddMinutes(-4).ToString("yyyyMMdd_HHmmss");
        var currentBackupName = DateTime.UtcNow.AddMinutes(-3).ToString("yyyyMMdd_HHmmss");

        var currentIndex = new BackupIndex()
        {
            Backups = new List<BackupMetadata>()
            {
                new BackupMetadata()
                {
                    BackupName = oldBackupName,
                    CreatedAtUtc = new DateTime(2024, 1, 1, 12, 0, 0),
                    FileCount = 1,
                    TotalSize = 1000,
                    FilesSerialization = new List<FileMetadata>()
                    {
                        new FileMetadata()
                        {
                            RelativePath = "file1.txt",
                            StoredPath = "a8512aa711f757608fdac530f82cd972616f8c6d/html/file1.txt",
                            Hash = "Hash_File1_V1",
                            Size = 1000,
                            LastWriteTimeUtc = new DateTime(2024, 1, 1, 11, 0, 0),
                            FoundInBackup = oldBackupName
                        }
                    }
                },
                new BackupMetadata()
                {
                    BackupName = currentBackupName,
                    CreatedAtUtc = new DateTime(2024, 1, 2, 12, 0, 0),
                    FileCount = 1,
                    TotalSize = 1000,
                    FilesSerialization = new List<FileMetadata>()
                    {
                        new FileMetadata()
                        {
                            RelativePath = "file1.txt",
                            StoredPath = "a8512aa711f757608fdac530f82cd972616f8c6d/html/file1.txt",
                            Hash = "Hash_File1_V1",
                            Size = 1000,
                            LastWriteTimeUtc = new DateTime(2024, 1, 1, 11, 0, 0),
                            FoundInBackup = oldBackupName
                        },
                        new FileMetadata()
                        {
                            RelativePath = "file2.txt",
                            StoredPath = "a8512aa711f757608fdac530f82cd972616f8c6d/html/file2.txt",
                            Hash = "Hash_File2_V1",
                            Size = 2000,
                            LastWriteTimeUtc = new DateTime(2024, 1, 1, 11, 0, 0),
                            FoundInBackup = currentBackupName
                        }
                    }
                }
            }
        };

        BackupIndex? capturedIndex = null;
        string[]? capturedCompressesFiles = null;

        var mockedFiles = new List<MockedFile>
        {
            new MockedFile(
                "file1.txt",
                "/var/www/html/file1.txt",
                1000,
                "Hash_File1_V1"),
            new MockedFile(
                "file2.txt",
                "/var/www/html/file2.txt",
                2000,
                "Hash_File2_V2"), // V2
            new MockedFile(
                "file3.txt",
                "/var/www/html/file3.txt",
                3000,
                "Hash_File3_V1"),
        };

        _mockedFiles.Clear();
        _mockedFiles.AddRange(mockedFiles);

        _indexServiceMock
            .Setup(s => s.GetBackupIndexAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentIndex);

        _indexServiceMock
            .Setup(s => s.SaveBackupIndexAsync(It.IsAny<BackupIndex>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<BackupIndex, CancellationToken>((index, _) => { capturedIndex = index; });

        _compressionServiceMock
            .Setup(s => s.ReadFileContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _compressionServiceMock
            .Setup(s => s.CompressFilesAsync(It.IsAny<IList<(string FullPath, string StoredPath)>>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<IList<(string FullPath, string StoredPath)>, string, string, CancellationToken>((files, backupPath, backupName, ct) =>
            {
                capturedCompressesFiles = files.Select(f => f.FullPath).ToArray();
            });

        // Act
        var result = await _sut.CreateBackupAsync(CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.NotNull(capturedIndex);

        Assert.Equal(3, capturedIndex.Backups.Count);
        
        var firstBackup = capturedIndex!.Backups[0];
        var secondBackup = capturedIndex!.Backups[1];
        var thirdBackup = capturedIndex!.Backups[2];

        Assert.Single(firstBackup.Files);
        Assert.Equal(2, secondBackup.Files.Count);
        Assert.Equal(3, thirdBackup.Files.Count);

        Assert.Equal(firstBackup.BackupName, firstBackup.Files[0].FoundInBackup);
        Assert.Equal(firstBackup.BackupName, secondBackup.Files[0].FoundInBackup);
        Assert.Equal(secondBackup.BackupName, secondBackup.Files[1].FoundInBackup);

        Assert.Equal(2, capturedCompressesFiles!.Length);
        Assert.Contains("/var/www/html/file2.txt", capturedCompressesFiles!);
        Assert.Contains("/var/www/html/file3.txt", capturedCompressesFiles!);
    }
}