using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Models.DTOs;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text;
using System.Text.Json;

namespace FileKeeper.Tests.Core.Services;

public class SnapshotServiceTests
{
    private readonly Mock<IFileStoreRepository> _repoMock;
    private readonly SnapshotService _sut;

    public SnapshotServiceTests()
    {
        _repoMock = new Mock<IFileStoreRepository>(MockBehavior.Strict);

        _sut = new SnapshotService(
            _repoMock.Object,
            new NullLogger<SnapshotService>());
    }

    #region GetIndexAsync

    [Fact]
    public async Task GetIndexAsync_WhenIndexExists_ReturnsDeserializedIndex()
    {
        // Arrange
        var index = SnapshotIndex.Empty();
        var json = JsonSerializer.Serialize(index);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        _repoMock.Setup(r => r.GetFileContentStreamAsync("index.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);

        // Act
        var result = await _sut.GetIndexAsync(CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.Snapshots);
        _repoMock.Verify(r => r.GetFileContentStreamAsync("index.json", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetIndexAsync_WhenRepositoryThrows_ReturnsError()
    {
        // Arrange
        _repoMock.Setup(r => r.GetFileContentStreamAsync("index.json", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disk"));

        // Act
        var result = await _sut.GetIndexAsync(CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("GetIndexAsync", result.FirstError.Code);
        _repoMock.Verify(r => r.GetFileContentStreamAsync("index.json", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region SaveIndexAsync

    [Fact]
    public async Task SaveIndexAsync_WhenSuccessful_ReturnsSuccess()
    {
        // Arrange
        _repoMock.Setup(r => r.AddStreamAsync(It.IsAny<Stream>(), "index.json", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var index = SnapshotIndex.Empty();

        // Act
        var result = await _sut.SaveIndexAsync(index, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        _repoMock.Verify(r => r.AddStreamAsync(It.IsAny<Stream>(), "index.json", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveIndexAsync_WhenRepositoryThrows_ReturnsError()
    {
        // Arrange
        _repoMock.Setup(r => r.AddStreamAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("write fail"));

        // Act
        var result = await _sut.SaveIndexAsync(SnapshotIndex.Empty(), CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("SaveIndexAsync", result.FirstError.Code);
        _repoMock.Verify(r => r.AddStreamAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region AddFileAsync

    [Fact]
    public async Task AddFileAsync_WhenSuccessful_ReturnsSuccess()
    {
        // Arrange
        var fileToSave = new FileToSave("/src/file.txt", "file.txt", "file.txt", "hash", 100, DateTime.Now);

        _repoMock.Setup(r => r.AddFileAsync(fileToSave, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var result = await _sut.AddFileAsync(fileToSave, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        _repoMock.Verify(r => r.AddFileAsync(fileToSave, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddFileAsync_WhenRepositoryThrows_ReturnsError()
    {
        // Arrange
        var missingFile = new FileToSave("/missing", "entry", "entry", "hash", 100, DateTime.Now);
        
        _repoMock.Setup(r => r.AddFileAsync(It.IsAny<FileToSave>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("not found"));

        // Act
        var result = await _sut.AddFileAsync(missingFile, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("AddFileAsync", result.FirstError.Code);
    }

    #endregion

    #region RestoreFileAsync

    [Fact]
    public async Task RestoreFileAsync_WhenSuccessful_ReturnsSuccess()
    {
        // Arrange
        var fileToRestore = new FileToRestore("/src/file.txt", "file.txt");
        
        _repoMock.Setup(r => r.ExtractFileAsync(fileToRestore, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.RestoreFileAsync(fileToRestore, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        _repoMock.Verify(r => r.ExtractFileAsync(fileToRestore, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestoreFileAsync_WhenRepositoryThrows_ReturnsError()
    {
        // Arrange
        var missingFile = new FileToRestore("/missing", "entry");
        
        _repoMock.Setup(r => r.ExtractFileAsync(It.IsAny<FileToRestore>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("extract"));

        // Act
        var result = await _sut.RestoreFileAsync(missingFile, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("RestoreFileAsync", result.FirstError.Code);
    }

    #endregion

    #region DeleteFileAsync

    [Fact]
    public async Task DeleteFileAsync_WhenSuccessful_ReturnsSuccess()
    {
        // Arrange
        _repoMock.Setup(r => r.DeleteFileAsync("entry/file.txt", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.DeleteFileAsync("entry/file.txt", CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        _repoMock.Verify(r => r.DeleteFileAsync("entry/file.txt", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteFileAsync_WhenRepositoryThrows_ReturnsError()
    {
        // Arrange
        _repoMock.Setup(r => r.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("delete"));

        // Act
        var result = await _sut.DeleteFileAsync("entry", CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("DeleteFileAsync", result.FirstError.Code);
    }

    #endregion
}