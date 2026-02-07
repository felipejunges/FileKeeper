namespace FileKeeper.Core.Interfaces;

public interface IHashingService
{
    string ComputeHash(string filePath);
}