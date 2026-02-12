using FileKeeper.Core.Interfaces.Abstraction.Info;

namespace FileKeeper.Core.Services.Abstraction.Info;

public class FileInfoWrapper : IFileInfo
{
    private readonly FileInfo _fileInfo;

    public FileInfoWrapper(string path)
    {
        _fileInfo = new FileInfo(path);
    }

    public string Name => _fileInfo.Name;
    public string FullName => _fileInfo.FullName;
    public long Length => _fileInfo.Length;
    public DateTime LastWriteTimeUtc => _fileInfo.LastWriteTimeUtc;
    public DateTime CreationTimeUtc => _fileInfo.CreationTimeUtc;
}