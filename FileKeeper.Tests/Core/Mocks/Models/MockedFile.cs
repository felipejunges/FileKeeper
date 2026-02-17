namespace FileKeeper.Tests.Core.Mocks.Models;

public record MockedFile(
    string Name,
    string FullName,
    long Length,
    string Hash);