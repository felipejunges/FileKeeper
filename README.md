# FileKeeper

FileKeeper is a **Backup Manager** project built with **.NET 10** and **GTK**.

> **Status:** Under development.

## Overview

FileKeeper is designed to keep backup management simple and portable:

- Uses **SQLite** as its storage engine.
- Stores backup metadata in a **single database file**.
- Makes backups easy to move, copy, and archive as one file.

## Main Features

- **Incremental backups**: only changed data is added in new backup cycles.
- **Backup recycling**: includes options to recycle/remove old backups.
- **Desktop interface with GTK** for local backup management.

## Tech Stack

- **.NET 10**
- **GTK**
- **SQLite**

## Goal

Provide a lightweight, desktop-friendly backup manager that keeps historical backups organized while reducing storage usage with incremental backup strategy and old backup recycling. The goal is to make this app run on both Linux and Windows.

## How to Run

From the project root, run:

```bash
dotnet run --project FileKeeper.Gtk
```

## Building single executable (Linux and Windows):

If you like, you can build this project as a Linux single file:

```bash
dotnet publish -c Release -o publish -r linux-x64 --self-contained true FileKeeper.Gtk/FileKeeper.Gtk.csproj
```

For Windows single file:

```bash
dotnet publish -c Release -o publish -r win-x64 --self-contained true FileKeeper.Gtk/FileKeeper.Gtk.csproj
```
