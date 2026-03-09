using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using FileKeeper.Core.Models.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.UseCases;
using System.Threading;
using System.Threading.Tasks;

namespace FileKeeper.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IInitializable
{
    [ObservableProperty] private ObservableCollection<Backup> _backups = new();

    [ObservableProperty] private string _backupCountText = "Backups: 0";

    [ObservableProperty] private string _totalSizeText = "Total Size: 0 MB";
    
    [ObservableProperty] private string _errorMessage = string.Empty;
    
    [ObservableProperty] private bool _isErrorVisible = false;

    private readonly IBackupRepository _backupRepository;
    
    private readonly ICreateBackupUseCase _createBackupUseCase;
    
    public MainWindowViewModel(
        IBackupRepository backupRepository,
        ICreateBackupUseCase createBackupUseCase)
    {
        _backupRepository = backupRepository;
        _createBackupUseCase = createBackupUseCase;
    }
    
    public async Task InitializeAsync()
    {
        var ct = new CancellationTokenSource().Token;
        await UpdateBackupListAsync(ct);
        
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
            ErrorMessage = $"Falha ao criar backup: {result.FirstError.Description}";
            IsErrorVisible = true;
        }
        
        await UpdateBackupListAsync(cancellationToken);
    }
}