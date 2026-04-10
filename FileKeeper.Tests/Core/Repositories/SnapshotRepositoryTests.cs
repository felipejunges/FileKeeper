using ErrorOr;
using FileKeeper.Core.Interfaces.Wrappers;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Models.Errors;
using FileKeeper.Core.Models.Options;
using FileKeeper.Core.Repositories;
using FileKeeper.Tests.Core.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;

namespace FileKeeper.Tests.Core.Repositories;

public class SnapshotRepositoryTests
{
    private const string StorageDirectory = "/var/filekeeper/snapshots";

    [Fact]
    public async Task GetAllSnapshotsAsync_WhenSnapshotFilesAreValid_ReturnsSortedByCreatedAtDescending()
    {
        await using var fileWrapper = new FileWrapperMock();
        var sut = CreateSut(fileWrapper);

        var older = new Snapshot(Guid.CreateVersion7(), DateTime.UtcNow.AddHours(-2), []);
        var newer = new Snapshot(Guid.CreateVersion7(), DateTime.UtcNow.AddHours(-1), []);

        fileWrapper.AddFile(GetSnapshotPath(older.Id), JsonSerializer.Serialize(older));
        fileWrapper.AddFile(GetSnapshotPath(newer.Id), JsonSerializer.Serialize(newer));

        var result = await sut.GetAllSnapshotsAsync(CancellationToken.None);

        Assert.False(result.IsError);
        var snapshots = result.Value.ToList();

        Assert.Equal(2, snapshots.Count);
        Assert.Equal(newer.Id, snapshots[0].Id);
        Assert.Equal(older.Id, snapshots[1].Id);
    }

    [Fact]
    public async Task GetAllSnapshotsAsync_WhenSomeFilesAreInvalid_SkipsInvalidOnes()
    {
        await using var fileWrapper = new FileWrapperMock();
        var sut = CreateSut(fileWrapper);

        var valid = new Snapshot(Guid.CreateVersion7(), DateTime.UtcNow, []);
        fileWrapper.AddFile(GetSnapshotPath(valid.Id), JsonSerializer.Serialize(valid));
        fileWrapper.AddFile(Path.Combine(StorageDirectory, $"{Guid.CreateVersion7()}.json"), "{ invalid json }");

        var result = await sut.GetAllSnapshotsAsync(CancellationToken.None);

        Assert.False(result.IsError);
        var snapshots = result.Value.ToList();
        Assert.Single(snapshots);
        Assert.Equal(valid.Id, snapshots[0].Id);
    }

    [Fact]
    public async Task GetAllSnapshotsAsync_WhenDirectoryCannotBeEnumerated_ReturnsError()
    {
        var fileWrapperMock = new Mock<IFileWrapper>();
        fileWrapperMock
            .Setup(f => f.GetFiles(StorageDirectory, "*.json", SearchOption.TopDirectoryOnly))
            .Throws(new DirectoryNotFoundException());

        var sut = CreateSut(fileWrapperMock.Object);

        var result = await sut.GetAllSnapshotsAsync(CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Snapshots directory was not found.", result.FirstError.Description);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenSnapshotExists_ReturnsSnapshot()
    {
        await using var fileWrapper = new FileWrapperMock();
        var sut = CreateSut(fileWrapper);

        var snapshot = new Snapshot(Guid.CreateVersion7(), DateTime.UtcNow, []);
        fileWrapper.AddFile(GetSnapshotPath(snapshot.Id), JsonSerializer.Serialize(snapshot));

        var result = await sut.GetSnapshotAsync(snapshot.Id, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(snapshot.Id, result.Value.Id);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenTokenIsCanceled_ReturnsOperationCanceled()
    {
        await using var fileWrapper = new FileWrapperMock();
        var sut = CreateSut(fileWrapper);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await sut.GetSnapshotAsync(Guid.CreateVersion7(), cts.Token);

        Assert.True(result.IsError);
        Assert.Equal(CommonErrors.OperationCanceled.Code, result.FirstError.Code);
    }

    [Fact]
    public async Task GetLastSnapshotAsync_WhenNoSnapshotsExist_ReturnsNotFound()
    {
        await using var fileWrapper = new FileWrapperMock();
        var sut = CreateSut(fileWrapper);

        var result = await sut.GetLastSnapshotAsync(CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task GetNextSnapshotAsync_WhenNextSnapshotExists_ReturnsIt()
    {
        await using var fileWrapper = new FileWrapperMock();
        var sut = CreateSut(fileWrapper);

        var first = Guid.Parse("019d493a-6a89-7080-93a1-815dd62ea950");
        var second = Guid.Parse("019d493a-6a89-7080-93a1-815dd62ea951");

        var firstSnapshot = new Snapshot(first, DateTime.UtcNow.AddMinutes(-2), []);
        var secondSnapshot = new Snapshot(second, DateTime.UtcNow.AddMinutes(-1), []);

        fileWrapper.AddFile(GetSnapshotPath(first), JsonSerializer.Serialize(firstSnapshot));
        fileWrapper.AddFile(GetSnapshotPath(second), JsonSerializer.Serialize(secondSnapshot));

        var result = await sut.GetNextSnapshotAsync(second, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(first, result.Value.Id);
    }

    [Fact]
    public async Task AddSnapshotAsync_WhenValid_CreatesSnapshotFile()
    {
        await using var fileWrapper = new FileWrapperMock();
        var sut = CreateSut(fileWrapper);

        var snapshot = new Snapshot(Guid.CreateVersion7(), DateTime.UtcNow, []);

        var result = await sut.AddSnapshotAsync(snapshot, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.True(fileWrapper.Exists(GetSnapshotPath(snapshot.Id)));
    }

    [Fact]
    public async Task AddSnapshotAsync_WhenTokenIsCanceled_ReturnsOperationCanceled()
    {
        await using var fileWrapper = new FileWrapperMock();
        var sut = CreateSut(fileWrapper);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await sut.AddSnapshotAsync(new Snapshot(Guid.CreateVersion7(), DateTime.UtcNow, []), cts.Token);

        Assert.True(result.IsError);
        Assert.Equal(CommonErrors.OperationCanceled.Code, result.FirstError.Code);
    }

    [Fact]
    public async Task DeleteSnapshotAsync_WhenSnapshotExists_DeletesFile()
    {
        await using var fileWrapper = new FileWrapperMock();
        var sut = CreateSut(fileWrapper);

        var id = Guid.CreateVersion7();
        fileWrapper.AddFile(GetSnapshotPath(id), "{}");

        var result = await sut.DeleteSnapshotAsync(id, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.False(fileWrapper.Exists(GetSnapshotPath(id)));
    }

    [Fact]
    public async Task DeleteSnapshotAsync_WhenSnapshotDoesNotExist_ReturnsNotFound()
    {
        await using var fileWrapper = new FileWrapperMock();
        var sut = CreateSut(fileWrapper);

        var result = await sut.DeleteSnapshotAsync(Guid.CreateVersion7(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task DeleteSnapshotAsync_WhenDeleteThrows_ReturnsFailure()
    {
        var fileWrapperMock = new Mock<IFileWrapper>();
        var id = Guid.CreateVersion7();
        var path = GetSnapshotPath(id);

        fileWrapperMock.Setup(f => f.Exists(path)).Returns(true);
        fileWrapperMock.Setup(f => f.DeleteFile(path)).Throws(new IOException("disk error"));

        var sut = CreateSut(fileWrapperMock.Object);

        var result = await sut.DeleteSnapshotAsync(id, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Failure, result.FirstError.Type);
    }

    private static SnapshotRepository CreateSut(IFileWrapper fileWrapper)
    {
        var options = Options.Create(new UserSettingsOptions
        {
            StorageDirectory = StorageDirectory,
            SourceDirectories = []
        });

        return new SnapshotRepository(fileWrapper, options, NullLogger<SnapshotRepository>.Instance);
    }

    private static string GetSnapshotPath(Guid id)
    {
        return Path.Combine(StorageDirectory, $"{id}.json");
    }
}