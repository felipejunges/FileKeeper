using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileKeeper.Core;
using FileKeeper.Core.Models;

namespace FileKeeper.App.ViewModels;

public partial class FilesViewModel : ViewModelBase
{
    private const int MaxRows = 5000;
    private List<FileRowViewModel> _all = [];
    private bool _rebuilding;

    [ObservableProperty]
    private string _filter = string.Empty;

    [ObservableProperty]
    private bool _showDeleted = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreFolderCommand))]
    private FolderNode? _currentFolder;

    [ObservableProperty]
    private BrowserEntryViewModel? _selectedEntry;

    [ObservableProperty]
    private FileRowViewModel? _selected;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    public FilesViewModel(MainWindowViewModel main) => Main = main;

    public MainWindowViewModel Main { get; }

    /// <summary>Single root node, so the tree shows every source folder under it.</summary>
    public ObservableCollection<FolderNode> RootFolders { get; } = [];

    /// <summary>Content of the current folder (or search results).</summary>
    public ObservableCollection<BrowserEntryViewModel> Entries { get; } = [];

    /// <summary>Folders from the root to the current folder.</summary>
    public ObservableCollection<FolderNode> Breadcrumbs { get; } = [];

    partial void OnFilterChanged(string value) => RefreshEntries();
    partial void OnShowDeletedChanged(bool value) => RebuildTree();
    partial void OnSelectedEntryChanged(BrowserEntryViewModel? value) => Selected = value?.File;

    partial void OnCurrentFolderChanged(FolderNode? value)
    {
        if (value is null)
        {
            // The tree clears its selection while being rebuilt; keep browsing the same place.
            if (!_rebuilding && RootFolders.Count > 0)
                CurrentFolder = RootFolders[0];
            return;
        }

        foreach (var node in value.SelfAndAncestors())
            node.IsExpanded = node != value || node.IsExpanded;

        Breadcrumbs.Clear();
        foreach (var node in value.SelfAndAncestors().Reverse())
            Breadcrumbs.Add(node);

        if (!_rebuilding)
            RefreshEntries();
    }

    public void Load(IReadOnlyList<SnapshotManifest> snapshots)
    {
        // Full paths ("home/me/Dropbox/..."), so the tree mirrors where the files live on disk.
        _all = FileHistory.Build(snapshots).Select(h => new FileRowViewModel(h, h.Path)).ToList();
        RebuildTree();
    }

    private void RebuildTree()
    {
        var currentPath = CurrentFolder?.DisplayPath ?? string.Empty;
        var selectedPath = Selected?.Path;

        _rebuilding = true;
        var root = FolderNode.Build("Backup", _all.Where(r => ShowDeleted || r.History.ExistsInLatest));
        RootFolders.Clear();
        RootFolders.Add(root);
        CurrentFolder = root.Find(currentPath);
        _rebuilding = false;

        RefreshEntries();
        SelectedEntry = Entries.FirstOrDefault(e => e.File?.Path == selectedPath);

        var versions = _all.Sum(r => r.VersionCount);
        var deleted = _all.Count(r => !r.History.ExistsInLatest);
        Summary = $"{_all.Count} file(s), {versions} stored version(s), {deleted} deleted";
    }

    private void RefreshEntries()
    {
        Entries.Clear();
        var folder = CurrentFolder;
        if (folder is null)
            return;

        var terms = Filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        IsSearching = terms.Length > 0;
        if (!IsSearching)
        {
            foreach (var sub in folder.Folders)
                Entries.Add(BrowserEntryViewModel.ForFolder(sub));
            foreach (var file in folder.Files)
                Entries.Add(BrowserEntryViewModel.ForFile(file, file.FileName));
            return;
        }

        // Recursive search below the current folder; names are shown relative to it.
        var prefix = folder.DisplayPath.Length == 0 ? string.Empty : folder.DisplayPath + "/";
        var matches = folder.AllFiles()
            .Select(f => (File: f, Relative: f.DisplayPath[prefix.Length..]))
            .Where(m => terms.All(t => m.Relative.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(m => m.Relative, StringComparer.OrdinalIgnoreCase)
            .Take(MaxRows);
        foreach (var (file, relative) in matches)
            Entries.Add(BrowserEntryViewModel.ForFile(file, relative));
    }

    /// <summary>Opens a folder entry (double click / Enter).</summary>
    [RelayCommand]
    public void Open(BrowserEntryViewModel? entry)
    {
        if (entry?.Folder is null)
            return;
        Filter = string.Empty;
        CurrentFolder = entry.Folder;
    }

    [RelayCommand]
    private void NavigateTo(FolderNode? folder)
    {
        if (folder is null)
            return;
        Filter = string.Empty;
        CurrentFolder = folder;
    }

    [RelayCommand(CanExecute = nameof(CanGoUp))]
    private void GoUp()
    {
        var current = CurrentFolder;
        NavigateTo(current?.Parent);
        // Keep the folder we came from selected, like a file manager.
        SelectedEntry = Entries.FirstOrDefault(e => e.Folder == current);
    }

    private bool CanGoUp() => CurrentFolder?.Parent is not null;

    /// <summary>
    /// Restores the latest stored version of every file below <paramref name="folder"/> (the current folder
    /// when null) into a chosen folder. Deleted files are included only while they are shown.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRestoreFolder))]
    public async Task RestoreFolderAsync(FolderNode? folder)
    {
        folder ??= CurrentFolder;
        if (folder is null || Main.Dialogs is null)
            return;

        var files = folder.AllFiles().ToList();
        if (files.Count == 0)
            return;

        var target = await Main.Dialogs.PickFolderAsync($"Restore folder \"{folder.Name}\" into...");
        if (target is null)
            return;

        // The folder itself is recreated inside the target, except for the root which holds the source folders.
        var prefix = folder.DisplayPath.Length == 0 ? string.Empty : folder.DisplayPath + "/";
        var baseName = folder.Parent is null ? string.Empty : folder.Name + "/";
        var items = files
            .Select(f => new RestoreItem(f.Path, f.History.LatestVersion.StoredIn,
                baseName + f.DisplayPath[prefix.Length..]))
            .ToList();

        var existing = items.Count(i => File.Exists(Path.Combine(target, i.TargetPath)));
        if (existing > 0 && !await Main.Dialogs.ConfirmAsync("Restore folder",
                $"{existing} of {items.Count} file(s) already exist in {target} and will be overwritten.",
                "Overwrite"))
            return;

        await Main.RunAsync("Restore", async (progress, ct) =>
        {
            var engine = Main.CreateEngine();
            var count = await Task.Run(() => engine.RestoreFiles(items, target, progress, ct), ct);
            Main.StatusMessage = $"Restored {count} file(s) of \"{folder.Name}\" into {target}.";
        });
    }

    private bool CanRestoreFolder(FolderNode? folder) => (folder ?? CurrentFolder)?.FileCount > 0;

    [RelayCommand]
    private async Task ExtractVersionAsync(VersionRowViewModel? version)
    {
        if (version is null || Main.Dialogs is null)
            return;

        var target = await Main.Dialogs.PickSaveFileAsync(
            $"Save {version.FileName} (version stored in snapshot #{version.StoredIn})", version.FileName);
        if (target is null)
            return;

        await Main.RunAsync("Extract", async (_, _) =>
        {
            var engine = Main.CreateEngine();
            await Task.Run(() => engine.ExtractFile(version.Path, version.StoredIn, target));
            Main.StatusMessage = $"Saved {version.FileName} to {target}.";
        });
    }
}

public sealed class FileRowViewModel
{
    public FileRowViewModel(FileHistory history, string displayPath)
    {
        History = history;
        DisplayPath = displayPath;
        Versions = history.Versions
            .Select((v, i) => new VersionRowViewModel(history.Path, i + 1, v))
            .Reverse()
            .ToList();
    }

    public FileHistory History { get; }
    public string Path => History.Path;
    public string DisplayPath { get; }
    public string FileName => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(DisplayPath)?.Replace('\\', '/') ?? string.Empty;
    public int VersionCount => History.Versions.Count;
    public string LatestSizeText => Formatting.Bytes(History.LatestVersion.Size);
    public string StatusText => History.ExistsInLatest ? "Present" : "Deleted";
    public string SnapshotsText => Formatting.SnapshotRanges(History.SnapshotIds);

    /// <summary>Newest version first.</summary>
    public IReadOnlyList<VersionRowViewModel> Versions { get; }
}

public sealed class VersionRowViewModel(string path, int number, FileVersion version)
{
    public string Path { get; } = path;
    public string FileName => System.IO.Path.GetFileName(Path);
    public string NumberText => $"v{number}";
    public int StoredIn => version.StoredIn;
    public string StoredInText => $"#{version.StoredIn}";
    public string SizeText => Formatting.Bytes(version.Size);
    public string ModifiedText => version.LastWriteTimeUtc.ToLocalTime().ToString("g");
    public string HashText => version.Sha256[..12];
    public string SnapshotsText => Formatting.SnapshotRanges(version.SnapshotIds);
}
