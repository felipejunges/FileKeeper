using System.IO.Compression;
using FileKeeper.Core.Interfaces;
using Spectre.Console;

namespace FileKeeper.Core.Services.Abstraction;

public class CompressionService : ICompressionService
{
    private readonly IAnsiConsole _console;
    
    public CompressionService(IAnsiConsole console)
    {
        _console = console;
    }
    
    public void CompressFiles(IList<(string FullPath, string StoredPath)> files, string backupPath, string fileNameWithoutExtension)
    {
        var zipPath = Path.Combine(backupPath, $"{fileNameWithoutExtension}.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach (var fileInfo in files)
            {
                archive.CreateEntryFromFile(fileInfo.FullPath, fileInfo.StoredPath);
            }
        }
        
        using (var archive = ZipFile.OpenRead(zipPath))
        {
            foreach (var entry in archive.Entries)
            {
                var storedPath = files.FirstOrDefault(x => x.StoredPath == entry.FullName).StoredPath;
                var entryName = entry.FullName;
                long entrySize = entry.Length;
                long compressedSize = entry.CompressedLength;
                var lastWrite = entry.LastWriteTime.DateTime;

                _console.MarkupLine($"[green]Compressed:[/] {storedPath}  [grey](Entry: {entryName}, Size: {entrySize} bytes, Compressed: {compressedSize} bytes, Modified: {lastWrite:O})[/]");
            }
        }
    }
}