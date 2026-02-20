███████╗██╗ ███████╗███████╗███████╗███████╗███████╗
██╔════╝██║██╔════╝██╔════╝██╔════╝██╔════╝██╔════╝
█████╗  ██║█████╗  █████╗  ███████╗███████╗███████╗
██╔══╝  ██║██╔══╝  ██╔══╝  ╚════██║╚════██║╚════██║
██║     ██║███████╗███████╗███████║███████║███████║
╚═╝     ╚═╝╚══════╝╚══════╝╚══════╝╚══════╝╚══════╝

# FileKeeper

FileKeeper is a lightweight console application that helps you create differential backups of your files and optionally recycle (remove) old backups to save space.

Key points
- Differential backups: only changed/added files are included after the initial full backup.
- Recycle support: optionally remove old backups according to retention policy.
- Current archive format: ZIP. TAR support will be added soon.
- Interactive TUI powered by Spectre.Console for navigation and selection.

Features
- Create differential backups of configured source directories to a destination directory.
- Restore from a selected backup to a destination path.
- Verify integrity of backups.
- Configure sources, destination, retention, compression via an interactive UI.
- Command-line switches for quick operations (see Usage).

Requirements
- .NET 10 (or the target framework used in the solution)
- A terminal that supports ANSI colors for best experience

Quick start

1. Restore dependencies and build the solution from the repository root:

```bash
dotnet build
```

2. Run the app interactively:

```bash
dotnet run --project FileKeeper.Core
```

3. Run backup immediately (non-interactive):

```bash
dotnet run --project FileKeeper.Core -- -a
```

4. Run integrity check immediately:

```bash
dotnet run --project FileKeeper.Core -- -v
```

CLI options
- `-a` : run backup immediately and exit
- `-v` : run integrity check and exit

Interactive UI
The app uses Spectre.Console for a friendly TUI. It displays a Figlet-style title (the ASCII art above) and interactive selection prompts for Backup, Restore, Integrity Check and Configuration. The library also supports building custom navigators — the project will include a directory selector soon that allows keyboard navigation to choose directories.

Current limitations
- Backups are stored as ZIP archives for now. TAR (and other archive options) are planned and will be added in a near-future release.

Contributing
Contributions, issues and suggestions are welcome. Please open issues or pull requests against the repository.

TODO
- Implement TAR compression option alongside ZIP
- Implement and polish a Spectre.Console-based directory navigator for selecting folders with the keyboard
- Implement fluent builder for BackupMetadata and FileMetadata (AddFile should add to the latest backup)
- Add unit tests for Backup, Restore and Integrity services
- Improve CLI parsing (add more flags/options and help output)
- Add logging and configurable log levels
- Add packaging and release pipeline
- Validate and fix reported issues (e.g. malformed markup tags)

License
Specify your license here (e.g. MIT).

Notes
If you want the Figlet rendering used in the app at runtime, see the `Program.cs` usage of Spectre.Console's `FigletText`:

```csharp
var title = new FigletText("FileKeeper")
    .LeftJustified()
    .Color(Color.Teal);
AnsiConsole.Write(title);
```

This README uses a static ASCII Figlet-style header to match the in-app appearance.

