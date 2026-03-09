using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileKeeper.Core.Models;
using FileKeeper.Core.Interfaces.Services;

namespace FileKeeper.UI.ViewModels;

public partial class ConfigurationWindowViewModel : ViewModelBase
{
    private readonly IConfigurationService? _configService;

    [ObservableProperty]
    private string _databaseLocation = string.Empty;

    [ObservableProperty]
    private int _versionsToKeep;

    [ObservableProperty]
    private int _autoBackupIntervalMinutes;

    [ObservableProperty]
    private long _maxDatabaseSizeMb;

    [ObservableProperty]
    private bool _enableCompression;

    public ConfigurationWindowViewModel()
    {
        // Parameterless constructor for XAML previewer or simple DI
        LoadMockData();
    }

    public ConfigurationWindowViewModel(IConfigurationService configService)
    {
        _configService = configService;
        LoadConfiguration();
    }

    private void LoadMockData()
    {
        DatabaseLocation = "C:\\FileKeeper\\filekeeper.db";
        VersionsToKeep = 5;
        AutoBackupIntervalMinutes = 60;
        MaxDatabaseSizeMb = 1024;
        EnableCompression = true;
    }

    private async void LoadConfiguration()
    {
        if (_configService == null) return;
        var config = await _configService.GetConfigurationAsync(default);
        DatabaseLocation = config.DatabaseLocation;
        VersionsToKeep = config.VersionsToKeep;
        AutoBackupIntervalMinutes = config.AutoBackupIntervalMinutes;
        MaxDatabaseSizeMb = config.MaxDatabaseSizeMb;
        EnableCompression = config.EnableCompression;
    }

    [RelayCommand]
    private async Task SaveConfiguration()
    {
        if (_configService == null) return;
        
        var config = new Configuration
        {
            DatabaseLocation = DatabaseLocation,
            VersionsToKeep = VersionsToKeep,
            AutoBackupIntervalMinutes = AutoBackupIntervalMinutes,
            MaxDatabaseSizeMb = MaxDatabaseSizeMb,
            EnableCompression = EnableCompression
        };

        await _configService.ApplyConfigurationAsync(config, default);
    }
}
