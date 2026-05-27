using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Models.Options;
using Microsoft.Extensions.Options;
using System.IO.Compression;

namespace FileKeeper.Core.Repositories;

public class ZipFileStoreRepository : IFileStoreRepository
{
    private readonly IOptionsMonitor<UserSettingsOptions> _userSettingsOptions;

    private const int BufferSize = 81920;
    private string BackupZipPath() => Path.Combine(_userSettingsOptions.CurrentValue.StorageDirectory, "store.zip");

    public ZipFileStoreRepository(IOptionsMonitor<UserSettingsOptions> userSettingsOptions)
    {
        _userSettingsOptions = userSettingsOptions;
    }

    public async Task AddFileAsync(string sourceFilePath, string entryPath, CancellationToken token)
    {
        await using var archive = await ZipFile.OpenAsync(BackupZipPath(), ZipArchiveMode.Update, token);

        await archive.CreateEntryFromFileAsync(
            sourceFilePath,
            entryPath,
            token);
    }

    public async Task AddFileAsync(IEnumerable<string> sourceFilePaths, string entryPath, CancellationToken token)
    {
        await using var archive = await ZipFile.OpenAsync(BackupZipPath(), ZipArchiveMode.Update, token);

        foreach (var sourceFilePath in sourceFilePaths)
        {
            await archive.CreateEntryFromFileAsync(
                sourceFilePath,
                entryPath,
                token);
        }
    }

    public async Task AddStreamAsync(Stream sourceStream, string entryPath, CancellationToken token)
    {
        if (!sourceStream.CanRead)
            throw new ArgumentException("Source stream must be readable.", nameof(sourceStream));

        if (sourceStream.CanSeek)
            sourceStream.Position = 0;

        await using var archive = await ZipFile.OpenAsync(BackupZipPath(), ZipArchiveMode.Update, token);

        var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);

        await using var destination = await entry.OpenAsync(token);

        await sourceStream.CopyToAsync(destination, BufferSize, token);
    }

    public async Task<Stream> GetFileContentStreamAsync(string entryPath, CancellationToken token)
    {
        await using var archive = await ZipFile.OpenReadAsync(BackupZipPath(), token);

        var entry = archive.GetEntry(entryPath);
        if (entry == null)
            throw new FileNotFoundException("The requested archive entry was not found.", entryPath);

        var output = new MemoryStream();

        // Open entry stream and copy into memory so we can dispose the archive safely
        await using (var source = await entry.OpenAsync(token))
        {
            await source.CopyToAsync(output, BufferSize, token);
        }

        output.Position = 0;
        return output;
    }

    public async Task ExtractFileAsync(string entryPath, string destinationFilePath, CancellationToken token)
    {
        await using var archive = await ZipFile.OpenAsync(BackupZipPath(), ZipArchiveMode.Update, token);

        var entry = archive.GetEntry(entryPath);
        if (entry == null)
            throw new FileNotFoundException("The requested archive entry was not found.", entryPath);

        await entry.ExtractToFileAsync(destinationFilePath, token);
    }

    public Task ExtractAllAsync(string destinationDirectoryPath, CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public async Task DeleteFileAsync(string entryPath, CancellationToken token)
    {
        await using var archive = await ZipFile.OpenReadAsync(BackupZipPath(), token);

        var entry = archive.GetEntry(entryPath);
        if (entry == null)
            throw new FileNotFoundException("The requested archive entry was not found.", entryPath);

        entry.Delete();
    }
}