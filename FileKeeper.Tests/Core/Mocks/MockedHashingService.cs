using System.Security.Cryptography;
using System.Text;
using FileKeeper.Core.Interfaces.Abstraction;
using FileKeeper.Tests.Core.Mocks.Models;

namespace FileKeeper.Tests.Core.Mocks;

public class MockedHashingService : IHashingService
{
    private readonly List<MockedFile> _files;

    public MockedHashingService(List<MockedFile> files)
    {
        _files = files;
    }
    
    public Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken)
    {
        var file = _files.First(f => f.FullName == filePath);
        return Task.FromResult(file.Hash);
    }

    public Task<string> ComputeHashFromStringAsync(string content, CancellationToken cancellationToken)
    {
        using var sha1 = SHA1.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = sha1.ComputeHash(bytes);
        var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        return Task.FromResult(hash);
    }
}