using FileKeeper.Core.Interfaces.Abstraction;
using FileKeeper.Tests.Core.Mocks.Models;

namespace FileKeeper.Tests.Core.Mocks;

public class MockedFileSystem : IFileSystem
{
    private readonly List<MockedFile> _files;

    public MockedFileSystem(List<MockedFile> files)
    {
        _files = files;
    }

    public bool DirectoryExists(string path)
    {
        throw new NotImplementedException();
    }

    public void CreateDirectory(string path)
    {
        throw new NotImplementedException();
    }

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
    {
        return _files.Select(f => f.FullName).ToArray();
    }

    public string[] GetDirectories(string path)
    {
        throw new NotImplementedException();
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        throw new NotImplementedException();
    }

    public bool FileExists(string path)
    {
        return _files.Any(f => f.FullName == path);
    }

    public void CopyFile(string source, string dest)
    {
        throw new NotImplementedException();
    }

    public void MoveFile(string source, string dest)
    {
        throw new NotImplementedException();
    }

    public string ReadAllText(string path)
    {
        throw new NotImplementedException();
    }

    public void WriteAllText(string path, string contents)
    {
        throw new NotImplementedException();
    }

    public void DeleteFile(string path)
    {
        throw new NotImplementedException();
    }

    public Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken)
    {
        var file = _files.First(f => f.FullName == filePath);
        return Task.FromResult(file.Hash);
    }
}