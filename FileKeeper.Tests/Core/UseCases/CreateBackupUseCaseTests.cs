using ErrorOr;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.Wrappers;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.UseCases;
using Moq;

namespace FileKeeper.Tests.Core.UseCases;

public class CreateBackupUseCaseTests : IAsyncLifetime
{
    private CreateBackupUseCase _sut;

    private readonly Mock<ISnapshotRepository> _snapshotRepository;
    private readonly Mock<IFileWrapper> _fileWrapper;
    private readonly Mock<IFilesService> _fileService;
    private readonly Mock<ICompressedEncryptedFileWriter> _compressedEncryptedFileWriter;
    private readonly Mock<IConfigurationService> _configurationService;

    public CreateBackupUseCaseTests()
    {
        _snapshotRepository = new Mock<ISnapshotRepository>();
        _fileWrapper = new Mock<IFileWrapper>();
        _fileService = new Mock<IFilesService>();
        _compressedEncryptedFileWriter = new Mock<ICompressedEncryptedFileWriter>();
        _configurationService = new Mock<IConfigurationService>();

        _sut = new CreateBackupUseCase(
            _snapshotRepository.Object,
            _fileWrapper.Object,
            _fileService.Object,
            _compressedEncryptedFileWriter.Object,
            _configurationService.Object);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFail_IfGetLastSnapshotReturnsError()
    {
        // Arrange
        _snapshotRepository
            .Setup(s => s.GetLastSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.Failure(description: "Error getting last snapshot"));

        // Act
        var result = await _sut.ExecuteAsync(null, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Error getting last snapshot", result.FirstError.Description);

        _snapshotRepository
            .Verify(v => v.AddSnapshotAsync(It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}