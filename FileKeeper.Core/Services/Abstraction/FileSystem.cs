using FileKeeper.Core.Interfaces.Abstraction;
using FileKeeper.Core.Utils;

namespace FileKeeper.Core.Services.Abstraction;

public class FileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.GetFiles(path, searchPattern, searchOption);

    public string[] GetDirectories(string path) => Directory.GetDirectories(path);
    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

    public bool FileExists(string path) => File.Exists(path);
    public void CopyFile(string source, string dest) => File.Copy(source, dest);
    public void MoveFile(string source, string dest) => File.Move(source, dest);
    public string ReadAllText(string path) => File.ReadAllText(path);
    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

    public void DeleteFile(string path) => File.Delete(path);
    
    public async Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken)
    {
        using (var stream = File.OpenRead(filePath))
        {
            return await HashingUtils.ComputeHashFromStreamAsync(stream, cancellationToken);
        }
    }
}