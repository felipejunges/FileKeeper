using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Interfaces.Abstraction;
using FileKeeper.Core.Interfaces.Abstraction.Info;
using FileKeeper.Core.Models;
using FileKeeper.Core.Services;
using FileKeeper.Tests.Core.Mocks;
using Moq;
using Spectre.Console;

namespace FileKeeper.Tests.Core.Services;

public class BackupServiceTests
{
    private readonly BackupService _sut;

    private readonly Mock<IAnsiConsole> _consoleMock;
    private readonly Mock<IFileSystem> _fileSystemMock;
    private readonly Mock<ICompressionService> _compressionServiceMock;
    private readonly Mock<IHashingService> _hashingServiceMock;
    private readonly Mock<IRecycleService> _recycleServiceMock;
    private readonly Mock<IConfigurationService> _configurationServiceMock;
    private readonly Mock<IFileInfoBuilder> _fileInfoBuilderMock;
    
    public BackupServiceTests()
    {
        _consoleMock = new Mock<IAnsiConsole>();
        _fileSystemMock = new Mock<IFileSystem>();
        _hashingServiceMock = new Mock<IHashingService>();
        _compressionServiceMock = new Mock<ICompressionService>();
        _recycleServiceMock = new  Mock<IRecycleService>();
        _configurationServiceMock = new Mock<IConfigurationService>();
        _fileInfoBuilderMock = new Mock<IFileInfoBuilder>();
        
        _sut = new BackupService(
            _consoleMock.Object,
            _configurationServiceMock.Object,
            _fileSystemMock.Object,
            _hashingServiceMock.Object,
            _compressionServiceMock.Object,
            _recycleServiceMock.Object,
            _fileInfoBuilderMock.Object);
    }

    [Fact]
    public async Task DeveGerarUmNovoBackupComSucesso()
    {
        // Arrange
        var configuration = new Configuration()
        {
            DestinationDirectory = "/home/felipe/backups",
            SourceDirectories = new List<string>() { "/var/www/html" }
        };

        var mockFileInfo = new MockFileInfo(
            "mockfile.txt",
            "/var/www/html/mockfile.txt",
            1000,
            new DateTime(2020, 01, 01),
            new DateTime(2020, 01, 02));
        
        _fileInfoBuilderMock
            .Setup(b => b.Build(It.IsAny<string>()))
            .Returns(mockFileInfo);
        
        _configurationServiceMock
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);
        
        _compressionServiceMock
            .Setup(s => s.ReadFileContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var listaArquivos = new[] { "/var/www/html/index.html", "/var/www/html/style.css", "/var/www/html/index.js" };
        
        _fileSystemMock
            .Setup(s => s.GetFiles("/var/www/html", It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns(listaArquivos);
        
        // Act
        var result = await _sut.CreateBackupAsync(CancellationToken.None);
        
        // Assert
        Assert.False(result.IsError);
    }
}