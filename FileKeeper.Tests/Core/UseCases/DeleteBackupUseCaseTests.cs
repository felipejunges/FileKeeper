using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.UseCases;
using FileKeeper.Tests.Core.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FileKeeper.Tests.Core.UseCases;

public class DeleteBackupUseCaseTests : IAsyncLifetime
{
    private readonly DeleteBackupUseCase _sut;
    
    private readonly Mock<ISnapshotRepository> _snapshotRepository;
    private readonly FileWrapperMock _fileWrapper;

    public DeleteBackupUseCaseTests()
    {
        _snapshotRepository = new Mock<ISnapshotRepository>();
        _fileWrapper = new FileWrapperMock();
        
        _sut = new DeleteBackupUseCase(
            _snapshotRepository.Object,
            _fileWrapper,
            new NullLogger<DeleteBackupUseCase>());
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
    public void Assert_True()
    {
        Assert.True(true);
    }
}