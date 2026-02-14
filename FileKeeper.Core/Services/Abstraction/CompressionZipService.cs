using FileKeeper.Core.Interfaces.Abstraction;
using Spectre.Console;
using System.IO.Compression;

namespace FileKeeper.Core.Services.Abstraction;

public class CompressionZipService : ICompressionService
{
    private readonly IAnsiConsole _console;
    private readonly IFileSystem _fileSystem;

    private string BackupZipPath(string backupPath) => Path.Combine(backupPath, "backup.zip");
    
    public CompressionZipService(IAnsiConsole console, IFileSystem fileSystem)
    {
        _console = console;
        _fileSystem = fileSystem;
    }

    public async Task CompressFilesAsync(IList<(string FullPath, string StoredPath)> files, string backupPath, string backupName,
        CancellationToken cancellationToken)
    {
        using (var archive = await ZipFile.OpenAsync(BackupZipPath(backupPath), ZipArchiveMode.Update, cancellationToken))
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

    public async Task DecompressFilesAsync(IList<(string BackupName, string StoredPath, string RelativePath)> files, string backupPath, string destinationPath, CancellationToken cancellationToken)
    {
        if (!_fileSystem.FileExists(BackupZipPath(backupPath)))
        {
            _console.MarkupLine($"[red]Backup not found:[/] {BackupZipPath(backupPath)}");
            return;
        }

        if (!_fileSystem.DirectoryExists(destinationPath))
        {
            _console.MarkupLine($"[yellow]Destination directory not found, creating:[/] {destinationPath}");
            _fileSystem.CreateDirectory(destinationPath);
        }

        using (var archive = await ZipFile.OpenAsync(BackupZipPath(backupPath), ZipArchiveMode.Update, cancellationToken))
        {
            foreach (var fileInfo in files)
            {
                var entryPath = Path.Combine(fileInfo.BackupName, fileInfo.StoredPath);
                
                var entry = archive.GetEntry(entryPath);
                if (entry == null)
                {
                    _console.MarkupLine($"[yellow]Entry not found in archive:[/] {entryPath}");
                    continue;
                }

                var destinationFileName = Path.Combine(destinationPath, fileInfo.RelativePath);
                await entry.ExtractToFileAsync(destinationFileName, cancellationToken);

                _console.MarkupLine($"[green]Decompressed:[/] {fileInfo.RelativePath}");
            }
        }
    }

    public async Task<string?> ReadFileContentAsync(string backupPath, string storedPath, CancellationToken cancellationToken)
    {
        if (!_fileSystem.FileExists(BackupZipPath(backupPath)))
            return null;

        using (var archive = await ZipFile.OpenReadAsync(BackupZipPath(backupPath), cancellationToken))
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
        using (var archive = await ZipFile.OpenAsync(BackupZipPath(backupPath), ZipArchiveMode.Update, cancellationToken))
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

    public async Task MoveFileAsync(string backupPath, string originStoredPath, string destinationStoredPath, CancellationToken cancellationToken)
    {
        if (!_fileSystem.FileExists(BackupZipPath(backupPath)))
        {
            _console.MarkupLine($"[red]Backup not found:[/] {BackupZipPath(backupPath)}");
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        using (var archive = await ZipFile.OpenAsync(BackupZipPath(backupPath), ZipArchiveMode.Update, cancellationToken))
        {
            var originEntry = archive.GetEntry(originStoredPath);
            if (originEntry == null)
            {
                _console.MarkupLine($"[yellow]Move skipped, entry not found:[/] {originStoredPath}");
                return;
            }

            // If destination exists, delete it first
            var existing = archive.GetEntry(destinationStoredPath);
            existing?.Delete();

            // Create destination entry and copy the content from origin
            var destinationEntry = archive.CreateEntry(destinationStoredPath, CompressionLevel.Optimal);

            using (var sourceStream = await originEntry.OpenAsync(cancellationToken))
            using (var destStream = await destinationEntry.OpenAsync(cancellationToken))
            {
                await sourceStream.CopyToAsync(destStream, 81920, cancellationToken);
            }

            // Try to preserve last write time
            try
            {
                destinationEntry.LastWriteTime = originEntry.LastWriteTime;
            }
            catch
            {
                // ignore if not supported
            }

            // Remove the original entry
            originEntry.Delete();

            _console.MarkupLine($"[green]Moved:[/] {originStoredPath} -> {destinationStoredPath}");
        }
    }

    public async Task RemoveFolderAsync(string backupPath, string firstBackupBackupName, CancellationToken cancellationToken)
    {
        if (!_fileSystem.FileExists(BackupZipPath(backupPath)))
        {
            _console.MarkupLine($"[red]Backup not found:[/] {BackupZipPath(backupPath)}");
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        using (var archive = await ZipFile.OpenAsync(BackupZipPath(backupPath), ZipArchiveMode.Update, cancellationToken))
        {
            // Normalize prefix to use forward slash which ZipArchive uses in FullName
            var prefix = firstBackupBackupName.TrimEnd('/', '\\') + "/";

            // Collect matching entries first to avoid modifying the collection while enumerating
            var toDelete = archive.Entries
                .Where(e => e.FullName.Replace('\\', '/').StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!toDelete.Any())
            {
                _console.MarkupLine($"[grey]No entries found for folder:[/] {firstBackupBackupName}");
                return;
            }

            foreach (var entry in toDelete)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    entry.Delete();
                    _console.MarkupLine($"[green]Deleted entry:[/] {entry.FullName}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _console.MarkupLine($"[red]Failed to delete entry:[/] {entry.FullName} - {ex.Message}");
                }
            }

            _console.MarkupLine($"[green]Removed folder:[/] {firstBackupBackupName}");
        }
    }
}