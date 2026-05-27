using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Models.Options;
using FileKeeper.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.IO.Compression;
using System.Text.Json;

namespace FileKeeper.Tests.Core.Services;

public class SnapshotServiceTests
{
    private readonly Mock<IFileStoreRepository> _tarRepositoryMock;
    private readonly Mock<IOptionsMonitor<UserSettingsOptions>> _optionsMonitorMock;
    private readonly Mock<ILogger<SnapshotService>> _loggerMock;
    private readonly UserSettingsOptions _userSettings;
    private readonly SnapshotService _sut;

    public SnapshotServiceTests()
    {
        _tarRepositoryMock = new Mock<IFileStoreRepository>();
        _optionsMonitorMock = new Mock<IOptionsMonitor<UserSettingsOptions>>();
        _loggerMock = new Mock<ILogger<SnapshotService>>();

        _userSettings = new UserSettingsOptions { StorageDirectory = "/tmp/test-storage" };
        _optionsMonitorMock.Setup(o => o.CurrentValue).Returns(_userSettings);

        _sut = new SnapshotService(
            _tarRepositoryMock.Object,
            _optionsMonitorMock.Object,
            _loggerMock.Object);
    }

    #region GetIndexAsync

    [Fact]
    public async Task GetIndexAsync_WhenIndexExists_ReturnsDeserializedIndex()
    {
        // Arrange
        var snapshot = Snapshot.Create();
        var index = new SnapshotIndex(new List<Snapshot> { snapshot });
        var json = JsonSerializer.Serialize(index);
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        _tarRepositoryMock.Setup(r => r.IsOpen).Returns(true);
        _tarRepositoryMock
            .Setup(r => r.GetFileContentStreamAsync("index.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);

        // Act
        var result = await _sut.GetIndexAsync(CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.Single(result.Value.Snapshots);
    }

    [Fact]
    public async Task GetIndexAsync_WhenRepositoryIsClosed_OpensRepository()
    {
        // Arrange
        var json = JsonSerializer.Serialize(SnapshotIndex.Empty());
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        _tarRepositoryMock.Setup(r => r.IsOpen).Returns(false);
        _tarRepositoryMock
            .Setup(r => r.GetFileContentStreamAsync("index.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);

        // Act
        await _sut.GetIndexAsync(CancellationToken.None);

        // Assert
        _tarRepositoryMock.Verify(r => r.Open(
            _userSettings.StorageDirectory,
            CompressionMode.Compress,
            true), Times.Once);
    }

    [Fact]
    public async Task GetIndexAsync_WhenRepositoryThrows_ReturnsError()
    {
        // Arrange
        _tarRepositoryMock.Setup(r => r.IsOpen).Returns(true);
        _tarRepositoryMock
            .Setup(r => r.GetFileContentStreamAsync("index.json", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk error"));

        // Act
        var result = await _sut.GetIndexAsync(CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("Disk error", result.FirstError.Description);
    }

    #endregion

    #region SaveIndexAsync

    [Fact]
    public async Task SaveIndexAsync_WhenSuccessful_AddsStreamAndFlushes()
    {
        // Arrange
        var index = SnapshotIndex.Empty();

        _tarRepositoryMock.Setup(r => r.IsOpen).Returns(true);
        _tarRepositoryMock
            .Setup(r => r.AddStreamAsync(It.IsAny<Stream>(), "index.json", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _tarRepositoryMock
            .Setup(r => r.FlushAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.SaveIndexAsync(index, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        _tarRepositoryMock.Verify(r => r.AddStreamAsync(It.IsAny<Stream>(), "index.json", It.IsAny<CancellationToken>()), Times.Once);
        _tarRepositoryMock.Verify(r => r.FlushAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveIndexAsync_WhenRepositoryThrows_ReturnsError()
    {
        // Arrange
        _tarRepositoryMock.Setup(r => r.IsOpen).Returns(true);
        _tarRepositoryMock
            .Setup(r => r.AddStreamAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Write error"));

        // Act
        var result = await _sut.SaveIndexAsync(SnapshotIndex.Empty(), CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("Write error", result.FirstError.Description);
    }

    #endregion

    #region AddFileAsync

    [Fact]
    public async Task AddFileAsync_WhenSuccessful_ReturnsSuccess()
    {
        // Arrange
        _tarRepositoryMock.Setup(r => r.IsOpen).Returns(true);
        _tarRepositoryMock
            .Setup(r => r.AddFileAsync("/source/file.txt", "entry/file.txt", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.AddFileAsync("/source/file.txt", "entry/file.txt", CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        _tarRepositoryMock.Verify(r => r.AddFileAsync("/source/file.txt", "entry/file.txt", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddFileAsync_WhenRepositoryThrows_ReturnsError()
    {
        // Arrange
        _tarRepositoryMock.Setup(r => r.IsOpen).Returns(true);
        _tarRepositoryMock
            .Setup(r => r.AddFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("File not found"));

        // Act
        var result = await _sut.AddFileAsync("/missing/file.txt", "entry/file.txt", CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("File not found", result.FirstError.Description);
    }

    #endregion

    #region RestoreFileAsync

    [Fact]
    public async Task RestoreFileAsync_WhenSuccessful_ReturnsSuccess()
    {
        // Arrange
        _tarRepositoryMock.Setup(r => r.IsOpen).Returns(true);
        _tarRepositoryMock
            .Setup(r => r.ExtractFileAsync("entry/file.txt", "/output/file.txt", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.RestoreFileAsync("entry/file.txt", "/output/file.txt", CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        _tarRepositoryMock.Verify(r => r.ExtractFileAsync("entry/file.txt", "/output/file.txt", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestoreFileAsync_WhenRepositoryThrows_ReturnsError()
    {
        // Arrange
        _tarRepositoryMock.Setup(r => r.IsOpen).Returns(true);
        _tarRepositoryMock
            .Setup(r => r.ExtractFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Extract error"));

        // Act
        var result = await _sut.RestoreFileAsync("entry/file.txt", "/output/file.txt", CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("Extract error", result.FirstError.Description);
    }

    #endregion

    #region FlushFilesAsync

    [Fact]
    public async Task FlushFilesAsync_DelegatesToRepository()
    {
        // Arrange
        _tarRepositoryMock
            .Setup(r => r.FlushAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.FlushFilesAsync(CancellationToken.None);

        // Assert
        _tarRepositoryMock.Verify(r => r.FlushAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Dispose / DisposeAsync

    [Fact]
    public void Dispose_ClosesAndDisposesRepository()
    {
        // Act
        _sut.Dispose();

        // Assert
        _tarRepositoryMock.Verify(r => r.Close(), Times.Once);
        _tarRepositoryMock.Verify(r => r.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_ClosesAndDisposesRepositoryAsync()
    {
        // Arrange
        _tarRepositoryMock
            .Setup(r => r.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        // Act
        await _sut.DisposeAsync();

        // Assert
        _tarRepositoryMock.Verify(r => r.Close(), Times.Once);
        _tarRepositoryMock.Verify(r => r.DisposeAsync(), Times.Once);
    }

    #endregion
}