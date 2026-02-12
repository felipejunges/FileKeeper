using FileKeeper.Core.Interfaces.Abstraction.Info;

namespace FileKeeper.Tests.Core.Mocks;

public class MockFileInfo : IFileInfo
{
    public MockFileInfo(
        string name,
        string fullName,
        long length,
        DateTime lastWriteTimeUtc,
        DateTime creationTimeUtc)
    {
        Name = name;
        FullName = fullName;
        Length = length;
        LastWriteTimeUtc = lastWriteTimeUtc;
        CreationTimeUtc = creationTimeUtc;
    }

    public string Name { get; private set; }
    public string FullName { get; private set; }
    public long Length { get; private set; }
    public DateTime LastWriteTimeUtc { get; private set; }
    public DateTime CreationTimeUtc { get; private set; }
}