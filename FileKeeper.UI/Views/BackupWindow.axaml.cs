using Avalonia.Controls;
using FileKeeper.Core.Models.Entities;
using FileKeeper.UI.ViewModels;
using System;

namespace FileKeeper.UI.Views;

public partial class BackupWindow : Window
{
    public BackupWindow(BackupWindowViewModel viewModel, Backup backup)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += OnRequestClose;

        viewModel.SetBackup(backup);
        viewModel.SetWindow(this);
    }

    private void OnRequestClose() => Close();

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is ConfigurationWindowViewModel vm)
        {
            vm.RequestClose -= OnRequestClose;
        }

        base.OnClosed(e);
    }
}