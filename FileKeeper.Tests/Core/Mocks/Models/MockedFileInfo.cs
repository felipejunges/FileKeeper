using FileKeeper.Core.Interfaces.Abstraction.Info;

namespace FileKeeper.Tests.Core.Mocks.Models;

public class MockedFileInfo : IFileInfo
{
    public MockedFileInfo(
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

    public MockedFileInfo(MockedFile mockedFile)
    {
        Name = mockedFile.Name;
        FullName = mockedFile.FullName;
        Length = mockedFile.Length;
        LastWriteTimeUtc = DateTime.UtcNow;
        CreationTimeUtc = DateTime.UtcNow;
    }

    public string Name { get; private set; }
    public string FullName { get; private set; }
    public long Length { get; private set; }
    public DateTime LastWriteTimeUtc { get; private set; }
    public DateTime CreationTimeUtc { get; private set; }
}