using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Models;
using FileKeeper.Core.Services;
using FileKeeper.Core.Services.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IHashingService, HashingService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ICompressionService, CompressionService>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<IAnsiConsole>(_ => AnsiConsole.Console);
        
        services.AddSingleton<Configuration>(o =>
        {
            var configService = o.GetRequiredService<IConfigurationService>();
            return configService.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        });
    })
    .Build();

var configurationService = host.Services.GetRequiredService<IConfigurationService>();
var backupService = host.Services.GetRequiredService<BackupService>();

while (true)
{
    var title = new FigletText("FileKeeper")
        .LeftJustified()
        .Color(Color.Teal);
    
    var grid = new Grid();
    grid.AddColumn(); // expande
    grid.AddColumn(new GridColumn().RightAligned());

    grid.AddRow(title, new Markup("[grey]v1.0[/]"));
    
    AnsiConsole.Clear();
    AnsiConsole.Write(grid);
    
    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("What do you want to do?")
            .PageSize(10)
            .AddChoices(new[]
            {
                "Backup Now",
                "Restore",
                "Configuration",
                "Exit"
            }));

    switch (choice)
    {
        case "Backup Now":
            PerformBackupUI(backupService);
            break;
        case "Restore":
            //PerformRestoreUI(config);
            break;
        case "Configuration":
            ConfigureUI(configurationService);
            break;
        case "Exit":
            return;
    }
}

static void PerformBackupUI(BackupService backupService)
{
    var cancellationToken = new CancellationTokenSource().Token;
    
    AnsiConsole.Status()
        .StartAsync("Running Backup...", async ctx =>
        {
            try
            {
                await backupService.CreateBackupAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.MarkupLine("[yellow]Backup canceled by user.[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Backup failed: {ex.Message}[/]");
            }
        })
        .GetAwaiter()
        .GetResult();

    AnsiConsole.MarkupLine("Press any key to return...");
    Console.ReadKey();
}

static async Task ConfigureUI(IConfigurationService configurationService)
{
    var cancellationToken = new CancellationTokenSource().Token;
    
    var configuration = await configurationService.LoadAsync(cancellationToken);
    
    while (true)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[bold]Destination:[/] {configuration.DestinationDirectory ?? "Not Set"}");
        AnsiConsole.MarkupLine(
            $"[bold]Keep Max Backups:[/] {(configuration.KeepMaxBackups == 0 ? "Infinite" : configuration.KeepMaxBackups.ToString())}");
        AnsiConsole.MarkupLine(
            $"[bold]Use Compression:[/] {(configuration.UseCompression ? "[green]Yes[/]" : "[grey]No[/]")}");
        AnsiConsole.MarkupLine("[bold]Sources:[/]");
        foreach (var src in configuration.SourceDirectories)
        {
            AnsiConsole.MarkupLine($"  - {src}");
        }

        AnsiConsole.WriteLine();

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Configuration Menu")
                .AddChoices(new[]
                {
                    "Set Destination",
                    "Set Retention Policy",
                    "Toggle Compression",
                    "Add Source",
                    "Remove Source",
                    "Back"
                }));

        if (choice == "Back") break;

        switch (choice)
        {
            case "Set Destination":
                var dest = AnsiConsole.Ask<string>("Enter destination path:");
                if (Directory.Exists(dest))
                {
                    configuration.DestinationDirectory = dest;
                    await configurationService.SaveAsync(configuration, cancellationToken);
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Directory does not exist. Create it first.[/]");
                    Console.ReadKey();
                }

                break;
            case "Set Retention Policy":
                var count = AnsiConsole.Ask<int>("Enter max backups to keep (0 = Infinite):");
                if (count >= 0)
                {
                    configuration.KeepMaxBackups = count;
                    await configurationService.SaveAsync(configuration, cancellationToken);
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Invalid number.[/]");
                    Console.ReadKey();
                }

                break;
            case "Add Source":
                var src = AnsiConsole.Ask<string>("Enter source path to backup:");
                if (Directory.Exists(src))
                {
                    if (!configuration.SourceDirectories.Contains(src))
                    {
                        configuration.SourceDirectories.Add(src);
                        await configurationService.SaveAsync(configuration, cancellationToken);
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Directory does not exist.[/]");
                    Console.ReadKey();
                }

                break;
            case "Remove Source":
                if (configuration.SourceDirectories.Count > 0)
                {
                    var toRemove = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("Select source to remove")
                            .AddChoices(configuration.SourceDirectories));
                    configuration.SourceDirectories.Remove(toRemove);
                    await configurationService.SaveAsync(configuration, cancellationToken);
                }

                break;
            case "Toggle Compression":
                configuration.UseCompression = !configuration.UseCompression;
                await configurationService.SaveAsync(configuration, cancellationToken);
                break;
        }
    }
}