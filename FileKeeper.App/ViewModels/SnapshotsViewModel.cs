using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileKeeper.Core;
using FileKeeper.Core.Models;

namespace FileKeeper.App.ViewModels;

public partial class SnapshotsViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreSelectedCommand))]
    private SnapshotItemViewModel? _selected;

    [ObservableProperty] private bool _showAdded = true;
    [ObservableProperty] private bool _showModified = true;
    [ObservableProperty] private bool _showRemoved = true;

    private PathDisplay _display = new([]);

    public SnapshotsViewModel(MainWindowViewModel main) => Main = main;

    public MainWindowViewModel Main { get; }

    /// <summary>Newest first.</summary>
    public ObservableCollection<SnapshotItemViewModel> Items { get; } = [];

    public ObservableCollection<ChangeRowViewModel> Changes { get; } = [];

    public bool HasSnapshots => Items.Count > 0;

    partial void OnSelectedChanged(SnapshotItemViewModel? value) => RefreshChanges();
    partial void OnShowAddedChanged(bool value) => RefreshChanges();
    partial void OnShowModifiedChanged(bool value) => RefreshChanges();
    partial void OnShowRemovedChanged(bool value) => RefreshChanges();

    public void Load(IReadOnlyList<SnapshotManifest> snapshots)
    {
        var selectedId = Selected?.Id;
        _display = new PathDisplay(snapshots.SelectMany(s => s.SourceFolders));
        Items.Clear();
        SnapshotManifest? previous = null;
        var items = new List<SnapshotItemViewModel>();
        foreach (var snapshot in snapshots)
        {
            items.Add(new SnapshotItemViewModel(snapshot, SnapshotDiff.Compute(previous, snapshot)));
            previous = snapshot;
        }

        items.Reverse();
        foreach (var item in items)
            Items.Add(item);
        if (Items.Count > 0)
            Items[0].IsLatest = true;

        Selected = Items.FirstOrDefault(i => i.Id == selectedId) ?? Items.FirstOrDefault();
        OnPropertyChanged(nameof(HasSnapshots));
        DeleteLatestCommand.NotifyCanExecuteChanged();
    }

    public void SelectById(int id) => Selected = Items.FirstOrDefault(i => i.Id == id) ?? Selected;

    private void RefreshChanges()
    {
        Changes.Clear();
        if (Selected is null)
            return;

        foreach (var change in Selected.Diff.Changes)
        {
            var visible = change.Kind switch
            {
                ChangeKind.Added => ShowAdded,
                ChangeKind.Modified => ShowModified,
                _ => ShowRemoved,
            };
            if (visible)
                Changes.Add(new ChangeRowViewModel(change, _display.Shorten(change.Path)));
        }
    }

    [RelayCommand(CanExecute = nameof(HasSnapshots))]
    private async Task DeleteLatestAsync()
    {
        var latest = Items.FirstOrDefault();
        if (latest is null || Main.Dialogs is null)
            return;

        var confirmed = await Main.Dialogs.ConfirmAsync("Delete snapshot",
            $"Delete the most recent snapshot #{latest.Id} ({latest.CreatedText})?\n\n" +
            $"{latest.StoredCount} file(s) stored only in this snapshot will be removed from the backup. " +
            "This cannot be undone.", "Delete");
        if (!confirmed)
            return;

        await Main.RunAsync("Delete snapshot", async (_, _) =>
        {
            var engine = Main.CreateEngine();
            var deleted = await Task.Run(engine.DeleteLatestSnapshot);
            await Main.ReloadAsync();
            Main.StatusMessage = $"Snapshot #{deleted.Id} deleted.";
        });
    }

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task RestoreSelectedAsync()
    {
        var snapshot = Selected;
        if (snapshot is null || Main.Dialogs is null)
            return;

        var folder = await Main.Dialogs.PickFolderAsync($"Restore snapshot #{snapshot.Id} into...");
        if (folder is null)
            return;

        await Main.RunAsync("Restore", async (progress, ct) =>
        {
            var engine = Main.CreateEngine();
            var count = await Task.Run(() => engine.RestoreSnapshot(snapshot.Id, folder, progress, ct), ct);
            Main.StatusMessage = $"Restored {count} file(s) of snapshot #{snapshot.Id} into {folder}.";
        });
    }

    private bool CanRestore() => Selected is not null;
}

public sealed class SnapshotItemViewModel(SnapshotManifest manifest, SnapshotDiff diff) : ViewModelBase
{
    private bool _isLatest;

    public SnapshotManifest Manifest { get; } = manifest;
    public SnapshotDiff Diff { get; } = diff;

    public int Id => Manifest.Id;
    public string Title => $"Snapshot #{Id}";
    public string CreatedText => Manifest.CreatedUtc.ToLocalTime().ToString("g");
    public int FileCount => Manifest.Files.Count;
    public string TotalSizeText => Formatting.Bytes(Manifest.TotalSize);
    public int StoredCount => Manifest.StoredFiles.Count();
    public string StoredSizeText => Formatting.Bytes(Manifest.StoredFiles.Sum(f => f.Size));
    public int AddedCount => Diff.AddedCount;
    public int ModifiedCount => Diff.ModifiedCount;
    public int RemovedCount => Diff.RemovedCount;
    public bool HasMerged => Manifest.MergedSnapshotIds.Count > 0;
    public string MergedText => HasMerged
        ? $"Includes pruned snapshot(s) {Formatting.SnapshotRanges(Manifest.MergedSnapshotIds)}"
        : string.Empty;

    public bool IsLatest
    {
        get => _isLatest;
        set => SetProperty(ref _isLatest, value);
    }
}

public sealed class ChangeRowViewModel(FileChange change, string displayPath)
{
    private static readonly IBrush AddedBrush = new SolidColorBrush(Color.Parse("#2E9E4F"));
    private static readonly IBrush ModifiedBrush = new SolidColorBrush(Color.Parse("#C98A12"));
    private static readonly IBrush RemovedBrush = new SolidColorBrush(Color.Parse("#D64545"));

    public string KindText => change.Kind.ToString();

    public string Symbol => change.Kind switch
    {
        ChangeKind.Added => "+",
        ChangeKind.Modified => "~",
        _ => "−",
    };

    public IBrush KindBrush => change.Kind switch
    {
        ChangeKind.Added => AddedBrush,
        ChangeKind.Modified => ModifiedBrush,
        _ => RemovedBrush,
    };

    public string Path => change.Path;

    public string DisplayPath => displayPath;

    public string SizeText => change.Kind == ChangeKind.Modified
        ? $"{Formatting.Bytes(change.Before!.Size)} → {Formatting.Bytes(change.After!.Size)}"
        : Formatting.Bytes(change.Size);

    public string ModifiedText => ((change.After ?? change.Before)!.LastWriteTimeUtc).ToLocalTime().ToString("g");
}
