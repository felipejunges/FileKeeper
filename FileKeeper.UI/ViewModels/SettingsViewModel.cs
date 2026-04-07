using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Models.Options;
using FileKeeper.UI.Infrastructure.Services;
using Microsoft.Extensions.Options;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileKeeper.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IUserSettingsWriter _userSettingsWriter;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IOptionsMonitor<UserSettingsOptions> _userSettings;

    [ObservableProperty] private string _storageDirectory = string.Empty;
    [ObservableProperty] private string _ignoredFolders = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isErrorVisible;

    public ObservableCollection<string> SourceDirectories { get; } = [];

    public SettingsViewModel(
        IUserSettingsWriter userSettingsWriter,
        IFolderPickerService folderPickerService,
        IOptionsMonitor<UserSettingsOptions> userSettings)
    {
        _userSettingsWriter = userSettingsWriter;
        _folderPickerService = folderPickerService;
        _userSettings = userSettings;
        
        LoadFromOptions(userSettings.CurrentValue);
    }

    private void LoadFromOptions(UserSettingsOptions options)
    {
        SourceDirectories.Clear();
        foreach (var sourceDirectory in options.SourceDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            SourceDirectories.Add(sourceDirectory);
        }

        StorageDirectory = options.StorageDirectory;
        IgnoredFolders = string.Join(",", options.IgnoredFolders);
        StatusMessage = string.Empty;
        IsErrorVisible = false;
    }

    public void AddSourceDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        if (SourceDirectories.Any(path => string.Equals(path, directoryPath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        SourceDirectories.Add(directoryPath);
    }

    [RelayCommand]
    private void RemoveSourceDirectory(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        var existingPath = SourceDirectories.FirstOrDefault(path =>
            string.Equals(path, directoryPath, StringComparison.OrdinalIgnoreCase));

        if (existingPath is null)
        {
            return;
        }

        SourceDirectories.Remove(existingPath);
    }

    [RelayCommand]
    private async Task AddSourceFolderAsync()
    {
        var selectedFolder = await _folderPickerService.PickSingleFolderAsync("Select source folder", CancellationToken.None);

        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            return;
        }

        AddSourceDirectory(selectedFolder);
    }

    [RelayCommand]
    private async Task PickStorageFolderAsync()
    {
        var selectedFolder = await _folderPickerService.PickSingleFolderAsync("Select storage folder", CancellationToken.None);

        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            return;
        }

        StorageDirectory = selectedFolder;
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var sourceDirectories = SourceDirectories
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var options = new UserSettingsOptions
        {
            SourceDirectories = sourceDirectories,
            StorageDirectory = StorageDirectory.Trim(),
            IgnoredFolders = IgnoredFolders.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        };

        var result = await _userSettingsWriter.SaveAsync(options, CancellationToken.None);

        if (result.IsError)
        {
            StatusMessage = result.FirstError.Description;
            IsErrorVisible = true;
            return;
        }

        StatusMessage = "Settings saved successfully.";
        IsErrorVisible = false;
    }

    [RelayCommand]
    private void ReloadSettings()
    {
        LoadFromOptions(_userSettings.CurrentValue);
        StatusMessage = "Settings reloaded.";
        IsErrorVisible = false;
    }
}

