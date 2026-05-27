using FileKeeper.Core.Repositories;
using System.IO.Compression;

namespace FileKeeper.Tests.Core.Repositories;

public sealed class FileStoreRepositoryTests : IAsyncLifetime
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "filekeeper-tests", Guid.NewGuid().ToString("N"));

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempRoot);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddFileAsync_AndExtractAllAsync_WithMultipleFiles_RoundTripsContent()
    {
        var sourceDir = Path.Combine(_tempRoot, "src");
        var outputDir = Path.Combine(_tempRoot, "out");
        var archivePath = Path.Combine(_tempRoot, "snapshot.tar.gz");
        Directory.CreateDirectory(sourceDir);

        var sourceA = Path.Combine(sourceDir, "a.txt");
        var sourceB = Path.Combine(sourceDir, "b.txt");

        await File.WriteAllTextAsync(sourceA, "alpha");
        await File.WriteAllTextAsync(sourceB, "beta");

        await using var sut = new FileStoreRepository();

        sut.Open(archivePath, CompressionMode.Compress);
        await sut.AddFileAsync(sourceA, "folder/a.txt", CancellationToken.None);
        await sut.AddFileAsync(sourceB, "b.txt", CancellationToken.None);
        sut.Close();

        sut.Open(archivePath, CompressionMode.Decompress);
        await sut.ExtractAllAsync(outputDir, CancellationToken.None);
        sut.Close();

        var restoredA = Path.Combine(outputDir, "folder", "a.txt");
        var restoredB = Path.Combine(outputDir, "b.txt");

        Assert.True(File.Exists(restoredA));
        Assert.True(File.Exists(restoredB));
        Assert.Equal("alpha", await File.ReadAllTextAsync(restoredA));
        Assert.Equal("beta", await File.ReadAllTextAsync(restoredB));
    }

    [Fact]
    public async Task ReopenForRead_WhenReaderReachedEnd_AllowsReadingFromBeginningAgain()
    {
        var sourceDir = Path.Combine(_tempRoot, "src2");
        var outputDir = Path.Combine(_tempRoot, "out2");
        var archivePath = Path.Combine(_tempRoot, "snapshot2.tar.gz");
        Directory.CreateDirectory(sourceDir);

        var sourceA = Path.Combine(sourceDir, "a.txt");
        var sourceB = Path.Combine(sourceDir, "b.txt");

        await File.WriteAllTextAsync(sourceA, "first");
        await File.WriteAllTextAsync(sourceB, "second");

        await using var sut = new FileStoreRepository();

        sut.Open(archivePath, CompressionMode.Compress);
        await sut.AddFileAsync(sourceA, "a.txt", CancellationToken.None);
        await sut.AddFileAsync(sourceB, "b.txt", CancellationToken.None);
        sut.ReopenForRead();

        var restoredB = Path.Combine(outputDir, "b.txt");
        await sut.ExtractFileAsync("b.txt", restoredB, CancellationToken.None);
        Assert.Equal("second", await File.ReadAllTextAsync(restoredB));

        sut.ReopenForRead();

        var restoredA = Path.Combine(outputDir, "a.txt");
        await sut.ExtractFileAsync("a.txt", restoredA, CancellationToken.None);
        Assert.Equal("first", await File.ReadAllTextAsync(restoredA));
    }

    [Fact]
    public async Task AddFileAsync_WhenRepositoryIsNotOpen_ThrowsInvalidOperationException()
    {
        await using var sut = new FileStoreRepository();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.AddFileAsync("/tmp/does-not-matter.txt", "a.txt", CancellationToken.None));

        Assert.Contains("Open", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractAllAsync_WhenEntryPathIsUnsafe_ThrowsInvalidOperationException()
    {
        var sourceDir = Path.Combine(_tempRoot, "src3");
        var outputDir = Path.Combine(_tempRoot, "out3");
        var archivePath = Path.Combine(_tempRoot, "snapshot3.tar.gz");
        Directory.CreateDirectory(sourceDir);

        var sourceA = Path.Combine(sourceDir, "a.txt");
        await File.WriteAllTextAsync(sourceA, "content");

        await using var sut = new FileStoreRepository();

        sut.Open(archivePath, CompressionMode.Compress);
        await sut.AddFileAsync(sourceA, "../evil.txt", CancellationToken.None);
        sut.ReopenForRead();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExtractAllAsync(outputDir, CancellationToken.None));
    }

    [Fact]
    public async Task GetFileContentStreamAsync_WhenEntryExists_ReturnsStreamWithContent()
    {
        var sourceDir = Path.Combine(_tempRoot, "src-stream");
        var archivePath = Path.Combine(_tempRoot, "snapshot-stream.tar.gz");
        Directory.CreateDirectory(sourceDir);

        var sourceA = Path.Combine(sourceDir, "a.txt");
        var sourceB = Path.Combine(sourceDir, "b.txt");

        await File.WriteAllTextAsync(sourceA, "hello");
        await File.WriteAllTextAsync(sourceB, "world");

        await using var sut = new FileStoreRepository();

        sut.Open(archivePath, CompressionMode.Compress);
        await sut.AddFileAsync(sourceA, "a.txt", CancellationToken.None);
        await sut.AddFileAsync(sourceB, "b.txt", CancellationToken.None);
        sut.ReopenForRead();

        await using var stream = await sut.GetFileContentStreamAsync("b.txt", CancellationToken.None);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        Assert.Equal("world", content);
    }

    [Fact]
    public async Task AddStreamAsync_WhenValid_WritesEntryThatCanBeReadBack()
    {
        var archivePath = Path.Combine(_tempRoot, "snapshot-stream-write.tar.gz");
        await using var sut = new FileStoreRepository();

        await using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("stream-content"));

        sut.Open(archivePath, CompressionMode.Compress);
        await sut.AddStreamAsync(input, "from-stream.txt", CancellationToken.None);
        sut.ReopenForRead();

        await using var output = await sut.GetFileContentStreamAsync("from-stream.txt", CancellationToken.None);
        using var reader = new StreamReader(output);
        var content = await reader.ReadToEndAsync();

        Assert.Equal("stream-content", content);
    }
}
