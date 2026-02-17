
using FileKeeper.Core.Interfaces.Abstraction.Info;
using FileKeeper.Tests.Core.Mocks.Models;

namespace FileKeeper.Tests.Core.Mocks;

public class MockedFileInfoBuilder : IFileInfoBuilder
{
    private readonly List<MockedFile> _files;

    public MockedFileInfoBuilder(List<MockedFile> files)
    {
        _files = files;
    }
    
    public IFileInfo Build(string path)
    {
        var file = _files.First(f => f.FullName == path);
        return new MockedFileInfo(file);
    }
}