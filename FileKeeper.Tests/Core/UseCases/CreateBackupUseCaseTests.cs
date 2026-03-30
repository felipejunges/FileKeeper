using ErrorOr;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.UseCases;
using FileKeeper.Tests.Core.Mocks;
using Moq;

namespace FileKeeper.Tests.Core.UseCases;

public class CreateBackupUseCaseTests : IAsyncLifetime
{
    private CreateBackupUseCase _sut;

    private readonly Mock<ISnapshotRepository> _snapshotRepository;
    private readonly FileWrapperMock _fileWrapper;
    private readonly Mock<ICompressedEncryptedFileWriter> _compressedEncryptedFileWriter;
    private readonly Mock<IConfigurationService> _configurationService;

    public CreateBackupUseCaseTests()
    {
        _snapshotRepository = new Mock<ISnapshotRepository>();
        _fileWrapper = new FileWrapperMock();
        _compressedEncryptedFileWriter = new Mock<ICompressedEncryptedFileWriter>();
        _configurationService = new Mock<IConfigurationService>();

        _sut = new CreateBackupUseCase(
            _snapshotRepository.Object,
            _fileWrapper,
            _compressedEncryptedFileWriter.Object,
            _configurationService.Object);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFail_IfGetLastSnapshotReturnsError()
    {
        // Arrange
        _snapshotRepository
            .Setup(s => s.GetLastSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.Failure(description: "Error getting last snapshot"));

        // Act
        var result = await _sut.ExecuteAsync(null, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Error getting last snapshot", result.FirstError.Description);

        _snapshotRepository
            .Verify(v => v.AddSnapshotAsync(It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoLastSnapshot_With3FilesOnDisk_ShouldGenerate3Files()
    {
        // Arrange
        _configurationService
            .Setup(s => s.GetConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Configuration(
                sourceDirectories: new[] { "/home/felipe" },
                storageDirectory: "/var"));
        
        _snapshotRepository
            .Setup(s => s.GetLastSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.NotFound(description: "No snapshot found"));

        _fileWrapper.ClearFiles();
        _fileWrapper.AddFile("/home/felipe/file1.txt", "Content of file 1");
        _fileWrapper.AddFile("/home/felipe/file2.txt", "Content of file 2");
        _fileWrapper.AddFile("/home/felipe/file3.txt", "Content of file 3");

        // Act
        var result = await _sut.ExecuteAsync(null, CancellationToken.None);
        
        // Assert
        Assert.False(result.IsError);
        Assert.Equal(3, result.Value.FileCount);

        _snapshotRepository.Verify(s =>
                s.AddSnapshotAsync(It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()),
            Times.Once());
        
        _compressedEncryptedFileWriter
            .Verify(v =>
                    v.CompressFromStreamToFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Exactly(3));
    }
    
    [Fact]
    public async Task ExecuteAsync_WhenCleanSnapshot_With3FilesOnDisk_ShouldGenerate3Files()
    {
        // Arrange
        _configurationService
            .Setup(s => s.GetConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Configuration(
                sourceDirectories: new[] { "/home/felipe" },
                storageDirectory: "/var"));
        
        _snapshotRepository
            .Setup(s => s.GetLastSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Snapshot(Guid.CreateVersion7(), DateTime.UtcNow, new List<FileEntry>()));

        _fileWrapper.ClearFiles();
        _fileWrapper.AddFile("/home/felipe/file1.txt", "Content of file 1");
        _fileWrapper.AddFile("/home/felipe/file2.txt", "Content of file 2");
        _fileWrapper.AddFile("/home/felipe/file3.txt", "Content of file 3");

        // Act
        var result = await _sut.ExecuteAsync(null, CancellationToken.None);
        
        // Assert
        var filesCompressed = 3;
        
        Assert.False(result.IsError);
        Assert.Equal(3, result.Value.FileCount);

        _snapshotRepository.Verify(s =>
                s.AddSnapshotAsync(It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()),
            Times.Once());
        
        _compressedEncryptedFileWriter
            .Verify(v =>
                    v.CompressFromStreamToFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Exactly(filesCompressed));
    }
    
    [Fact]
    public async Task ExecuteAsync_WhenSnapshotWith2Files_With3FilesOnDisk_ShouldGenerate1NewFile()
    {
        // Arrange
        _configurationService
            .Setup(s => s.GetConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Configuration(
                sourceDirectories: new[] { "/home/felipe" },
                storageDirectory: "/var"));

        var currentSnapshotId = Guid.CreateVersion7();
        var currentSnapshotName = currentSnapshotId.ToString("N")[..8];
        
        var snapshot = new Snapshot(
            Guid.CreateVersion7(),
            DateTime.UtcNow, 
            new List<FileEntry>()
            {
                new FileEntry(
                    id: Guid.CreateVersion7(),
                    relativePath: "file1.txt",
                    storedPath: "abcd/abcdefghijkl1",
                    "k8vfVcLU9Ts4e9YMT9IEpukdcL877GL+UIiRWC+Qi40=", // same hash as '"Content of file 1"'
                    size: 100,
                    lastModified: DateTime.UtcNow,
                    currentSnapshotName),
                new FileEntry(
                    id: Guid.CreateVersion7(),
                    relativePath: "file2.txt",
                    storedPath: "abcd/abcdefghijkl2",
                    "UqQm+33HyANVVbmXykthdNWI1PIAFWLuGjt9oHeVsp0=", // same hash as '"Content of file 2"'
                    size: 100,
                    lastModified: DateTime.UtcNow,
                    currentSnapshotName)
            }); 
        
        _snapshotRepository
            .Setup(s => s.GetLastSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        _fileWrapper.ClearFiles();
        _fileWrapper.AddFile("/home/felipe/file1.txt", "Content of file 1");
        _fileWrapper.AddFile("/home/felipe/file2.txt", "Content of file 2");
        _fileWrapper.AddFile("/home/felipe/file3.txt", "Content of file 3");

        // Act
        var result = await _sut.ExecuteAsync(null, CancellationToken.None);
        
        // Assert
        var filesCompressed = 1;
        
        Assert.False(result.IsError);
        Assert.Equal(3, result.Value.FileCount);
        
        Assert.Equal(currentSnapshotName, result.Value.Files.ElementAt(0).FoundInSnapshot);
        Assert.Equal(currentSnapshotName, result.Value.Files.ElementAt(1).FoundInSnapshot);
        Assert.Equal(result.Value.SnapshotName, result.Value.Files.ElementAt(2).FoundInSnapshot);

        _snapshotRepository.Verify(s =>
                s.AddSnapshotAsync(It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()),
            Times.Once());
        
        _compressedEncryptedFileWriter
            .Verify(v =>
                    v.CompressFromStreamToFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Exactly(filesCompressed));
    }
    
    [Fact]
    public async Task ExecuteAsync_WhenSnapshotWith2Files_With3FilesOnDisk_OneDifferent_ShouldGenerate2NewFiles()
    {
        // Arrange
        _configurationService
            .Setup(s => s.GetConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Configuration(
                sourceDirectories: new[] { "/home/felipe" },
                storageDirectory: "/var"));

        var currentSnapshotId = Guid.CreateVersion7();
        var currentSnapshotName = currentSnapshotId.ToString("N")[..8];
        
        var snapshot = new Snapshot(
            Guid.CreateVersion7(),
            DateTime.UtcNow, 
            new List<FileEntry>()
            {
                new FileEntry(
                    id: Guid.CreateVersion7(),
                    relativePath: "file1.txt",
                    storedPath: "abcd/abcdefghijkl1",
                    "k8vfVcLU9Ts4e9YMT9IEpukdcL877GL+UIiRWC+Qi40=", // same hash as '"Content of file 1"'
                    size: 100,
                    lastModified: DateTime.UtcNow,
                    currentSnapshotName),
                new FileEntry(
                    id: Guid.CreateVersion7(),
                    relativePath: "file2.txt",
                    storedPath: "abcd/abcdefghijkl2",
                    "UqQm+33HyANVVbmXykthdNWI1PIAFWLuGjt9oHeVsp0=AAAAA", // different hash than '"Content of file 2"'
                    size: 100,
                    lastModified: DateTime.UtcNow,
                    currentSnapshotName)
            }); 
        
        _snapshotRepository
            .Setup(s => s.GetLastSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        _fileWrapper.ClearFiles();
        _fileWrapper.AddFile("/home/felipe/file1.txt", "Content of file 1");
        _fileWrapper.AddFile("/home/felipe/file2.txt", "Content of file 2");
        _fileWrapper.AddFile("/home/felipe/file3.txt", "Content of file 3");

        // Act
        var result = await _sut.ExecuteAsync(null, CancellationToken.None);
        
        // Assert
        var filesCompressed = 2;
        
        Assert.False(result.IsError);
        Assert.Equal(3, result.Value.FileCount);
        
        Assert.Equal(currentSnapshotName, result.Value.Files.ElementAt(0).FoundInSnapshot);
        Assert.Equal(result.Value.SnapshotName, result.Value.Files.ElementAt(1).FoundInSnapshot);
        Assert.Equal(result.Value.SnapshotName, result.Value.Files.ElementAt(2).FoundInSnapshot);

        _snapshotRepository.Verify(s =>
                s.AddSnapshotAsync(It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()),
            Times.Once());
        
        _compressedEncryptedFileWriter
            .Verify(v =>
                    v.CompressFromStreamToFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Exactly(filesCompressed));
    }
}