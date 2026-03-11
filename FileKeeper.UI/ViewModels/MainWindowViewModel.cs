using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FileKeeper.Core;
using FileKeeper.Core.Extensions;
using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Models;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileKeeper.UI.ViewModels;

public partial class MainWindowViewModel
    : ViewModelBase,
        IInitializable,
        IRecipient<BackupDeletedMessage>
{
    [ObservableProperty] private ObservableCollection<Backup> _backups = new();

    [ObservableProperty] private string _backupCountText = "Backups: 0";

    [ObservableProperty] private string _totalSizeText = "Total Size: 0 MB";

    [ObservableProperty] private string _errorMessage = string.Empty;

    [ObservableProperty] private bool _isErrorVisible;

    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty] private double _backupProgress = 0;

    [ObservableProperty] private bool _isBackupInProgress = false;

    private readonly IBackupRepository _backupRepository = null!;

    private readonly ICreateBackupUseCase _createBackupUseCase = null!;

    private readonly IRecycleOldBackupUseCase _recycleOldBackupUseCase = null!;

    private readonly IDatabaseService _databaseService = null!;

    private Window? _window;
    
    public MainWindowViewModel()
    {
    }
    
    public MainWindowViewModel(
        IBackupRepository backupRepository,
        ICreateBackupUseCase createBackupUseCase,
        IRecycleOldBackupUseCase recycleOldBackupUseCase,
        IDatabaseService databaseService)
    {
        _backupRepository = backupRepository;
        _createBackupUseCase = createBackupUseCase;
        _recycleOldBackupUseCase = recycleOldBackupUseCase;
        _databaseService = databaseService;

        WeakReferenceMessenger.Default.Register(this);
    }

    public async Task InitializeAsync()
    {
        var ct = new CancellationTokenSource().Token;
        await UpdateBackupListAsync(ct);
        await UpdateFooterAsync(ct);
    }

    private async Task UpdateFooterAsync(CancellationToken cancellationToken)
    {
        var dbSizeResult = await _databaseService.GetDatabaseSizeAsync(cancellationToken);
        var dbSize = dbSizeResult.Match(d => d, _ => 0);
        
        BackupCountText = $"Backups: {Backups.Count}";
        TotalSizeText = $"Total Size: {Backups.Sum(b => b.TotalSize).ToHumanReadableSize()} / {dbSize.ToHumanReadableSize()}";
    }

    private async Task UpdateBackupListAsync(CancellationToken cancellationToken)
    {
        var backupsResult = await _backupRepository.GetAllAsync(cancellationToken);
        var backups = backupsResult.IsError ? [] : backupsResult.Value;

        Backups = new ObservableCollection<Backup>(backups);
    }

    [RelayCommand]
    private void OpenConfiguration()
    {
        App.ShowConfigurationWindow();
    }

    [RelayCommand]
    private void OpenFiles()
    {
        App.ShowFilesWindow();
    }

    [RelayCommand]
    private void OpenBackupDetails(Backup backup)
    {
        App.ShowBackupWindow(backup);
    }

    [RelayCommand]
    private async Task RecycleOldBackupAsync(CancellationToken cancellationToken)
    {
        var confirm = await DialogBuilder.CreateConfirmation()
            .WithTitle("Confirm recycle?")
            .WithMessage("Confirm recycle of the oldest backup? This will delete the oldest backup permanently.")
            .ShowAndWaitForYesAsync(_window!);

        if (!confirm)
            return;
        
        IsErrorVisible = false;
        
        var result = await _recycleOldBackupUseCase.ExecuteAsync(cancellationToken);
        
        if (result.IsError)
        {
            ErrorMessage = $"Failed to create new backup: {result.FirstError.Description}";
            IsErrorVisible = true;
        }
        
        await UpdateBackupListAsync(cancellationToken);
        await UpdateFooterAsync(cancellationToken);
    }
    
    [RelayCommand]
    private async Task CreateBackup(CancellationToken cancellationToken)
    {
        IsErrorVisible = false;
        IsBackupInProgress = true;
        BackupProgress = 0;
        StatusMessage = "Initializing backup...";

        var progress = new Progress<BackupProgress>(report =>
        {
            BackupProgress = report.Percentage;
            StatusMessage = report.Message;
        });

        var result = await _createBackupUseCase.ExecuteAsync(progress, cancellationToken);

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

        await UpdateBackupListAsync(cancellationToken);
        await UpdateFooterAsync(cancellationToken);
    }

    public void Receive(BackupDeletedMessage message)
    {
        _ = HandleBackupDeletedAsync();
    }

    private async Task HandleBackupDeletedAsync()
    {
        var cancellationToken = new CancellationTokenSource().Token;
        
        try
        {
            await UpdateBackupListAsync(cancellationToken);
            await UpdateFooterAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to refresh backups: {ex.Message}";
            IsErrorVisible = true;
        }
    }
    
    public void SetWindow(Window window) => _window = window;
}