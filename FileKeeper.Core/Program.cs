using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Interfaces.Abstraction;
using FileKeeper.Core.Interfaces.Abstraction.Info;
using FileKeeper.Core.Models;
using FileKeeper.Core.Services;
using FileKeeper.Core.Services.Abstraction;
using FileKeeper.Core.Services.Abstraction.Info;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ICompressionService, CompressionZipService>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<RestoreService>();
        services.AddSingleton<IRecycleService, RecycleService>();
        services.AddSingleton<IIndexService, IndexService>();
        services.AddSingleton<IIntegrityService, IntegrityService>();
        services.AddSingleton<IFileInfoBuilder, FileInfoBuilder>();
        services.AddSingleton<IFileSourceService, FileSourceService>();
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
var restoreService = host.Services.GetRequiredService<RestoreService>();
var integrityService = host.Services.GetRequiredService<IIntegrityService>();

if (args.Contains("-a"))
{
    // run backup immediately and exit
    var cts = new CancellationTokenSource();
    backupService.CreateBackupAsync(cts.Token).GetAwaiter().GetResult();
    return;
}

if (args.Contains("-v"))
{
    // run integrity check and exit
    var cts = new CancellationTokenSource();
    integrityService.VerifyIntegrityAsync(cts.Token).GetAwaiter().GetResult();
    return;
}

while (true)
{
    var title = new FigletText("FileKeeper")
        .LeftJustified()
        .Color(Color.Teal);

    var grid = new Grid();
    grid.AddColumn(); // expande
    grid.AddColumn(new GridColumn().RightAligned());

    grid.AddRow(title, new Markup($"[grey]{FileKeeper.Core.Utils.AppInfo.GetAppVersion()}[/]"));

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
                "Check Integrity",
                "Configuration",
                "Exit"
            }));

    switch (choice)
    {
        case "Backup Now":
            PerformBackupUI(backupService);
            break;
        case "Restore":
            PerformRestoreUI(restoreService).GetAwaiter().GetResult();
            break;
        case "Check Integrity":
            PerformIntegrityCheckUI(integrityService);
            break;
        case "Configuration":
            ConfigureUI(configurationService).GetAwaiter().GetResult();
            break;
        case "Exit":
            return;
    }
}

static void PerformBackupUI(BackupService backupService)
{
    var cancellationToken = new CancellationTokenSource().Token;

    AnsiConsole.Status()
        .StartAsync("Running Backup...", async _ =>
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

static async Task PerformRestoreUI(RestoreService restoreService)
{
    var cancellationToken = new CancellationTokenSource().Token;

    var backups = await restoreService.GetListOfBackupsAsync(cancellationToken);
    backups.Add(new ValueTuple<string, DateTime>("Voltar", DateTime.MinValue));

    var selectedBackup = AnsiConsole.Prompt(
        new SelectionPrompt<(string Id, DateTime Date)>()
            .Title("Select a backup to restore point:")
            .PageSize(10)
            .UseConverter(x => x.Id == "Voltar" ? "Voltar" : $"{x.Date:yyyy-MM-dd HH:mm} ({x.Id})")
            .AddChoices(backups));

    if (selectedBackup.Item1 == "Voltar")
    {
        return;
    }

    var destination = AnsiConsole.Prompt(
        new TextPrompt<string>("[bold yellow]Dear beloved user,[/] what is the destination directory for your backups?")
            .DefaultValue(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
            .Validate(path => string.IsNullOrWhiteSpace(path)
                ? ValidationResult.Error("[red]Path can't be empty[/]")
                : ValidationResult.Success()));

    AnsiConsole.Status()
        .StartAsync("Running Backup...", async _ =>
        {
            try
            {
                await restoreService.RestoreBackupAsync(selectedBackup.Item1, destination, cancellationToken);
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
        AnsiConsole.MarkupLine($"[bold]Compression Type:[/] {configuration.CompressionType}");
        AnsiConsole.MarkupLine("[bold]Sources:[/]");
        foreach (var src in configuration.SourceDirectories)
        {
            AnsiConsole.MarkupLine($"  - {src}");
        }
        AnsiConsole.MarkupLine("[bold]Exclude Patterns:[/]");
        if (configuration.ExcludePatterns.Count > 0)
        {
            foreach (var pattern in configuration.ExcludePatterns)
            {
                AnsiConsole.MarkupLine($"  - {pattern}");
            }
        }
        else
        {
            AnsiConsole.MarkupLine("  - [grey]None[/]");
        }

        AnsiConsole.WriteLine();

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Configuration Menu")
                .AddChoices(new[]
                {
                    "Set Destination",
                    "Set Retention Policy",
                    "Set Compression Type",
                    "Add Source",
                    "Remove Source",
                    "Add Exclude Pattern",
                    "Remove Exclude Pattern",
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
            case "Set Compression Type":
                var typeChoice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select compression type")
                        .AddChoices(Enum.GetNames(typeof(CompressionTypeConfiguration))));

                configuration.CompressionType = Enum.Parse<CompressionTypeConfiguration>(typeChoice);
                await configurationService.SaveAsync(configuration, cancellationToken);
                break;
            case "Add Exclude Pattern":
                var pattern = AnsiConsole.Ask<string>("Enter pattern to exclude (e.g. 'node_modules', '.tmp'):");
                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    if (!configuration.ExcludePatterns.Contains(pattern))
                    {
                        configuration.ExcludePatterns.Add(pattern);
                        await configurationService.SaveAsync(configuration, cancellationToken);
                    }
                }
                break;
            case "Remove Exclude Pattern":
                if (configuration.ExcludePatterns.Count > 0)
                {
                    var toRemove = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("Select pattern to remove")
                            .AddChoices(configuration.ExcludePatterns));
                    configuration.ExcludePatterns.Remove(toRemove);
                    await configurationService.SaveAsync(configuration, cancellationToken);
                }
                break;
        }
    }
}

static void PerformIntegrityCheckUI(IIntegrityService integrityService)
{
    var cancellationToken = new CancellationTokenSource().Token;

    AnsiConsole.Status()
        .StartAsync("Verifying Integrity...", async _ =>
        {
            try
            {
                await integrityService.VerifyIntegrityAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.MarkupLine("[yellow]Verification canceled by user.[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Verification failed: {ex.Message}[/]");
            }
        })
        .GetAwaiter()
        .GetResult();

    AnsiConsole.MarkupLine("Press any key to return...");
    Console.ReadKey();
}