using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FileKeeper.App.ViewModels;

namespace FileKeeper.App.Views;

public partial class FilesView : UserControl
{
    public FilesView()
    {
        InitializeComponent();
        // Select the row under the pointer on right click, so the context menu acts on it.
        EntriesGrid.AddHandler(PointerPressedEvent, OnEntriesPointerPressed, RoutingStrategies.Tunnel);
    }

    private FilesViewModel? ViewModel => DataContext as FilesViewModel;

    private void OnEntriesDoubleTapped(object? sender, TappedEventArgs e) =>
        ViewModel?.Open(EntriesGrid.SelectedItem as BrowserEntryViewModel);

    private void OnEntriesKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm)
            return;

        switch (e.Key)
        {
            case Key.Enter when EntriesGrid.SelectedItem is BrowserEntryViewModel { IsFolder: true } entry:
                vm.Open(entry);
                e.Handled = true;
                break;
            case Key.Back when vm.GoUpCommand.CanExecute(null):
                vm.GoUpCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnEntriesPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(EntriesGrid).Properties.IsRightButtonPressed)
            return;
        if ((e.Source as Control)?.FindAncestorOfType<DataGridRow>(includeSelf: true) is { DataContext: { } item })
            EntriesGrid.SelectedItem = item;
    }

    // The list's menu only applies to folders.
    private void OnEntriesContextMenuOpening(object? sender, CancelEventArgs e) =>
        e.Cancel = EntriesGrid.SelectedItem is not BrowserEntryViewModel { IsFolder: true };

    private void OnOpenEntryClick(object? sender, RoutedEventArgs e) =>
        ViewModel?.Open(EntriesGrid.SelectedItem as BrowserEntryViewModel);

    // Used by both the tree (DataContext is the folder) and the list (acts on the selected row).
    private void OnRestoreFolderClick(object? sender, RoutedEventArgs e)
    {
        var folder = (sender as MenuItem)?.DataContext as FolderNode
                     ?? (EntriesGrid.SelectedItem as BrowserEntryViewModel)?.Folder;
        if (folder is not null && ViewModel?.RestoreFolderCommand.CanExecute(folder) == true)
            ViewModel.RestoreFolderCommand.Execute(folder);
    }
}
