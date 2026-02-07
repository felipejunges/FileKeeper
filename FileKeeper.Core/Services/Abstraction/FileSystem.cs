using FileKeeper.Core.Interfaces;

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

    public long GetFileLength(string path) => new FileInfo(path).Length;
    public DateTime GetFileLastWriteTimeUtc(string path) => new FileInfo(path).LastWriteTimeUtc;

    public DateTime GetDirectoryCreationTimeUtc(string path) => new DirectoryInfo(path).CreationTimeUtc;
    public string GetDirectoryName(string path) => new DirectoryInfo(path).Name;

    public void DeleteFile(string path) => File.Delete(path);
    public DateTime GetFileCreationTimeUtc(string path) => new FileInfo(path).CreationTimeUtc;
}