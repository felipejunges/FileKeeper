using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using FileKeeper.Core.Models.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using FileKeeper.Core.Interfaces.Repositories;
using System.Threading;
using System.Threading.Tasks;

namespace FileKeeper.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IInitializable
{
    [ObservableProperty] private ObservableCollection<Backup> _backups = new();

    [ObservableProperty] private string _backupCountText = "Backups: 0";

    [ObservableProperty] private string _totalSizeText = "Total Size: 0 MB";

    private readonly IBackupRepository _backupRepository;
    
    public MainWindowViewModel(
        IBackupRepository backupRepository)
    {
        _backupRepository = backupRepository;
    }
    
    public async Task InitializeAsync()
    {
        var ct = new CancellationTokenSource().Token;
        var backupsResult = await _backupRepository.GetAllAsync(ct);
        var backups = backupsResult.IsError ? [] : backupsResult.Value;
        
        Backups = new ObservableCollection<Backup>(backups);
        
        BackupCountText = $"Backups: {Backups.Count}";
        TotalSizeText = "Total Size: 127 MB (Mocked)";
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
}