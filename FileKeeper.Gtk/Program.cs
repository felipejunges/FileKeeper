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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        services.AddSingleton<ICreateBackupUseCase, CreateBackupUseCase>();

        services.AddSingleton<IFileSystem, LocalFileSystem>();
        services.AddSingleton<IBackupRepository, BackupRepository>();
        services.AddSingleton<IFileRepository, FileRepository>();

        services.AddSingleton<IConfigurationStore, ConfigurationStore>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IDatabaseService, DatabaseService>();
    })
    .Build();

var configurationService = host.Services.GetRequiredService<IConfigurationService>();
var createBackupUseCase = host.Services.GetRequiredService<ICreateBackupUseCase>();
var databaseService = host.Services.GetRequiredService<IDatabaseService>();

var initResult = await databaseService.InitializeAsync(CancellationToken.None);
if (initResult.IsError)
{
    Console.WriteLine($"Erro ao inicializar banco: {initResult.FirstError.Description}");
    return;
}

Application.Init();

var win = new MainWindow(
    configurationService,
    createBackupUseCase);

win.ShowAll();

Application.Run();
