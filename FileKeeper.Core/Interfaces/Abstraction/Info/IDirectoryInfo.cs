namespace FileKeeper.Core.Interfaces.Abstraction.Info;

public interface IDirectoryInfo
{
    DateTime CreationTimeUtc { get; }
    string Name { get; }
}