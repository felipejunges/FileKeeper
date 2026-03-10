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
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Persistence;
using FileKeeper.Core.Persistence.Repositories;
using FileKeeper.Core.UseCases;
using FileKeeper.UI.Infrastructure.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

namespace FileKeeper.UI;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }
    private IHost? _host;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging((_, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddFileLogger("FileKeeper", LogLevel.Trace); // Se você tiver o FileLogger
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices((_, services) =>
            {
                ConfigureServices(services);
            })
            .Build();
        
        Services = _host.Services;

        var logger = Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("========== Application Starting ==========");
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            
            var mainWindow = new MainWindow();
            
            var vm = Services.GetRequiredService<MainWindowViewModel>();
            vm.InitializeAsync();
            
            mainWindow.DataContext = vm;
            desktop.MainWindow = mainWindow;
            
            desktop.ShutdownRequested += (s, e) =>
            {
                logger.LogInformation("========== Application Closing ==========");
            };
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
        services.AddSingleton<IFileSystem, LocalFileSystem>();
        services.AddSingleton<IDatabaseService, DatabaseService>();
        
        // Repositories
        services.AddSingleton<IBackupRepository, BackupRepository>();
        services.AddSingleton<IFileRepository, FileRepository>();
        
        // UseCases
        services.AddSingleton<ICreateBackupUseCase, CreateBackupUseCase>();
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
            
            var vm = services?.GetRequiredService<ConfigurationWindowViewModel>();
            vm.InitializeAsync();
            
            var window = new ConfigurationWindow(vm);
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