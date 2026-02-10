using FileKeeper.Core.Interfaces.Abstraction;
using Spectre.Console;
using System.IO.Compression;

namespace FileKeeper.Core.Services.Abstraction;

public class CompressionZipService : ICompressionService
{
    private readonly IAnsiConsole _console;
    private readonly IFileSystem _fileSystem;

    public CompressionZipService(IAnsiConsole console, IFileSystem fileSystem)
    {
        _console = console;
        _fileSystem = fileSystem;
    }

    public async Task CompressFilesAsync(IList<(string FullPath, string StoredPath)> files, string backupPath, string backupName,
        CancellationToken cancellationToken)
    {
        using (var archive = await ZipFile.OpenAsync(backupPath, ZipArchiveMode.Update, cancellationToken))
        {
            foreach (var fileInfo in files)
            {
                await archive.CreateEntryFromFileAsync(
                    fileInfo.FullPath,
                    Path.Combine(backupName, fileInfo.StoredPath),
                    cancellationToken);

                _console.MarkupLine($"[green]Compressed:[/] {fileInfo.FullPath}");
            }
        }
    }

    public async Task<string?> ReadFileContentAsync(string backupPath, string storedPath, CancellationToken cancellationToken)
    {
        if (!_fileSystem.FileExists(backupPath))
            return null;

        using (var archive = await ZipFile.OpenReadAsync(backupPath, cancellationToken))
        {
            var entry = archive.GetEntry(storedPath);

            if (entry == null)
                return null;

            using (var reader = new StreamReader(await entry.OpenAsync(cancellationToken)))
            {
                return await reader.ReadToEndAsync(cancellationToken);
            }
        }
    }

    public async Task WriteFileContentAsync(string backupPath, string storedPath, string content, CancellationToken cancellationToken)
    {
        using (var archive = await ZipFile.OpenAsync(backupPath, ZipArchiveMode.Update, cancellationToken))
        {
            var existing = archive.GetEntry(storedPath);
            existing?.Delete();

            var entry = archive.CreateEntry(storedPath);

            using (var writer = new StreamWriter(await entry.OpenAsync(cancellationToken)))
            {
                await writer.WriteAsync(content.AsMemory(), cancellationToken);
            }
        }
    }

    public Task MoveFileAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task RemoveFolderAsync(string backupPath, string firstBackupBackupName, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}