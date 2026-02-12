using FileKeeper.Core.Interfaces.Abstraction.Info;

namespace FileKeeper.Core.Services.Abstraction.Info;

public class DirectoryInfoWrapper : IDirectoryInfo
{
    private readonly DirectoryInfo _directoryInfo;

    public DirectoryInfoWrapper(string path)
    {
        _directoryInfo = new DirectoryInfo(path);
    }

    public DateTime CreationTimeUtc => _directoryInfo.CreationTimeUtc;
    public string Name => _directoryInfo.Name;
}