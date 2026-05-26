using FileKeeper.Core.Interfaces.Repositories;
using System.Formats.Tar;
using System.IO.Compression;

namespace FileKeeper.Core.Repositories;

public sealed class TarRepository : ITarRepository
{
    private const int BufferSize = 81920;

    private FileStream? _fileStream;
    private GZipStream? _gzipStream;
    private TarWriter? _tarWriter;
    private TarReader? _tarReader;

    public bool IsOpen => _fileStream is not null;
    public string? CurrentFilePath { get; private set; }
    public CompressionMode? CurrentMode { get; private set; }

    public void Open(string tarGzFilePath, CompressionMode mode, bool leaveFileStreamOpen = false)
    {
        if (string.IsNullOrWhiteSpace(tarGzFilePath))
            throw new ArgumentException("Archive path must be provided.", nameof(tarGzFilePath));

        if (IsOpen)
            throw new InvalidOperationException("An archive is already open. Close it before opening a new one.");

        var fileMode = mode == CompressionMode.Compress ? FileMode.Create : FileMode.Open;
        var fileAccess = mode == CompressionMode.Compress ? FileAccess.Write : FileAccess.Read;

        _fileStream = new FileStream(
            tarGzFilePath,
            fileMode,
            fileAccess,
            FileShare.Read,
            BufferSize,
            useAsync: true);

        _gzipStream = new GZipStream(_fileStream, mode, leaveOpen: leaveFileStreamOpen);

        if (mode == CompressionMode.Compress)
            _tarWriter = new TarWriter(_gzipStream, leaveOpen: true);
        else
            _tarReader = new TarReader(_gzipStream, leaveOpen: true);

        CurrentMode = mode;
        CurrentFilePath = tarGzFilePath;
    }

    public void ReopenForRead()
    {
        if (!IsOpen || string.IsNullOrWhiteSpace(CurrentFilePath))
            throw new InvalidOperationException("No tar.gz archive is open. Open an archive before calling ReopenForRead.");

        var archivePath = CurrentFilePath;
        Close();
        Open(archivePath, CompressionMode.Decompress);
    }

    public Task AddFileAsync(string sourceFilePath, string? entryPath, CancellationToken token)
    {
        EnsureOpen();
        EnsureMode(CompressionMode.Compress);
        token.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(sourceFilePath))
            throw new ArgumentException("Source file path must be provided.", nameof(sourceFilePath));

        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Source file was not found.", sourceFilePath);

        var normalizedEntryPath = NormalizeEntryPath(entryPath ?? Path.GetFileName(sourceFilePath));
        _tarWriter!.WriteEntry(sourceFilePath, normalizedEntryPath);

        return Task.CompletedTask;
    }

    public async Task ExtractFileAsync(string entryPath, string destinationFilePath, CancellationToken token)
    {
        EnsureOpen();
        EnsureMode(CompressionMode.Decompress);

        if (string.IsNullOrWhiteSpace(entryPath))
            throw new ArgumentException("Entry path must be provided.", nameof(entryPath));

        if (string.IsNullOrWhiteSpace(destinationFilePath))
            throw new ArgumentException("Destination file path must be provided.", nameof(destinationFilePath));

        var normalizedEntryPath = NormalizeEntryPath(entryPath);

        TarEntry? entry;
        while ((entry = _tarReader!.GetNextEntry()) is not null)
        {
            token.ThrowIfCancellationRequested();

            if (!string.Equals(NormalizeEntryPath(entry.Name), normalizedEntryPath, StringComparison.Ordinal))
                continue;

            if (entry.EntryType is TarEntryType.Directory)
                return;

            var destinationDirectory = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            await using var destination = new FileStream(
                destinationFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                useAsync: true);

            if (entry.DataStream is not null)
                await entry.DataStream.CopyToAsync(destination, BufferSize, token);

            await destination.FlushAsync(token);
            return;
        }

        throw new FileNotFoundException("The requested archive entry was not found.", entryPath);
    }

    public async Task ExtractAllAsync(string destinationDirectoryPath, CancellationToken token)
    {
        EnsureOpen();
        EnsureMode(CompressionMode.Decompress);

        if (string.IsNullOrWhiteSpace(destinationDirectoryPath))
            throw new ArgumentException("Destination directory path must be provided.", nameof(destinationDirectoryPath));

        Directory.CreateDirectory(destinationDirectoryPath);
        var destinationRoot = Path.GetFullPath(destinationDirectoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        TarEntry? entry;
        while ((entry = await _tarReader!.GetNextEntryAsync(cancellationToken: token)) is not null)
        {
            token.ThrowIfCancellationRequested();

            var relativeEntryPath = NormalizeEntryPath(entry.Name)
                .Replace('/', Path.DirectorySeparatorChar);

            var fullOutputPath = Path.GetFullPath(Path.Combine(destinationDirectoryPath, relativeEntryPath));
            if (!fullOutputPath.StartsWith(destinationRoot, StringComparison.Ordinal))
                throw new InvalidOperationException($"Unsafe tar entry path: {entry.Name}");

            if (entry.EntryType is TarEntryType.Directory)
            {
                Directory.CreateDirectory(fullOutputPath);
                continue;
            }

            var parentDirectory = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
                Directory.CreateDirectory(parentDirectory);

            await using var output = new FileStream(
                fullOutputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                useAsync: true);

            if (entry.DataStream is not null)
                await entry.DataStream.CopyToAsync(output, BufferSize, token);

            await output.FlushAsync(token);
        }
    }

    public async Task FlushAsync(CancellationToken token)
    {
        EnsureOpen();
        EnsureMode(CompressionMode.Compress);
        
        await _gzipStream!.FlushAsync(token);
        await _fileStream!.FlushAsync(token); 
    }
    
    public void Close()
    {
        _tarWriter?.Dispose();
        _tarWriter = null;

        _tarReader?.Dispose();
        _tarReader = null;

        _gzipStream?.Dispose();
        _gzipStream = null;

        _fileStream?.Dispose();
        _fileStream = null;

        CurrentFilePath = null;
        CurrentMode = null;
    }

    public void Dispose()
    {
        Close();
    }

    public ValueTask DisposeAsync()
    {
        Close();
        return ValueTask.CompletedTask;
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
            throw new InvalidOperationException("No tar.gz archive is open. Call Open first.");
    }

    private void EnsureMode(CompressionMode expectedMode)
    {
        if (CurrentMode != expectedMode)
            throw new InvalidOperationException($"Current mode is {CurrentMode}; expected {expectedMode}.");
    }

    private static string NormalizeEntryPath(string entryPath)
    {
        var normalized = entryPath.Replace('\\', '/').Trim();
        while (normalized.StartsWith("./", StringComparison.Ordinal))
            normalized = normalized[2..];

        return normalized.TrimStart('/');
    }
}