using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UI;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Persistence;
using FileKeeper.Core.Persistence.Repositories;
using FileKeeper.Core.Services;
using FileKeeper.Core.UseCases;
using FileKeeper.UI.Infrastructure.Logging;
using FileKeeper.UI.Services;
using FileKeeper.UI.ViewModels;
using FileKeeper.UI.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileKeeper.UI;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;
    private IHost? _host;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddConfiguration(context.Configuration.GetSection("Logging"));
                logging.AddFileLogger(context.Configuration);
            })
            .ConfigureServices((_, services) => { ConfigureServices(services); })
            .Build();

        Services = _host.Services ?? throw new NullReferenceException("Unable to initialize HostServices");

        var logger = Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("========== Application Starting ==========");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            // TODO: this is needed:
            InitializeDatabaseAsync()
                 .GetAwaiter()
                 .GetResult();
            //await InitializeDatabaseAsync();

            var vm = Services.GetRequiredService<MainWindowViewModel>();
            _ = vm.InitializeAsync();

            var mainWindow = new MainWindow();
            mainWindow.DataContext = vm;
            desktop.MainWindow = mainWindow;

            desktop.ShutdownRequested += (_, _) => { logger.LogInformation("========== Application Closing =========="); };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task InitializeDatabaseAsync()
    {
        if (Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime)
            return;
        
        var services = ((App)Current).Services;
        var databaseService = services.GetRequiredService<IDatabaseService>();
        var logger = services.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Initializing database");
        
        var initResult = await databaseService.InitializeAsync(CancellationToken.None);
        if (initResult.IsError)
        {
            logger.LogError("Failed to initialize database: {Error}", initResult.FirstError.Description);
            Console.WriteLine($"Failed to initialize database: {initResult.FirstError.Description}");
            return;
        }
        
        logger.LogInformation("Database initialized successfully");
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<BackupWindowViewModel>();
        services.AddTransient<ConfigurationWindowViewModel>();
        services.AddTransient<FilesWindowViewModel>();

        // Services
        services.AddSingleton<IConfigurationStore, ConfigurationStore>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IFileSystem, LocalFileSystem>();
        services.AddSingleton<IDatabaseService, DatabaseService>();

        // UI Services
        services.AddSingleton<IFolderPickerService, FolderPickerService>();

        // Repositories
        services.AddSingleton<IBackupRepository, BackupRepository>();
        services.AddSingleton<IFileRepository, FileRepository>();

        // UseCases
        services.AddSingleton<ICreateBackupUseCase, CreateBackupUseCase>();
        services.AddSingleton<IDeleteBackupUseCase, DeleteBackupUseCase>();
        services.AddSingleton<IRecycleOldBackupUseCase, RecycleOldBackupUseCase>();
        services.AddSingleton<IRestoreBackupUseCase, RestoreBackupUseCase>();
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
        if (Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;
        
        var services = ((App)Current).Services;
        
        var vm = services.GetRequiredService<ConfigurationWindowViewModel>();
        _ = vm.InitializeAsync();

        var window = new ConfigurationWindow(vm);
        window.ShowDialog(desktop.MainWindow!);
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
        if (Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;
        
        var services = ((App)Current).Services;

        var vm = services.GetRequiredService<BackupWindowViewModel>();
        _ = vm.InitializeAsync();

        var window = new BackupWindow(vm, backup);
        window.ShowDialog(desktop.MainWindow!);
    }
}