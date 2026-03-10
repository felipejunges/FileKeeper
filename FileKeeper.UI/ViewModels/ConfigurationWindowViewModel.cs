using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileKeeper.Core.Models;
using FileKeeper.Core.Interfaces.Services;
using System;
using System.Threading;

namespace FileKeeper.UI.ViewModels;

public partial class ConfigurationWindowViewModel : ViewModelBase, IInitializable
{
    private readonly IConfigurationService? _configurationService;
    
    public event Action? RequestClose;

    [ObservableProperty] private string _databaseLocation = string.Empty;

    [ObservableProperty] private int _versionsToKeep;

    [ObservableProperty] private int _autoBackupIntervalMinutes;

    [ObservableProperty] private long _maxDatabaseSizeMb;

    [ObservableProperty] private bool _enableCompression;
    
    [ObservableProperty] private string _errorMessage = string.Empty;
    
    [ObservableProperty] private bool _isErrorVisible = false;

    private string? _currentRestoreDestination;

    public ConfigurationWindowViewModel(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
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
}