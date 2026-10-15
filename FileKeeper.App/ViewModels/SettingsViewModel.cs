using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileKeeper.Core.Models;

namespace FileKeeper.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private const long BytesPerMb = 1024 * 1024;
    private bool _loading;

    [ObservableProperty]
    private string _destinationFile = string.Empty;

    [ObservableProperty]
    private bool _limitEnabled;

    [ObservableProperty]
    private decimal? _maxSizeMb = 1024;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddIgnoredNameCommand))]
    private string _newIgnoredName = string.Empty;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    public SettingsViewModel(MainWindowViewModel main)
    {
        Main = main;
        SourceFolders.CollectionChanged += (_, _) => UpdateDirty();
        IgnoredFolders.CollectionChanged += (_, _) => UpdateDirty();
        LoadFrom(main.CurrentSettings);
    }

    public MainWindowViewModel Main { get; }

    public ObservableCollection<string> SourceFolders { get; } = [];

    public ObservableCollection<string> IgnoredFolders { get; } = [];

    partial void OnDestinationFileChanged(string value) => UpdateDirty();
    partial void OnLimitEnabledChanged(bool value) => UpdateDirty();
    partial void OnMaxSizeMbChanged(decimal? value) => UpdateDirty();

    private void LoadFrom(BackupSettings settings)
    {
        _loading = true;
        SourceFolders.Clear();
        foreach (var folder in settings.SourceFolders)
            SourceFolders.Add(folder);
        IgnoredFolders.Clear();
        foreach (var folder in settings.IgnoredFolders)
            IgnoredFolders.Add(folder);
        DestinationFile = settings.DestinationFile;
        LimitEnabled = settings.MaxBackupSizeBytes.HasValue;
        if (settings.MaxBackupSizeBytes is { } max)
            MaxSizeMb = Math.Round((decimal)max / BytesPerMb, 2);
        _loading = false;
        HasUnsavedChanges = false;
    }

    private BackupSettings ToSettings() => new()
    {
        SourceFolders = [.. SourceFolders],
        DestinationFile = DestinationFile.Trim(),
        IgnoredFolders = [.. IgnoredFolders],
        MaxBackupSizeBytes = LimitEnabled && MaxSizeMb is > 0 ? (long)(MaxSizeMb.Value * BytesPerMb) : null,
    };

    private void UpdateDirty()
    {
        if (_loading)
            return;
        var current = Main.CurrentSettings;
        var edited = ToSettings();
        HasUnsavedChanges =
            !current.SourceFolders.SequenceEqual(edited.SourceFolders) ||
            !current.IgnoredFolders.SequenceEqual(edited.IgnoredFolders) ||
            current.DestinationFile != edited.DestinationFile ||
            current.MaxBackupSizeBytes != edited.MaxBackupSizeBytes;
    }

    [RelayCommand]
    private async Task AddSourceAsync()
    {
        var folder = await PickFolder("Select a folder to back up");
        if (folder is not null && !SourceFolders.Contains(folder))
            SourceFolders.Add(folder);
    }

    [RelayCommand]
    private void RemoveSource(string folder) => SourceFolders.Remove(folder);

    [RelayCommand]
    private async Task AddIgnoredFolderAsync()
    {
        var folder = await PickFolder("Select a folder to ignore");
        if (folder is not null && !IgnoredFolders.Contains(folder))
            IgnoredFolders.Add(folder);
    }

    [RelayCommand(CanExecute = nameof(CanAddIgnoredName))]
    private void AddIgnoredName()
    {
        var name = NewIgnoredName.Trim();
        if (!IgnoredFolders.Contains(name))
            IgnoredFolders.Add(name);
        NewIgnoredName = string.Empty;
    }

    private bool CanAddIgnoredName() => !string.IsNullOrWhiteSpace(NewIgnoredName);

    [RelayCommand]
    private void RemoveIgnored(string entry) => IgnoredFolders.Remove(entry);

    [RelayCommand]
    private async Task BrowseDestinationAsync()
    {
        if (Main.Dialogs is null)
            return;
        var name = string.IsNullOrWhiteSpace(DestinationFile) ? "FileKeeper-backup.zip" : Path.GetFileName(DestinationFile);
        var file = await Main.Dialogs.PickSaveFileAsync("Backup destination file", name, "zip");
        if (file is not null)
            DestinationFile = file;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var settings = ToSettings();
        if (settings.DestinationFile.Length > 0)
        {
            var full = Path.GetFullPath(settings.DestinationFile);
            if (Directory.Exists(full))
            {
                await ShowError("The destination must be a file, not a folder.");
                return;
            }
            settings.DestinationFile = full;
            DestinationFile = full;
        }

        try
        {
            Main.SaveSettings(settings);
            HasUnsavedChanges = false;
        }
        catch (Exception ex)
        {
            await ShowError($"Could not save settings: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Revert() => LoadFrom(Main.CurrentSettings);

    private Task<string?> PickFolder(string title) =>
        Main.Dialogs?.PickFolderAsync(title) ?? Task.FromResult<string?>(null);

    private Task ShowError(string message) =>
        Main.Dialogs?.ShowMessageAsync("Settings", message) ?? Task.CompletedTask;
}
