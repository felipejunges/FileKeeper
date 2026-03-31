using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Interfaces.Wrappers;
using FileKeeper.Core.Repositories;
using FileKeeper.Core.Services;
using FileKeeper.Core.UseCases;
using FileKeeper.Core.Wrappers;
using FileKeeper.Core.Models.Options;
using FileKeeper.UI.Infrastructure.Logging;
using FileKeeper.UI.ViewModels;
using FileKeeper.UI.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

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
        var userSettingsPath = GetUserSettingsPath();
        EnsureUserSettingsFileExists(userSettingsPath);

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile(userSettingsPath, optional: true, reloadOnChange: true);
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddConfiguration(context.Configuration.GetSection("Logging"));
                logging.AddFileLogger(context.Configuration);
            })
            .ConfigureServices((context, services) => { ConfigureServices(context, services, userSettingsPath); })
            .Build();
        
        Services = _host.Services ?? throw new NullReferenceException("Unable to initialize HostServices");
        
        var logger = Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("========== Application Starting ==========");
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            
            var mainWindow = new MainWindow();
            var vm = Services.GetRequiredService<MainWindowViewModel>();
            
            mainWindow.DataContext = vm;
            
            desktop.MainWindow = mainWindow;
            
            desktop.ShutdownRequested += (_, _) => { logger.LogInformation("========== Application Closing =========="); };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(HostBuilderContext context, IServiceCollection services, string userSettingsPath)
    {
        services.Configure<UserSettingsOptions>(
            context.Configuration.GetSection(UserSettingsOptions.SectionName));

        // UI - ViewModels
        services
            .AddTransient<MainWindowViewModel>();
        
        // Services
        services
            .AddSingleton<ICompressedEncryptedFileWriter, CompressedEncryptedFileWriter>()
            .AddSingleton<IUserSettingsWriter>(_ => new UserSettingsWriter(userSettingsPath));

        // Repositories
        services
            .AddSingleton<ISnapshotRepository, SnapshotRepository>();
        
        // Wrappers
        services
            .AddSingleton<IFileWrapper, FileWrapper>();
        
        // UseCases
        services
            .AddSingleton<ICreateBackupUseCase, CreateBackupUseCase>();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private static string GetUserSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDirectory = Path.Combine(appData, "FileKeeper");
        return Path.Combine(appDirectory, "user-settings.json");
    }

    private static void EnsureUserSettingsFileExists(string userSettingsPath)
    {
        var parentDirectory = Path.GetDirectoryName(userSettingsPath);
        if (!string.IsNullOrWhiteSpace(parentDirectory) && !Directory.Exists(parentDirectory))
        {
            Directory.CreateDirectory(parentDirectory);
        }

        if (File.Exists(userSettingsPath))
        {
            return;
        }

        const string defaultSettings = """
                                       {
                                         "FileKeeper": {
                                           "SourceDirectories": [
                                                "/home/felipe/Documentos/Obsidian"
                                           ],
                                           "StorageDirectory": "/home/felipe/.local/share/FileKeeper/storage"
                                         }
                                       }
                                       """;

        File.WriteAllText(userSettingsPath, defaultSettings);
    }
}