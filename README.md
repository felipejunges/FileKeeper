# FileKeeper

FileKeeper is a **Backup Manager** project built with **.NET 10** and **AvaloniaUI**.

> **Status:** Under development.

## Overview

FileKeeper is designed to keep backup management simple and portable:

- Uses **SQLite** as its storage engine.
- Stores backup metadata in a **single database file**.
- Makes backups easy to move, copy, and archive as one file.

## Main Features

- **Incremental backups**: only changed data is added in new backup cycles.
- **Backup recycling**: includes options to recycle/remove old backups.
- **Desktop interface with AvaloniaUI** for local backup management.

## Tech Stack

- **.NET 10**
- **AvaloniaUI**
- **SQLite**

## Goal

Provide a lightweight, desktop-friendly backup manager that keeps historical backups organized while reducing storage usage with incremental backup strategy and old backup recycling. AvaloniaUI is used so the app builds for both Linux and Windows.

## How to Run

From the project root, run:

```bash
dotnet run --project FileKeeper.UI
```

## Building single executable (Linux and Windows)

AvaloniaUI enables cross-platform desktop builds. You can publish a single-file executable for Linux and Windows:

#### For Linux single file:

```bash
dotnet publish -c Release -o publish -r linux-x64 --self-contained true FileKeeper.UI/FileKeeper.UI.csproj
```

## Install on Linux (local machine)

Use the installer script to publish and install the app to your user folder:

```bash
./deploy/linux-mint/install-local.sh
```

This script:

- Publishes `FileKeeper.UI` to `./publish` (by default)
- Installs files to `~/.local/opt/filekeeper/publish`
- Creates a desktop launcher in `~/.local/share/applications/filekeeper.desktop`
- Optionally enables a user service (`systemd --user`)

### Installer options

```bash
./deploy/linux-mint/install-local.sh --help
```

Available options:

- `--enable-service` enable and start user service after install
- `--no-service` skip service enable/start
- `--publish` run `dotnet publish` before install (default)
- `--no-publish` skip publish and use existing `publish` folder
- `--configuration=<VALUE>` publish configuration (default: `Release`)
- `--runtime=<RID>` publish runtime RID (default: `linux-x64`)

Examples:

```bash
./deploy/linux-mint/install-local.sh --no-service
./deploy/linux-mint/install-local.sh --configuration=Debug --runtime=linux-x64
./deploy/linux-mint/install-local.sh --no-publish --enable-service
```

### Run after install

From the menu: open **FileKeeper**.

Or from terminal:

```bash
cd "$HOME/.local/opt/filekeeper/publish"
LD_LIBRARY_PATH="$PWD" ./FileKeeper.UI
```

### Uninstall

```bash
./deploy/linux-mint/uninstall-local.sh
```

Optionally, you can configure the binary in the system `bin` folder:
```bash
sudo cp publish/libe_sqlite3.so /usr/local/lib/
sudo ldconfig

sudo cp publish/FileKeeper /usr/local/bin/
```

Now, the `FileKeeper` binary is available in any directory of the system.

#### For Windows single file:

```bash
dotnet publish -c Release -o publish -r win-x64 --self-contained true FileKeeper.UI/FileKeeper.UI.csproj
```
