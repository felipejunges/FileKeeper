using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Models;
using FileKeeper.Core.Services;
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
    
    public BackupServiceTests()
    {
        _consoleMock = new Mock<IAnsiConsole>();
        _fileSystemMock = new Mock<IFileSystem>();
        _hashingServiceMock = new Mock<IHashingService>();
        _compressionServiceMock = new Mock<ICompressionService>();
        
        var configuration = new Configuration()
        {
            DestinationDirectory = "/home/felipe/backups",
            SourceDirectories = new List<string>() { "/var/www/html" }
        };
        
        _sut = new BackupService(
            _consoleMock.Object,
            configuration,
            _fileSystemMock.Object,
            _hashingServiceMock.Object,
            _compressionServiceMock.Object);
    }

    [Fact]
    public async Task DeveGerarUmNovoBackupComSucesso()
    {
        // Arrange
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