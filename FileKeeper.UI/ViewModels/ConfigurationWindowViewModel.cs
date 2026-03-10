using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UI;
using FileKeeper.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileKeeper.UI.ViewModels;

public partial class ConfigurationWindowViewModel : ViewModelBase, IInitializable
{
    private readonly IConfigurationService? _configurationService;

    private readonly IFolderPickerService _folderPickerService;
    
    public event Action? RequestClose;

    [ObservableProperty] private string _databaseLocation = string.Empty;

    [ObservableProperty] private int _versionsToKeep;

    [ObservableProperty] private int _autoBackupIntervalMinutes;

    [ObservableProperty] private long _maxDatabaseSizeMb;

    [ObservableProperty] private bool _enableCompression;
    
    [ObservableProperty] private string _errorMessage = string.Empty;
    
    [ObservableProperty] private bool _isErrorVisible = false;

    [ObservableProperty] private ObservableCollection<string> _monitoredFolders = new();

    private string? _currentRestoreDestination;

    public ConfigurationWindowViewModel(
        IConfigurationService configurationService,
        IFolderPickerService folderPickerService)
    {
        _configurationService = configurationService;
        _folderPickerService = folderPickerService;
    }
    
    public async Task InitializeAsync()
    {
        var ct = new CancellationTokenSource().Token;
        await LoadConfigurationAsync(ct);
    }

    private async Task LoadConfigurationAsync(CancellationToken cancellationToken)
    {
        if (_configurationService == null) return;
        var configuration = await _configurationService.GetConfigurationAsync(cancellationToken);
        
        DatabaseLocation = configuration.DatabaseLocation;
        VersionsToKeep = configuration.VersionsToKeep;
        AutoBackupIntervalMinutes = configuration.AutoBackupIntervalMinutes;
        MaxDatabaseSizeMb = configuration.MaxDatabaseSizeMb;
        EnableCompression = configuration.EnableCompression;
        
        MonitoredFolders.Clear();
        foreach (var folder in configuration.MonitoredFolders)
        {
            MonitoredFolders.Add(folder);
        }
        
        _currentRestoreDestination = configuration.CurrentRestoreDestination;
    }

    [RelayCommand]
    private async Task SaveConfiguration(CancellationToken cancellationToken)
    {
        IsErrorVisible = false;
        
        if (_configurationService == null) return;

        var config = new Configuration
        {
            DatabaseLocation = DatabaseLocation,
            VersionsToKeep = VersionsToKeep,
            AutoBackupIntervalMinutes = AutoBackupIntervalMinutes,
            MaxDatabaseSizeMb = MaxDatabaseSizeMb,
            EnableCompression = EnableCompression,
            MonitoredFolders = MonitoredFolders.ToList(),
            CurrentRestoreDestination = _currentRestoreDestination
        };

        var result = await _configurationService.ApplyConfigurationAsync(config, cancellationToken);

        if (result.IsError)
        {
            ErrorMessage = $"Failed to save configuration: {result.FirstError.Description}";
            IsErrorVisible = true;
            
            return;
        }
        
        RequestClose?.Invoke();
    }
    
    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke();
    }
    
    [RelayCommand]
    private async Task AddFolder()
    {
        var ct = new  CancellationTokenSource().Token;
        var folder = await _folderPickerService.PickFolderAsync("Select a folder to include", ct);
        
        if (!string.IsNullOrWhiteSpace(folder) && !MonitoredFolders.Contains(folder) && Directory.Exists(folder))
        {
            MonitoredFolders.Add(folder);
        }
    }
    
    [RelayCommand]
    private void RemoveFolder(string folder)
    {
        MonitoredFolders.Remove(folder);
    }
}