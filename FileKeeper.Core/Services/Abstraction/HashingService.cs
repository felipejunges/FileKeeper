using System.Security.Cryptography;
using FileKeeper.Core.Interfaces;

namespace FileKeeper.Core.Services.Abstraction;

public class HashingService : IHashingService
{
    public string ComputeHash(string filePath)
    {
        using (var sha256 = SHA256.Create())
        {
            using (var stream = File.OpenRead(filePath))
            {
                var hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}