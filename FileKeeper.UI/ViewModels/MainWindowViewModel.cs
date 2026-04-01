using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.UI.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileKeeper.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly ICreateBackupUseCase _createBackupUseCase;

    [ObservableProperty] private string _windowTitle;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private SnapshotDto? _selectedSnapshot;

    public SnapshotViewModel SnapshotView { get; } = new();

    public ObservableCollection<SnapshotDto> Snapshots { get; } = [];

    public MainWindowViewModel(
        ISnapshotRepository snapshotRepository,
        ICreateBackupUseCase createBackupUseCase)
    {
        _snapshotRepository = snapshotRepository;
        _createBackupUseCase = createBackupUseCase;

        WindowTitle = "FileKeeper - Backups Manager";

        // Constructors can't be async — fire-and-forget is the standard pattern here.
        // The underscore discards the Task intentionally; exceptions are caught inside.
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        IsLoading = true;

        await LoadSnapshotsAsync(CancellationToken.None);

        IsLoading = false;
    }

    private async Task LoadSnapshotsAsync(CancellationToken cancellationToken)
    {
        Snapshots.Clear();
        
        var snapshots = await _snapshotRepository.GetAllSnapshotsAsync(cancellationToken);
        foreach (var dto in snapshots.Select(SnapshotDto.FromEntity))
            Snapshots.Add(dto);

        SelectedSnapshot = Snapshots.FirstOrDefault();
    }

    partial void OnSelectedSnapshotChanged(SnapshotDto? value)
    {
        SnapshotView.SetSnapshot(value);
    }

    [RelayCommand]
    private async Task CreateSnapshot()
    {
        var ct = new CancellationTokenSource().Token;
        
        await _createBackupUseCase.ExecuteAsync(null, ct);

        await LoadSnapshotsAsync(ct);
    }
}