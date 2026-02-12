namespace FileKeeper.Core.Interfaces.Abstraction.Info;

public interface IFileInfo
{
    string Name { get; }
    string FullName { get; }
    long Length { get; }
    DateTime LastWriteTimeUtc { get; }
    DateTime CreationTimeUtc { get; }
}