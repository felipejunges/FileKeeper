using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Interfaces.Abstraction;
using FileKeeper.Core.Models;
using FileKeeper.Core.Services;
using FileKeeper.Tests.Builders;
using Moq;
using Spectre.Console;

namespace FileKeeper.Tests.Core.Services;

public class BackupServiceTests
{
    private readonly BackupService _sut;

    private readonly Mock<ICompressionService> _compressionServiceMock;
    private readonly Mock<IIndexService> _indexServiceMock;
    private readonly Mock<IFileSourceService> _fileSourceServiceMock;

    public BackupServiceTests()
    {
        var consoleMock = new Mock<IAnsiConsole>();
        var recycleServiceMock = new Mock<IRecycleService>();
        var configurationServiceMock = new Mock<IConfigurationService>();
        
        _compressionServiceMock = new Mock<ICompressionService>();
        _indexServiceMock = new Mock<IIndexService>();
        _fileSourceServiceMock = new Mock<IFileSourceService>();

        var defaultConfiguration = new Configuration()
        {
            DestinationDirectory = "/home/felipe/backups",
            SourceDirectories = new List<string>() { "/var/www/html" }
        };

        configurationServiceMock
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultConfiguration);

        _sut = new BackupService(
            consoleMock.Object,
            configurationServiceMock.Object,
            _compressionServiceMock.Object,
            recycleServiceMock.Object,
            _indexServiceMock.Object,
            _fileSourceServiceMock.Object);
    }

    [Fact]
    public async Task SemBackup_ETem1ArquivoNaPasta_ResultadoDeveConterOArquivo()
    {
        // Arrange
        BackupIndex? capturedIndex = null;
        string[]? capturedCompressesFiles = null;

        var localFiles = new List<FileMetadata>()
        {
            new FileMetadata
            {
                RelativePath = "file1.txt",
                StoredPath = "L3Zhci93d3cvaHRtbA==/file1.txt",
                Size = 1000,
                LastWriteTimeUtc = DateTime.UtcNow.AddHours(-48),
                Hash = "Hash_File1_V1",
            }
        };
        
        _fileSourceServiceMock
            .Setup(s => s.ScanLocalFolderAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(localFiles);

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
            .Callback<IList<(string FullPath, string StoredPath)>, string, string, CancellationToken>((files, _, _, _) =>
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
            Backups = BackupMetadataBuilder
                .New()
                .AddBackup(DateTime.UtcNow.AddMinutes(-3))
                .AddFile(
                    "file1.txt",
                    "L3Zhci93d3cvaHRtbA==/file1.txt",
                    1000,
                    "Hash_File1_V1",
                    DateTime.UtcNow,
                    currentBackupName)
                .Build()
        };

        BackupIndex? capturedIndex = null;
        string[]? capturedCompressesFiles = null;

        var localFiles = new List<FileMetadata>()
        {
            new FileMetadata
            {
                RelativePath = "file1.txt",
                StoredPath = "L3Zhci93d3cvaHRtbA==/file1.txt",
                Size = 1000,
                LastWriteTimeUtc = DateTime.UtcNow.AddHours(-48),
                Hash = "Hash_File1_V1",
            },
            new FileMetadata
            {
                RelativePath = "file2.txt",
                StoredPath = "L3Zhci93d3cvaHRtbA==/file2.txt",
                Size = 2000,
                LastWriteTimeUtc = DateTime.UtcNow.AddHours(-48),
                Hash = "Hash_File2_V1",
            }
        };
        
        _fileSourceServiceMock
            .Setup(s => s.ScanLocalFolderAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(localFiles);

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
            .Callback<IList<(string FullPath, string StoredPath)>, string, string, CancellationToken>((files, _, _, _) =>
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
        var oldBackupName = DateTime.UtcNow.AddMinutes(-3).ToString("yyyyMMdd_HHmmss");
        var currentBackupName = DateTime.UtcNow.AddMinutes(-2).ToString("yyyyMMdd_HHmmss");

        var currentIndex = new BackupIndex()
        {
            Backups = BackupMetadataBuilder
                .New()
                .AddBackup(DateTime.UtcNow.AddMinutes(-3))
                .AddFile(
                    "file1.txt",
                    "L3Zhci93d3cvaHRtbA==/file1.txt",
                    1000,
                    "Hash_File1_V1",
                    DateTime.UtcNow,
                    oldBackupName)
                .AddBackup(DateTime.UtcNow.AddMinutes(-2))
                .AddFile(
                    "file1.txt",
                    "L3Zhci93d3cvaHRtbA==/file1.txt",
                    1000,
                    "Hash_File1_V1",
                    DateTime.UtcNow,
                    oldBackupName)
                .AddFile(
                    "file2.txt",
                    "L3Zhci93d3cvaHRtbA==/file2.txt",
                    1000,
                    "Hash_File2_V1",
                    DateTime.UtcNow,
                    currentBackupName)
                .Build()
        };

        BackupIndex? capturedIndex = null;
        string[]? capturedCompressesFiles = null;

        var localFiles = new List<FileMetadata>()
        {
            new FileMetadata
            {
                RelativePath = "file1.txt",
                StoredPath = "L3Zhci93d3cvaHRtbA==/file1.txt",
                Size = 1000,
                LastWriteTimeUtc = DateTime.UtcNow.AddHours(-48),
                Hash = "Hash_File1_V1",
            },
            new FileMetadata
            {
                RelativePath = "file2.txt",
                StoredPath = "L3Zhci93d3cvaHRtbA==/file2.txt",
                Size = 2000,
                LastWriteTimeUtc = DateTime.UtcNow.AddHours(-48),
                Hash = "Hash_File2_V2", // V2
            },
            new FileMetadata
            {
                RelativePath = "file3.txt",
                StoredPath = "L3Zhci93d3cvaHRtbA==/file3.txt",
                Size = 3000,
                LastWriteTimeUtc = DateTime.UtcNow.AddHours(-48),
                Hash = "Hash_File3_V1",
            }
        };
        
        _fileSourceServiceMock
            .Setup(s => s.ScanLocalFolderAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(localFiles);

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
            .Callback<IList<(string FullPath, string StoredPath)>, string, string, CancellationToken>((files, _, _, _) =>
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