using System.IO.Compression;

namespace FileKeeper.Core.Interfaces.Repositories;

public interface ITarRepository : IAsyncDisposable, IDisposable
{
    bool IsOpen { get; }
    string? CurrentFilePath { get; }
    CompressionMode? CurrentMode { get; }

    void Open(string tarGzFilePath, CompressionMode mode, bool leaveFileStreamOpen = false);
    void ReopenForRead();
    Task FlushAsync(CancellationToken token);
    void Close();

    Task AddFileAsync(string sourceFilePath, string entryPath, CancellationToken token);
    Task AddStreamAsync(Stream sourceStream, string entryPath, CancellationToken token);
    Task<Stream> GetFileContentStreamAsync(string entryPath, CancellationToken token);
    Task ExtractFileAsync(string entryPath, string destinationFilePath, CancellationToken token);
    Task ExtractAllAsync(string destinationDirectoryPath, CancellationToken token);
}