using Avalonia.Controls;
using FileKeeper.UI.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace FileKeeper.UI.Views;

public partial class ConfigurationWindow : Window
{
    public ConfigurationWindow(ConfigurationWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += OnRequestClose;
        viewModel.RequestFolderPicker += OnRequestFolderPicker;
    }
    
    private void OnRequestClose() => Close();

    private async Task<string?> OnRequestFolderPicker()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Folder to Monitor",
            AllowMultiple = false
        });

        return folders.FirstOrDefault()?.Path.LocalPath;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is ConfigurationWindowViewModel vm)
        {
            vm.RequestClose -= OnRequestClose;
            vm.RequestFolderPicker -= OnRequestFolderPicker;
        }

        base.OnClosed(e);
    }
}
