using CommunityToolkit.Mvvm.ComponentModel;
using FileKeeper.UI.Models;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace FileKeeper.UI.ViewModels;

public partial class SnapshotViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _snapshotName = "Select a snapshot to inspect its files";

    public ObservableCollection<FileEntryDto> FileEntries { get; } = [];

    public SnapshotViewModel()
    {
    }

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
        FileEntries.Clear();

        foreach (var file in files)
        {
            FileEntries.Add(file);
        }
    }
}

