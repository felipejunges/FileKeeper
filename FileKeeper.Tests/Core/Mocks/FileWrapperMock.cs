using FileKeeper.Core.Interfaces.Wrappers;

namespace FileKeeper.Tests.Core.Mocks;

public class FileWrapperMock : IFileWrapper, IDisposable, IAsyncDisposable
{
    private readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>();

    public void AddFile(string path, string content)
    {
        var contentBytes = System.Text.Encoding.UTF8.GetBytes(content);

        _files[path] = contentBytes;
    }

    public void ClearFiles()
    {
        _files.Clear();
    }

    public string RetrieveStreamContentAsString(string path)
    {
        return _files.TryGetValue(path, out var content)
            ? System.Text.Encoding.UTF8.GetString(content)
            : throw new FileNotFoundException($"File not found: {path}");
    }

    public bool Exists(string path)
    {
        return _files.ContainsKey(path);
    }

    public Stream OpenRead(string path)
    {
        if (!_files.TryGetValue(path, out var content))
            throw new FileNotFoundException($"File not found: {path}");

        return new MemoryStream(content, writable: false);
    }

    public Stream Create(string path)
    {
        return new PersistedMemoryStream(bytes => _files[path] = bytes);
    }

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
    {
        return _files.Keys.Where(k => k.StartsWith(path)).ToArray();
    }

    public Task<(long Size, DateTime LastModified, string Hash)> GetFileMetadataAsync(string path, CancellationToken token)
    {
        if (!_files.TryGetValue(path, out var content))
            throw new FileNotFoundException($"File not found: {path}");

        var size = content.Length;
        var lastModified = DateTime.UtcNow; // For testing purposes, we can use the current time
        var hash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(content));

        return Task.FromResult<(long Size, DateTime LastModified, string Hash)>((size, lastModified, hash));
    }

    public void CreateDirectoryIfNotExists(string dir)
    {
        // nothing to do here!
    }

    public void DeleteFile(string path)
    {
        _files.Remove(path);
    }

    public void Dispose()
    {
        _files.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
        _files.Clear();
    }

    private sealed class PersistedMemoryStream : MemoryStream
    {
        private readonly Action<byte[]> _onDispose;
        private bool _disposed;

        public PersistedMemoryStream(Action<byte[]> onDispose)
        {
            _onDispose = onDispose;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _onDispose(ToArray());
                _disposed = true;
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _onDispose(ToArray());
                _disposed = true;
            }

            await base.DisposeAsync();
        }
    }
}