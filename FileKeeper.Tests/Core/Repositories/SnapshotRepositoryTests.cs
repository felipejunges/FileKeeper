using System.Text;
using System.Text.Json;
using ErrorOr;
using FileKeeper.Core.Interfaces.Wrappers;
using FileKeeper.Core.Models;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FileKeeper.Tests.Core.Repositories;

public class SnapshotRepositoryTests
{
    [Fact]
    public async Task GetSnapshotAsync_WhenFileExistsAndJsonIsValid_ReturnsSnapshot()
    {
        var id = Guid.CreateVersion7();
        var expected = new Snapshot(
            id,
            DateTime.UtcNow,
            [
                new FileEntry(
                    Guid.CreateVersion7(),
                    "docs/readme.txt",
                    "abc/abcdef",
                    "abc123",
                    42,
                    DateTime.UtcNow,
                    id.ToString()
                )
            ]
        );

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expected)));

        var fileWrapper = new Mock<IFileWrapper>();
        fileWrapper.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        fileWrapper.Setup(x => x.OpenRead(It.IsAny<string>())).Returns(stream);

        var sut = new SnapshotRepository(new NullLogger<SnapshotRepository>(), fileWrapper.Object, "/snapshots");

        var result = await sut.GetSnapshotAsync(id, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(id, result.Value.Id);
        Assert.Equal(1, result.Value.FileCount);
        Assert.Equal("docs/readme.txt", result.Value.Files.Single().RelativePath);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenFileDoesNotExist_ReturnsNotFound()
    {
        var fileWrapper = new Mock<IFileWrapper>();
        fileWrapper.Setup(x => x.Exists(It.IsAny<string>())).Returns(false);

        var sut = new SnapshotRepository(new NullLogger<SnapshotRepository>(), fileWrapper.Object, "/snapshots");

        var result = await sut.GetSnapshotAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        fileWrapper.Verify(x => x.OpenRead(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenOpenReadThrows_ReturnsFailure()
    {
        var fileWrapper = new Mock<IFileWrapper>();
        fileWrapper.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        fileWrapper.Setup(x => x.OpenRead(It.IsAny<string>())).Throws(new IOException("failed"));

        var sut = new SnapshotRepository(new NullLogger<SnapshotRepository>(), fileWrapper.Object, "/snapshots");

        var result = await sut.GetSnapshotAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Failure, result.FirstError.Type);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenJsonIsInvalid_ReturnsFailure()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{ invalid json }"));

        var fileWrapper = new Mock<IFileWrapper>();
        fileWrapper.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        fileWrapper.Setup(x => x.OpenRead(It.IsAny<string>())).Returns(stream);

        var sut = new SnapshotRepository(new NullLogger<SnapshotRepository>(), fileWrapper.Object, "/snapshots");

        var result = await sut.GetSnapshotAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Failure, result.FirstError.Type);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenJsonIsNull_ReturnsFailure()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("null"));

        var fileWrapper = new Mock<IFileWrapper>();
        fileWrapper.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        fileWrapper.Setup(x => x.OpenRead(It.IsAny<string>())).Returns(stream);

        var sut = new SnapshotRepository(new NullLogger<SnapshotRepository>(), fileWrapper.Object, "/snapshots");

        var result = await sut.GetSnapshotAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Failure, result.FirstError.Type);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenTokenIsCanceled_ThrowsOperationCanceledException()
    {
        var sut = new SnapshotRepository(new NullLogger<SnapshotRepository>());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.GetSnapshotAsync(Guid.NewGuid(), cts.Token));
    }
}

