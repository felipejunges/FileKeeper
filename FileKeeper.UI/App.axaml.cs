using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using FileKeeper.UI.ViewModels;
using FileKeeper.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Services;
using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Persistence;
using FileKeeper.Core.Persistence.Repositories;
using System;

namespace FileKeeper.UI;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            
            var mainWindow = new MainWindow();
            
            var vm = Services.GetRequiredService<MainWindowViewModel>();
            vm.InitializeAsync();
            
            mainWindow.DataContext = vm;
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ConfigurationWindowViewModel>();
        services.AddTransient<FilesWindowViewModel>();

        // Services
        services.AddSingleton<IConfigurationStore, ConfigurationStore>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IDatabaseService, DatabaseService>();
        
        // Repositories
        services.AddSingleton<IBackupRepository, BackupRepository>();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    public static void ShowConfigurationWindow()
    {
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = ((App)Current).Services;
            var window = new ConfigurationWindow
            {
                DataContext = services?.GetRequiredService<ConfigurationWindowViewModel>()
            };
            window.ShowDialog(desktop.MainWindow!);
        }
    }

    public static void ShowFilesWindow()
    {
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = ((App)Current).Services;
            var window = new FilesWindow
            {
                DataContext = services?.GetRequiredService<FilesWindowViewModel>()
            };
            window.ShowDialog(desktop.MainWindow!);
        }
    }

    public static void ShowBackupWindow(FileKeeper.Core.Models.Entities.Backup backup)
    {
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new BackupWindow
            {
                DataContext = new BackupWindowViewModel(backup)
            };
            window.ShowDialog(desktop.MainWindow!);
        }
    }
}