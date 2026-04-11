using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileKeeper.Core.Application;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Models;
using FileKeeper.UI.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileKeeper.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly ICreateBackupUseCase _createBackupUseCase;

    [ObservableProperty] private double _backupProgress;
    [ObservableProperty] private bool _isBackupInProgress;
    [ObservableProperty] private string _statusMessage = string.Empty;
    
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isErrorVisible;
    
    [ObservableProperty] private string _windowTitle;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private SnapshotDto? _selectedSnapshot;
    [ObservableProperty] private ViewModelBase _currentDetailView;

    public SnapshotViewModel SnapshotView { get; }
    public SettingsViewModel SettingsView { get; }

    public ObservableCollection<SnapshotDto> Snapshots { get; } = [];

    public MainWindowViewModel(
        ISnapshotRepository snapshotRepository,
        ICreateBackupUseCase createBackupUseCase,
        SnapshotViewModel snapshotView,
        SettingsViewModel settingsView)
    {
        _snapshotRepository = snapshotRepository;
        _createBackupUseCase = createBackupUseCase;
        SnapshotView = snapshotView;
        SettingsView = settingsView;
        CurrentDetailView = SnapshotView;

        var version = ApplicationInfo.GetAppVersion();
        WindowTitle = $"FileKeeper v{version} - Backups Manager";

        if (ApplicationInfo.IsDebug)
            WindowTitle += " (debug)";

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
        IsErrorVisible = false;
        
        Snapshots.Clear();
        SelectedSnapshot = null;
        
        var snapshotsResult = await _snapshotRepository.GetAllSnapshotsAsync(cancellationToken);
        if (snapshotsResult.IsError)
        {
            ErrorMessage = $"Failed to load snapshots: {snapshotsResult.FirstError.Description}";
            IsErrorVisible = true;

            return;
        }

        var snapshots = snapshotsResult.Value;
        
        foreach (var dto in snapshots.Select(SnapshotDto.FromEntity))
            Snapshots.Add(dto);

        SelectedSnapshot = Snapshots.FirstOrDefault();
    }

    partial void OnSelectedSnapshotChanged(SnapshotDto? value)
    {
        SnapshotView.SetSnapshot(value);
        CurrentDetailView = SnapshotView;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        SelectedSnapshot = null;
        CurrentDetailView = SettingsView;
    }

    [RelayCommand]
    private async Task CreateSnapshot()
    {
        var ct = new CancellationTokenSource().Token;
        
        IsErrorVisible = false;
        IsBackupInProgress = true;
        BackupProgress = 0;
        StatusMessage = "Initializing backup...";
        
        var progress = new Progress<BackupProgress>(report =>
        {
            BackupProgress = report.Percentage;
            StatusMessage = report.Message;
        });
        
        var result = await _createBackupUseCase.ExecuteAsync(progress, ct);
        
        IsBackupInProgress = false;
        
        if (result.IsError)
        {
            ErrorMessage = $"Failed to create new backup: {result.FirstError.Description}";
            IsErrorVisible = true;
            StatusMessage = "Backup failed.";
        }
        else
        {
            StatusMessage = "Backup completed successfully!";
        }

        await LoadSnapshotsAsync(ct);
    }
}