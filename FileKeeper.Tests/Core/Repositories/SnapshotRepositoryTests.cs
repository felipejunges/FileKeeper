using ErrorOr;
using FileKeeper.Core.Interfaces.Wrappers;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Models.Options;
using FileKeeper.Core.Repositories;
using FileKeeper.Tests.Core.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;
using System.Text.Json;

namespace FileKeeper.Tests.Core.Repositories;

public class SnapshotRepositoryTests : IDisposable
{
    private readonly List<string> _temporaryDirectories = [];

    [Fact]
    public async Task GetAllSnapshotsAsync_WhenFilesAreValid_ReturnsSnapshotsSortedByCreatedAtUtcDescending()
    {
        var older = CreateSnapshot(DateTime.UtcNow.AddHours(-3), "docs/older.txt");
        var newer = CreateSnapshot(DateTime.UtcNow.AddHours(-1), "docs/newer.txt");

        var storageDirectory = "/snapshots";
        var olderPath = Path.Combine(storageDirectory, $"{older.Id}.json");
        var newerPath = Path.Combine(storageDirectory, $"{newer.Id}.json");

        var fileWrapper = new Mock<IFileWrapper>();
        fileWrapper
            .Setup(x => x.GetFiles(storageDirectory, "*.json", SearchOption.TopDirectoryOnly))
            .Returns([olderPath, newerPath]);
        fileWrapper
            .Setup(x => x.OpenRead(olderPath))
            .Returns(() => CreateJsonStream(older));
        fileWrapper
            .Setup(x => x.OpenRead(newerPath))
            .Returns(() => CreateJsonStream(newer));

        var sut = CreateSut(fileWrapper.Object, storageDirectory);

        var result = (await sut.GetAllSnapshotsAsync(CancellationToken.None)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(newer.Id, result[0].Id);
        Assert.Equal(older.Id, result[1].Id);
    }

    [Fact]
    public async Task GetAllSnapshotsAsync_WhenSomeFilesAreUnreadableOrInvalid_SkipsThem()
    {
        var validSnapshot = CreateSnapshot(DateTime.UtcNow, "docs/valid.txt");
        var storageDirectory = "/snapshots";
        var validPath = Path.Combine(storageDirectory, "valid.json");
        var invalidPath = Path.Combine(storageDirectory, "invalid.json");
        var nullPath = Path.Combine(storageDirectory, "null.json");
        var failingPath = Path.Combine(storageDirectory, "failing.json");

        var fileWrapper = new Mock<IFileWrapper>();
        fileWrapper
            .Setup(x => x.GetFiles(storageDirectory, "*.json", SearchOption.TopDirectoryOnly))
            .Returns([validPath, invalidPath, nullPath, failingPath]);
        fileWrapper
            .Setup(x => x.OpenRead(validPath))
            .Returns(() => CreateJsonStream(validSnapshot));
        fileWrapper
            .Setup(x => x.OpenRead(invalidPath))
            .Returns(() => new MemoryStream(Encoding.UTF8.GetBytes("{ invalid json }")));
        fileWrapper
            .Setup(x => x.OpenRead(nullPath))
            .Returns(() => new MemoryStream(Encoding.UTF8.GetBytes("null")));
        fileWrapper
            .Setup(x => x.OpenRead(failingPath))
            .Throws(new IOException("boom"));

        var sut = CreateSut(fileWrapper.Object, storageDirectory);

        var result = (await sut.GetAllSnapshotsAsync(CancellationToken.None)).ToList();

        Assert.Single(result);
        Assert.Equal(validSnapshot.Id, result[0].Id);
    }

    [Fact]
    public async Task GetAllSnapshotsAsync_WhenDirectoryDoesNotExist_ReturnsEmpty()
    {
        var storageDirectory = "/missing";
        var fileWrapper = new Mock<IFileWrapper>();
        fileWrapper
            .Setup(x => x.GetFiles(storageDirectory, "*.json", SearchOption.TopDirectoryOnly))
            .Throws(new DirectoryNotFoundException());

        var sut = CreateSut(fileWrapper.Object, storageDirectory);

        var result = await sut.GetAllSnapshotsAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllSnapshotsAsync_WhenTokenIsCanceled_ThrowsOperationCanceledException()
    {
        var sut = CreateSut(new Mock<IFileWrapper>().Object);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.GetAllSnapshotsAsync(cts.Token));
    }

    [Fact]
    public async Task GetLastSnapshotAsync_WhenSnapshotsExist_ReturnsNewestSnapshot()
    {
        var older = CreateSnapshot(DateTime.UtcNow.AddDays(-1), "docs/older.txt");
        var newer = CreateSnapshot(DateTime.UtcNow, "docs/newer.txt");
        var storageDirectory = "/snapshots";

        var fileWrapper = new Mock<IFileWrapper>();
        fileWrapper
            .Setup(x => x.GetFiles(storageDirectory, "*.json", SearchOption.TopDirectoryOnly))
            .Returns([
                Path.Combine(storageDirectory, "older.json"),
                Path.Combine(storageDirectory, "newer.json")
            ]);
        fileWrapper
            .Setup(x => x.OpenRead(Path.Combine(storageDirectory, "older.json")))
            .Returns(() => CreateJsonStream(older));
        fileWrapper
            .Setup(x => x.OpenRead(Path.Combine(storageDirectory, "newer.json")))
            .Returns(() => CreateJsonStream(newer));

        var sut = CreateSut(fileWrapper.Object, storageDirectory);

        var result = await sut.GetLastSnapshotAsync(CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(newer.Id, result.Value.Id);
    }

    [Fact]
    public async Task GetLastSnapshotAsync_WhenNoSnapshotsExist_ReturnsNotFound()
    {
        var storageDirectory = "/snapshots";
        var fileWrapper = new Mock<IFileWrapper>();
        fileWrapper
            .Setup(x => x.GetFiles(storageDirectory, "*.json", SearchOption.TopDirectoryOnly))
            .Returns([]);

        var sut = CreateSut(fileWrapper.Object, storageDirectory);

        var result = await sut.GetLastSnapshotAsync(CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task GetLastSnapshotAsync_WhenTokenIsCanceled_ThrowsOperationCanceledException()
    {
        var sut = CreateSut(new Mock<IFileWrapper>().Object);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.GetLastSnapshotAsync(cts.Token));
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenFileExistsAndJsonIsValid_ReturnsSnapshot()
    {
        var snapshot = CreateSnapshot(DateTime.UtcNow, "docs/readme.txt");
        var storageDirectory = "/snapshots";
        var expectedPath = Path.Combine(storageDirectory, $"{snapshot.Id}.json");

        var fileWrapper = new Mock<IFileWrapper>();
        fileWrapper.Setup(x => x.Exists(expectedPath)).Returns(true);
        fileWrapper.Setup(x => x.OpenRead(expectedPath)).Returns(() => CreateJsonStream(snapshot));

        var sut = CreateSut(fileWrapper.Object, storageDirectory);

        var result = await sut.GetSnapshotAsync(snapshot.Id, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(snapshot.Id, result.Value.Id);
        Assert.Equal("docs/readme.txt", result.Value.Files.Single().RelativePath);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenFileDoesNotExist_ReturnsNotFound()
    {
        var storageDirectory = "/snapshots";
        var expectedPath = Path.Combine(storageDirectory, $"{Guid.Empty}.json");
        var fileWrapper = new Mock<IFileWrapper>();
        fileWrapper.Setup(x => x.Exists(expectedPath)).Returns(false);

        var sut = CreateSut(fileWrapper.Object, storageDirectory);

        var result = await sut.GetSnapshotAsync(Guid.Empty, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        fileWrapper.Verify(x => x.OpenRead(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenOpenReadThrows_ReturnsFailure()
    {
        var id = Guid.CreateVersion7();
        var storageDirectory = "/snapshots";
        var expectedPath = Path.Combine(storageDirectory, $"{id}.json");
        var fileWrapper = new Mock<IFileWrapper>();
        fileWrapper.Setup(x => x.Exists(expectedPath)).Returns(true);
        fileWrapper.Setup(x => x.OpenRead(expectedPath)).Throws(new IOException("failed"));

        var sut = CreateSut(fileWrapper.Object, storageDirectory);

        var result = await sut.GetSnapshotAsync(id, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Failure, result.FirstError.Type);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenJsonIsInvalid_ReturnsFailure()
    {
        var id = Guid.CreateVersion7();
        var storageDirectory = "/snapshots";
        var expectedPath = Path.Combine(storageDirectory, $"{id}.json");
        var fileWrapper = new Mock<IFileWrapper>();
        fileWrapper.Setup(x => x.Exists(expectedPath)).Returns(true);
        fileWrapper.Setup(x => x.OpenRead(expectedPath))
            .Returns(() => new MemoryStream(Encoding.UTF8.GetBytes("{ invalid json }")));

        var sut = CreateSut(fileWrapper.Object, storageDirectory);

        var result = await sut.GetSnapshotAsync(id, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Failure, result.FirstError.Type);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenJsonIsNull_ReturnsFailure()
    {
        var id = Guid.CreateVersion7();
        var storageDirectory = "/snapshots";
        var expectedPath = Path.Combine(storageDirectory, $"{id}.json");
        var fileWrapper = new Mock<IFileWrapper>();
        fileWrapper.Setup(x => x.Exists(expectedPath)).Returns(true);
        fileWrapper.Setup(x => x.OpenRead(expectedPath))
            .Returns(() => new MemoryStream(Encoding.UTF8.GetBytes("null")));

        var sut = CreateSut(fileWrapper.Object, storageDirectory);

        var result = await sut.GetSnapshotAsync(id, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Failure, result.FirstError.Type);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenTokenIsCanceled_ThrowsOperationCanceledException()
    {
        var sut = CreateSut(new Mock<IFileWrapper>().Object);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.GetSnapshotAsync(Guid.NewGuid(), cts.Token));
    }

    [Fact]
    public async Task AddSnapshotAsync_WhenSnapshotIsValid_SavesJsonFile()
    {
        var storageDirectory = CreateTemporaryDirectory();
        var fileWrapper = new FileWrapperMock();
        var snapshot = CreateSnapshot(DateTime.UtcNow, "docs/file.txt");
        var expectedPath = Path.Combine(storageDirectory, $"{snapshot.Id}.json");
        var sut = CreateSut(fileWrapper, storageDirectory);

        var result = await sut.AddSnapshotAsync(snapshot, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.True(fileWrapper.Exists(expectedPath));

        var savedJson = fileWrapper.RetrieveStreamContentAsString(expectedPath);
        var persistedSnapshot = JsonSerializer.Deserialize<Snapshot>(savedJson);

        Assert.NotNull(persistedSnapshot);
        Assert.Equal(snapshot.Id, persistedSnapshot.Id);
        Assert.Equal(snapshot.FileCount, persistedSnapshot.FileCount);
    }

    [Fact]
    public async Task AddSnapshotAsync_WhenCreateThrows_ReturnsFailure()
    {
        var storageDirectory = CreateTemporaryDirectory();
        var snapshot = CreateSnapshot(DateTime.UtcNow, "docs/file.txt");
        var expectedPath = Path.Combine(storageDirectory, $"{snapshot.Id}.json");

        var fileWrapper = new Mock<IFileWrapper>();
        fileWrapper.Setup(x => x.Create(expectedPath)).Throws(new IOException("create failed"));

        var sut = CreateSut(fileWrapper.Object, storageDirectory);

        var result = await sut.AddSnapshotAsync(snapshot, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Failure, result.FirstError.Type);
    }

    [Fact]
    public async Task AddSnapshotAsync_WhenStorageDirectoryIsInvalid_ReturnsFailure()
    {
        var invalidStorageDirectory = "\0-invalid-path";
        var snapshot = CreateSnapshot(DateTime.UtcNow, "docs/file.txt");
        var sut = CreateSut(new Mock<IFileWrapper>().Object, invalidStorageDirectory);

        var result = await sut.AddSnapshotAsync(snapshot, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Failure, result.FirstError.Type);
    }

    [Fact]
    public async Task AddSnapshotAsync_WhenTokenIsCanceled_ThrowsOperationCanceledException()
    {
        var sut = CreateSut(new Mock<IFileWrapper>().Object);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.AddSnapshotAsync(CreateSnapshot(DateTime.UtcNow, "docs/file.txt"), cts.Token));
    }

    public void Dispose()
    {
        foreach (var directory in _temporaryDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private SnapshotRepository CreateSut(IFileWrapper fileWrapper, string storageDirectory = "/snapshots")
    {
        var options = Options.Create(new UserSettingsOptions
        {
            StorageDirectory = storageDirectory,
            SourceDirectories = []
        });

        return new SnapshotRepository(
            fileWrapper,
            options,
            new NullLogger<SnapshotRepository>());
    }

    private static Snapshot CreateSnapshot(DateTime createdAtUtc, string relativePath)
    {
        var id = Guid.CreateVersion7();
        return new Snapshot(
            id,
            createdAtUtc,
            [
                new FileEntry(
                    Guid.CreateVersion7(),
                    "/home/felipe",
                    relativePath,
                    $"stored/{Path.GetFileName(relativePath)}",
                    "abc123",
                    42,
                    createdAtUtc,
                    id.ToString())
            ]);
    }

    private static MemoryStream CreateJsonStream<T>(T value)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)));
    }

    private string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"filekeeper-snapshot-repository-tests-{Guid.NewGuid():N}");
        _temporaryDirectories.Add(directory);
        return directory;
    }
}

