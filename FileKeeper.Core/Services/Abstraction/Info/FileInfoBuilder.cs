using FileKeeper.Core.Interfaces.Abstraction.Info;

namespace FileKeeper.Core.Services.Abstraction.Info;

public class FileInfoBuilder : IFileInfoBuilder
{
    public IFileInfo Build(string path) => new FileInfoWrapper(path);
}