using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Persistence;
using FileKeeper.Core.Persistence.Repositories;
using FileKeeper.Core.Services;
using FileKeeper.Core.UseCases;
using Gtk;
using FileKeeper.Gtk.Dialogs;
using FileKeeper.Gtk.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

try
{
    AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
    {
        Console.WriteLine($"[AppDomain.UnhandledException] {eventArgs.ExceptionObject}");
    };

    TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
    {
        Console.WriteLine($"[TaskScheduler.UnobservedTaskException] {eventArgs.Exception}");
        eventArgs.SetObserved();
    };

    var host = Host.CreateDefaultBuilder(args)
        .ConfigureLogging((_, logging) =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddFileLogger("FileKeeper", LogLevel.Trace);
            logging.SetMinimumLevel(LogLevel.Information);
        })
        .ConfigureServices((_, services) =>
        {
            services.AddSingleton<ICreateBackupUseCase, CreateBackupUseCase>();
            services.AddSingleton<IRestoreBackupUseCase, RestoreBackupUseCase>();
            services.AddSingleton<IDeleteBackupUseCase, DeleteBackupUseCase>();
            services.AddSingleton<IRecycleOldBackupUseCase, RecycleOldBackupUseCase>();

            services.AddSingleton<IFileSystem, LocalFileSystem>();
            services.AddSingleton<IBackupRepository, BackupRepository>();
            services.AddSingleton<IFileRepository, FileRepository>();

            services.AddSingleton<IConfigurationStore, ConfigurationStore>();
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddSingleton<IDatabaseService, DatabaseService>();
        })
        .Build();

    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    
    logger.LogInformation("========== Application Starting ==========");
    logger.LogInformation("Current OS: {OS}", System.Runtime.InteropServices.RuntimeInformation.OSDescription);
    logger.LogInformation("Current Time: {Time}", DateTime.UtcNow);

    var configurationService = host.Services.GetRequiredService<IConfigurationService>();
    var createBackupUseCase = host.Services.GetRequiredService<ICreateBackupUseCase>();
    var restoreBackupUseCase = host.Services.GetRequiredService<IRestoreBackupUseCase>();
    var backupRepository = host.Services.GetRequiredService<IBackupRepository>();
    var databaseService = host.Services.GetRequiredService<IDatabaseService>();
    var recycleOldBackupUseCase = host.Services.GetRequiredService<IRecycleOldBackupUseCase>();

    var initResult = await databaseService.InitializeAsync(CancellationToken.None);
    if (initResult.IsError)
    {
        logger.LogError("Failed to initialize database: {Error}", initResult.FirstError.Description);
        Console.WriteLine($"Erro ao inicializar banco: {initResult.FirstError.Description}");
        return;
    }

    logger.LogInformation("Database initialized successfully");

    Application.Init();
    //SynchronizationContext.SetSynchronizationContext(new GLib.GLibSynchronizationContext());

    var win = new MainWindow(
        configurationService,
        createBackupUseCase,
        restoreBackupUseCase,
        backupRepository,
        recycleOldBackupUseCase);

    logger.LogInformation("Main window created and showing");
    win.ShowAll();

    Application.Run();

    logger.LogInformation("========== Application Closing ==========");
}
catch (Exception ex)
{
    Console.WriteLine($"Fatal error: {ex}");
}

