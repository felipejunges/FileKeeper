using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileKeeper.App.Services;
using FileKeeper.Core;
using FileKeeper.Core.Models;
using FileKeeper.Core.Services;

namespace FileKeeper.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsStore _store;
    private CancellationTokenSource? _cancellation;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(BackupNowCommand))]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _archiveSummary = string.Empty;

    // Designer constructor.
    public MainWindowViewModel() : this(new SettingsStore(SettingsStore.DefaultPath))
    {
    }

    public MainWindowViewModel(SettingsStore store)
    {
        _store = store;
        try
        {
            CurrentSettings = store.Load();
        }
        catch (Exception ex)
        {
            CurrentSettings = new BackupSettings();
            StatusMessage = $"Could not read settings: {ex.Message}";
        }

        Settings = new SettingsViewModel(this);
        Snapshots = new SnapshotsViewModel(this);
        Files = new FilesViewModel(this);
    }

    public IDialogService? Dialogs { get; set; }

    public bool IsIdle => !IsBusy;

    public BackupSettings CurrentSettings { get; private set; }

    public SettingsViewModel Settings { get; }

    public SnapshotsViewModel Snapshots { get; }

    public FilesViewModel Files { get; }

    public BackupEngine CreateEngine() => new(CurrentSettings);

    public void SaveSettings(BackupSettings settings)
    {
        _store.Save(settings);
        CurrentSettings = settings.Clone();
        StatusMessage = "Settings saved.";
        _ = ReloadAsync();
    }

    /// <summary>Reads all snapshot manifests and refreshes the snapshot and file views.</summary>
    [RelayCommand]
    public async Task ReloadAsync()
    {
        var engine = CreateEngine();
        try
        {
            var (snapshots, size) = await Task.Run(() => (engine.LoadSnapshots(), engine.GetArchiveSize()));
            ApplySnapshots(snapshots, size);
        }
        catch (Exception ex)
        {
            ApplySnapshots([], 0);
            StatusMessage = $"Could not read the backup file: {ex.Message}";
        }
    }

    private void ApplySnapshots(IReadOnlyList<SnapshotManifest> snapshots, long archiveSize)
    {
        Snapshots.Load(snapshots);
        Files.Load(snapshots);

        if (string.IsNullOrWhiteSpace(CurrentSettings.DestinationFile))
        {
            ArchiveSummary = "No destination configured";
            return;
        }

        var limit = CurrentSettings.MaxBackupSizeBytes is { } max ? $" of {Formatting.Bytes(max)}" : string.Empty;
        ArchiveSummary = $"{snapshots.Count} snapshot(s) · {Formatting.Bytes(archiveSize)}{limit}";
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private Task BackupNowAsync() => RunAsync("Backup", async (progress, ct) =>
    {
        var engine = CreateEngine();
        var result = await Task.Run(() => engine.CreateSnapshot(progress, ct), ct);
        await ReloadAsync();

        if (result.Snapshot is null)
        {
            StatusMessage = "No changes since the last snapshot.";
        }
        else
        {
            var diff = result.Diff!;
            StatusMessage = $"Snapshot #{result.Snapshot.Id} created: {diff.AddedCount} added, " +
                            $"{diff.ModifiedCount} modified, {diff.RemovedCount} removed " +
                            $"({Formatting.Bytes(result.StoredBytes)} stored).";
            if (result.PrunedSnapshotIds.Count > 0)
                StatusMessage += $" Pruned {Formatting.SnapshotRanges(result.PrunedSnapshotIds)} to respect the size limit.";
            Snapshots.SelectById(result.Snapshot.Id);
        }

        var warnings = new List<string>();
        if (result.ExceedsMaxSize)
            warnings.Add("The backup is still larger than the configured maximum size, " +
                         "but the latest snapshot cannot be pruned.");
        if (result.Errors.Count > 0)
            warnings.Add($"{result.Errors.Count} problem(s):\n" + string.Join("\n", result.Errors.Take(15)) +
                         (result.Errors.Count > 15 ? "\n..." : string.Empty));
        if (warnings.Count > 0 && Dialogs is not null)
            await Dialogs.ShowMessageAsync("Backup finished with warnings", string.Join("\n\n", warnings));
    });

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel()
    {
        _cancellation?.Cancel();
        StatusMessage = "Cancelling...";
    }

    /// <summary>Runs a long operation with busy state, progress reporting, cancellation and error handling.</summary>
    public async Task RunAsync(string name, Func<IProgress<string>, CancellationToken, Task> work)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        _cancellation = new CancellationTokenSource();
        var progress = new Progress<string>(message => StatusMessage = message);
        try
        {
            await work(progress, _cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"{name} cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"{name} failed: {ex.Message}";
            if (Dialogs is not null)
                await Dialogs.ShowMessageAsync($"{name} failed", ex.Message);
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            IsBusy = false;
        }
    }
}
