using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FileKeeper.Core;
using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.UseCases;
using System;
using System.Collections.ObjectModel;
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

    [ObservableProperty] private bool _isErrorVisible = false;

    private readonly IBackupRepository _backupRepository = null!;

    private readonly ICreateBackupUseCase _createBackupUseCase = null!;

    public MainWindowViewModel()
    {
    }
    
    public MainWindowViewModel(
        IBackupRepository backupRepository,
        ICreateBackupUseCase createBackupUseCase)
    {
        _backupRepository = backupRepository;
        _createBackupUseCase = createBackupUseCase;

        WeakReferenceMessenger.Default.Register(this);
    }

    public async Task InitializeAsync()
    {
        var ct = new CancellationTokenSource().Token;
        await UpdateBackupListAsync(ct);

        UpdateFooter();
    }

    private void UpdateFooter()
    {
        BackupCountText = $"Backups: {Backups.Count}";
        TotalSizeText = "Total Size: 127 MB (Mocked)";
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
    private async Task CreateBackup(CancellationToken cancellationToken)
    {
        IsErrorVisible = false;

        var result = await _createBackupUseCase.ExecuteAsync(cancellationToken);

        if (result.IsError)
        {
            ErrorMessage = $"Failed to save backup: {result.FirstError.Description}";
            IsErrorVisible = true;
        }

        await UpdateBackupListAsync(cancellationToken);
    }

    public void Receive(BackupDeletedMessage message)
    {
        _ = HandleBackupDeletedAsync();
    }

    private async Task HandleBackupDeletedAsync()
    {
        try
        {
            await UpdateBackupListAsync(CancellationToken.None);
            UpdateFooter();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to refresh backups: {ex.Message}";
            IsErrorVisible = true;
        }
    }
}