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
