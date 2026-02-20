using FileKeeper.Core.Interfaces.Abstraction;
using FileKeeper.Core.Interfaces.Abstraction.Info;
using FileKeeper.Core.Services.Abstraction;
using FileKeeper.Tests.Core.Mocks;
using FileKeeper.Tests.Core.Mocks.Models;
using Moq;

namespace FileKeeper.Tests.Core.Services.Abstraction;

public class FileSourceServiceTests
{
    private readonly FileSourceService _sut;
    
    private List<MockedFile> _mockedFiles = new();
    
    private readonly IFileSystem _fileSystem;
    private readonly IFileInfoBuilder _fileInfoBuilder;

    public FileSourceServiceTests()
    {
        _fileSystem = new MockedFileSystem(_mockedFiles);
        _fileInfoBuilder = new MockedFileInfoBuilder(_mockedFiles);
        
        _sut = new FileSourceService(
            _fileSystem,
            _fileInfoBuilder);
    }
    
    [Fact]
    public async Task ScanLocalFolder_ShouldReturnCorrectly() 
    {
        // Arrange
        var mockedFiles = new List<MockedFile>
        {
            new MockedFile(
                "file1.txt",
                "/var/www/html/file1.txt",
                1000,
                "Hash_File1_V1")
        };
        
        _mockedFiles.Clear();
        _mockedFiles.AddRange(mockedFiles);

        // Act
        var result = await _sut.ScanLocalFolderAsync(
            "/var/www/html",
            Array.Empty<string>(),
            CancellationToken.None);
        
        // Assert
        Assert.Single(result);
        Assert.Equal("file1.txt", result[0].RelativePath);
        Assert.Equal("Hash_File1_V1", result[0].Hash);
    }
    
    [Fact]
    public async Task ScanLocalFolder_ShouldIgnoreExcludedPatterns() 
    {
        // Arrange
        var mockedFiles = new List<MockedFile>
        {
            new MockedFile(
                "file1.txt",
                "/var/www/html/file1.txt",
                1000,
                "Hash_File1_V1"),
            new MockedFile(
                "file2.txt",
                "/var/www/html/bin/file2.txt",
                2000,
                "Hash_File2_V1"),
            new MockedFile(
                "file1.txt",
                "/var/www/html/obj/file3.txt",
                3000,
                "Hash_File3_V1"),
            new MockedFile(
                "file4.txt",
                "/var/www/html/pub/file4.txt",
                4000,
                "Hash_File4_V1")
        };
        
        _mockedFiles.Clear();
        _mockedFiles.AddRange(mockedFiles);

        // Act
        var result = await _sut.ScanLocalFolderAsync(
            "/var/www/html",
            new [] { "bin", "obj" },
            CancellationToken.None);
        
        // Assert
        Assert.Equal(2, result.Count);
        
        Assert.Equal("file1.txt", result[0].RelativePath);
        Assert.Equal("Hash_File1_V1", result[0].Hash);
        Assert.Equal("pub/file4.txt", result[1].RelativePath);
        Assert.Equal("Hash_File4_V1", result[1].Hash);
    }
}