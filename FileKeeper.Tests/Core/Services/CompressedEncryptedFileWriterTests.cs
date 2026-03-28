using FileKeeper.Core.Services;
using FileKeeper.Tests.Core.Mocks;

namespace FileKeeper.Tests.Core.Services;

public sealed class CompressedEncryptedFileWriterTests : IAsyncLifetime
{
    private readonly CompressedEncryptedFileWriter _sut;

    private readonly FileWrapperMock _fileWrapperMock;

    public CompressedEncryptedFileWriterTests()
    {
        _fileWrapperMock = new FileWrapperMock();

        _sut = new CompressedEncryptedFileWriter(_fileWrapperMock);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _fileWrapperMock.DisposeAsync();
    }

    [Fact]
    public async Task CompressFromStreamToFileAsync_WhenSourceFileExists_ReturnsSuccessAndCreatesOutput()
    {
        // Arrange
        _fileWrapperMock.AddFile("source.txt", "This is the file content!");

        // Act
        var result = await _sut.CompressFromStreamToFileAsync("source.txt", "output.txt", CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        var exit = _fileWrapperMock.RetrieveStreamContentAsString("output.txt");

        Assert.NotNull(exit);
    }
    
    [Fact]
    public async Task CompressAndDecompressFromStreamToFileAsync_WhenSourceFileExists_ReturnsToTheOriginalOutput()
    {
        // Arrange
        var originalContent = "This is the file content!";
        _fileWrapperMock.AddFile("source.txt", originalContent);

        // Act
        var compressResult = await _sut.CompressFromStreamToFileAsync("source.txt", "output.txt", CancellationToken.None);
        var decompressResult = await _sut.DecompressAndDecryptFileAsync("output.txt", "newOutput.txt", CancellationToken.None);

        // Assert
        Assert.False(compressResult.IsError);
        Assert.False(decompressResult.IsError);

        var exit = _fileWrapperMock.RetrieveStreamContentAsString("newOutput.txt");

        Assert.NotNull(exit);
        Assert.Equal(originalContent, exit);
    }
}