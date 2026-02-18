namespace FileKeeper.Core.Interfaces.Abstraction;

public interface IFileSystem
{
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
    string[] GetDirectories(string path);
    void DeleteDirectory(string path, bool recursive);

    bool FileExists(string path);
    void CopyFile(string source, string dest);
    void MoveFile(string source, string dest);
    string ReadAllText(string path);
    void WriteAllText(string path, string contents);

    void DeleteFile(string path);
    
    Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken);
}