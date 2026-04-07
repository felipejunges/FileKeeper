using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileKeeper.UI.Models;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileKeeper.UI.ViewModels;

public partial class SnapshotViewModel : ViewModelBase
{
    [ObservableProperty] private string _snapshotName = "Select a snapshot to inspect its files";

    [ObservableProperty] private bool _onlyNewFiles = true;

    [ObservableProperty] private SnapshotFolderDto? _selectedFolder;

    public ObservableCollection<SnapshotFolderDto> Folders { get; } = [];

    public ObservableCollection<FileEntryDto> FileEntries { get; } = [];

    public string ToggleOnlyNewFilesButtonText => OnlyNewFiles ? "Show all files" : "Show only new files";

    public string CurrentFolderDisplayName => SelectedFolder?.DisplayPath ?? "Select a folder to inspect its files";

    private IReadOnlyCollection<FileEntryDto> _allFiles = [];
    private IReadOnlyList<FileEntryDto> _filteredFiles = [];

    public void SetSnapshot(SnapshotDto? snapshot)
    {
        if (snapshot is null)
        {
            SetSnapshot("No snapshot selected", []);
            return;
        }

        SetSnapshot(snapshot.Name, snapshot.Files);
    }

    private void SetSnapshot(string snapshotName, IReadOnlyCollection<FileEntryDto> files)
    {
        SnapshotName = snapshotName;
        _allFiles = files;
        ApplyFilter();
    }

    partial void OnOnlyNewFilesChanged(bool value)
    {
        _ = value;
        OnPropertyChanged(nameof(ToggleOnlyNewFilesButtonText));
        ApplyFilter();
    }

    partial void OnSelectedFolderChanged(SnapshotFolderDto? value)
    {
        _ = value;
        OnPropertyChanged(nameof(CurrentFolderDisplayName));
        RefreshSelectedFolderFiles();
    }

    [RelayCommand]
    private void ToggleOnlyNewFiles()
    {
        OnlyNewFiles = !OnlyNewFiles;
    }

    private void ApplyFilter()
    {
        _filteredFiles = _allFiles
            .Where(file => !OnlyNewFiles || file.FoundInSnapshot == SnapshotName)
            .ToList();

        RebuildFolders();
    }

    private void RebuildFolders()
    {
        var previousKey = SelectedFolder?.Key;
        var folders = BuildFolders(_filteredFiles);

        Folders.Clear();
        foreach (var folder in folders)
            Folders.Add(folder);

        SelectedFolder = folders.FirstOrDefault(folder => folder.Key == previousKey)
            ?? folders.FirstOrDefault();

        if (SelectedFolder is null)
            FileEntries.Clear();
    }

    private void RefreshSelectedFolderFiles()
    {
        FileEntries.Clear();

        if (SelectedFolder is null)
            return;

        foreach (var file in _filteredFiles
                     .Where(file => IsInsideSelectedFolder(file, SelectedFolder))
                     .OrderBy(file => file.FileName, StringComparer.OrdinalIgnoreCase))
        {
            FileEntries.Add(file);
        }
    }

    private static IReadOnlyList<SnapshotFolderDto> BuildFolders(IEnumerable<FileEntryDto> files)
    {
        var directFileCounts = files
            .GroupBy(file => (file.SourceDirectory, RelativeFolder: GetRelativeFolder(file.RelativePath)))
            .ToDictionary(group => CreateFolderKey(group.Key.SourceDirectory, group.Key.RelativeFolder), group => group.Count());

        var folders = new Dictionary<string, SnapshotFolderDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceDirectory in files
                     .Select(file => file.SourceDirectory)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            AddFolder(folders, directFileCounts, sourceDirectory, string.Empty, 0);
        }

        foreach (var file in files)
        {
            var relativeFolder = GetRelativeFolder(file.RelativePath);
            if (string.IsNullOrWhiteSpace(relativeFolder))
                continue;

            var segments = relativeFolder.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < segments.Length; index++)
            {
                var folderPath = string.Join('/', segments.Take(index + 1));
                AddFolder(folders, directFileCounts, file.SourceDirectory, folderPath, index + 1);
            }
        }

        return folders.Values
            .OrderBy(folder => folder.SourceDirectory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(folder => folder.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddFolder(
        IDictionary<string, SnapshotFolderDto> folders,
        IReadOnlyDictionary<string, int> directFileCounts,
        string sourceDirectory,
        string relativePath,
        int depth)
    {
        var key = CreateFolderKey(sourceDirectory, relativePath);
        if (folders.ContainsKey(key))
            return;

        folders[key] = new SnapshotFolderDto(
            Key: key,
            SourceDirectory: sourceDirectory,
            RelativePath: relativePath,
            Depth: depth,
            FileCount: directFileCounts.GetValueOrDefault(key));
    }

    private static bool IsInsideSelectedFolder(FileEntryDto file, SnapshotFolderDto selectedFolder) =>
        string.Equals(file.SourceDirectory, selectedFolder.SourceDirectory, StringComparison.OrdinalIgnoreCase)
        && string.Equals(GetRelativeFolder(file.RelativePath), selectedFolder.RelativePath, StringComparison.OrdinalIgnoreCase);

    private static string GetRelativeFolder(string relativePath)
    {
        var normalizedPath = NormalizePath(relativePath);
        var lastSeparatorIndex = normalizedPath.LastIndexOf('/');

        return lastSeparatorIndex < 0
            ? string.Empty
            : normalizedPath[..lastSeparatorIndex];
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').Trim('/');

    private static string CreateFolderKey(string sourceDirectory, string relativePath) =>
        $"{sourceDirectory}|{relativePath}";
}