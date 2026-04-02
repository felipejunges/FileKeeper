using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileKeeper.UI.Models;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace FileKeeper.UI.ViewModels;

public partial class SnapshotViewModel : ViewModelBase
{
    [ObservableProperty] private string _snapshotName = "Select a snapshot to inspect its files";

    [ObservableProperty] private bool _onlyNewFiles = true;

    public ObservableCollection<FileEntryDto> FileEntries { get; } = [];

    public string ToggleOnlyNewFilesButtonText => OnlyNewFiles ? "Show all files" : "Show only new files";

    private IReadOnlyCollection<FileEntryDto> _allFiles = [];

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
        OnPropertyChanged(nameof(ToggleOnlyNewFilesButtonText));
        ApplyFilter();
    }

    [RelayCommand]
    private void ToggleOnlyNewFiles()
    {
        OnlyNewFiles = !OnlyNewFiles;
    }

    private void ApplyFilter()
    {
        FileEntries.Clear();

        foreach (var file in _allFiles)
        {
            if (OnlyNewFiles && file.FoundInSnapshot != SnapshotName)
                continue;

            FileEntries.Add(file);
        }
    }
}