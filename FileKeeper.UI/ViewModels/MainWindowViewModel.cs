using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using FileKeeper.Core.Models.Entities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FileKeeper.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<Backup> _backups = new();

    [ObservableProperty]
    private string _backupCountText = "Backups: 0";

    [ObservableProperty]
    private string _totalSizeText = "Total Size: 0 MB";

    public MainWindowViewModel()
    {
        // Mock data
        Backups.Add(new Backup(1, DateTime.Now.AddDays(-1), 10, 5, 2));
        Backups.Add(new Backup(2, DateTime.Now, 2, 8, 1));
        
        UpdateFooter();
    }

    private void UpdateFooter()
    {
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
