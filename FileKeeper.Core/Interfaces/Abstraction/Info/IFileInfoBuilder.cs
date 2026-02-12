namespace FileKeeper.Core.Interfaces.Abstraction.Info;

public interface IFileInfoBuilder
{
    IFileInfo Build(string path);
}