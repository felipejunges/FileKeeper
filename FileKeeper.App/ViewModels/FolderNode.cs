using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FileKeeper.Core;

namespace FileKeeper.App.ViewModels;

/// <summary>A folder of the backed up tree, built from the files' display paths.</summary>
public sealed partial class FolderNode : ObservableObject
{
    private readonly Dictionary<string, FolderNode> _byName = new(StringComparer.Ordinal);

    [ObservableProperty]
    private bool _isExpanded;

    public FolderNode(string name, FolderNode? parent)
    {
        Name = name;
        Parent = parent;
        DisplayPath = parent is null ? string.Empty
            : parent.Parent is null ? name
            : parent.DisplayPath + "/" + name;
    }

    public string Name { get; }

    public FolderNode? Parent { get; }

    /// <summary>Path relative to the root ("" for the root itself).</summary>
    public string DisplayPath { get; }

    public ObservableCollection<FolderNode> Folders { get; } = [];

    public List<FileRowViewModel> Files { get; } = [];

    public int FileCount { get; private set; }

    public long TotalSize { get; private set; }

    public bool HasPresentFiles { get; private set; }

    public string FileCountText => $"{FileCount} file(s)";

    public string TotalSizeText => Formatting.Bytes(TotalSize);

    public IEnumerable<FolderNode> SelfAndAncestors()
    {
        for (var node = this; node is not null; node = node.Parent)
            yield return node;
    }

    public IEnumerable<FileRowViewModel> AllFiles() => Files.Concat(Folders.SelectMany(f => f.AllFiles()));

    /// <summary>Returns the folder at <paramref name="displayPath"/>, or the deepest existing ancestor.</summary>
    public FolderNode Find(string displayPath)
    {
        var node = this;
        foreach (var segment in displayPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!node._byName.TryGetValue(segment, out var child))
                break;
            node = child;
        }

        return node;
    }

    /// <summary>Builds the folder tree for <paramref name="files"/>.</summary>
    public static FolderNode Build(string rootName, IEnumerable<FileRowViewModel> files)
    {
        var root = new FolderNode(rootName, null) { IsExpanded = true };
        foreach (var file in files)
        {
            var segments = file.DisplayPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var node = root;
            foreach (var segment in segments[..^1])
            {
                if (!node._byName.TryGetValue(segment, out var child))
                    node._byName[segment] = child = new FolderNode(segment, node);
                node = child;
            }

            node.Files.Add(file);
        }

        root.Complete();

        // Expand the chain of single folders leading to the sources (home > me > Dropbox).
        for (var node = root; node.Files.Count == 0 && node.Folders.Count == 1; node = node.Folders[0])
            node.Folders[0].IsExpanded = true;

        return root;
    }

    private void Complete()
    {
        foreach (var child in _byName.Values.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
        {
            child.Complete();
            Folders.Add(child);
        }

        Files.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.FileName, b.FileName));
        FileCount = Files.Count + Folders.Sum(f => f.FileCount);
        TotalSize = Files.Sum(f => f.History.LatestVersion.Size) + Folders.Sum(f => f.TotalSize);
        HasPresentFiles = Files.Any(f => f.History.ExistsInLatest) || Folders.Any(f => f.HasPresentFiles);
    }
}

/// <summary>A row of the folder content list: either a sub folder or a file.</summary>
public sealed class BrowserEntryViewModel
{
    private BrowserEntryViewModel(string name, FolderNode? folder, FileRowViewModel? file)
    {
        Name = name;
        Folder = folder;
        File = file;
    }

    public static BrowserEntryViewModel ForFolder(FolderNode folder) => new(folder.Name, folder, null);

    public static BrowserEntryViewModel ForFile(FileRowViewModel file, string name) => new(name, null, file);

    public string Name { get; }

    public FolderNode? Folder { get; }

    public FileRowViewModel? File { get; }

    public bool IsFolder => Folder is not null;

    public bool IsFile => File is not null;

    public string VersionsText => Folder is not null ? Folder.FileCountText : $"{File!.VersionCount}";

    public string SizeText => Folder?.TotalSizeText ?? File!.LatestSizeText;

    public string StatusText => Folder is not null
        ? Folder.HasPresentFiles ? string.Empty : "Deleted"
        : File!.StatusText;

    public string SnapshotsText => File?.SnapshotsText ?? string.Empty;

    public double Opacity => (Folder?.HasPresentFiles ?? File!.History.ExistsInLatest) ? 1.0 : 0.55;

    public string ToolTip => Folder?.DisplayPath ?? File!.Path;
}
