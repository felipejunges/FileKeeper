using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileKeeper.Core.Interfaces.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using FileKeeper.UI.Models;
using System.Linq;
using System.Threading;

namespace FileKeeper.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ISnapshotRepository _snapshotRepository;

    [ObservableProperty] private string _windowTitle;
    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<SnapshotDto> Snapshots { get; } = [];

    public MainWindowViewModel(ISnapshotRepository snapshotRepository)
    {
        _snapshotRepository = snapshotRepository;
        WindowTitle = "FileKeeper - Backups Manager";

        // Constructors can't be async — fire-and-forget is the standard pattern here.
        // The underscore discards the Task intentionally; exceptions are caught inside.
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        IsLoading = true;
        var snapshots = await _snapshotRepository.GetAllSnapshotsAsync(CancellationToken.None);
        foreach (var dto in snapshots.Select(SnapshotDto.FromEntity))
            Snapshots.Add(dto);

        IsLoading = false;
    }

    [RelayCommand]
    private void CreateSnapshot()
    {
        // TODO: implement create snapshot logic
    }
}