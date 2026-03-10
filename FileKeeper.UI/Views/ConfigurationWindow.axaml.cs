using Avalonia.Controls;
using FileKeeper.UI.ViewModels;
using System;

namespace FileKeeper.UI.Views;

public partial class ConfigurationWindow : Window
{
    public ConfigurationWindow(ConfigurationWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += OnRequestClose;
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
