using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileKeeper.Core.Models.DMs;

namespace FileKeeper.UI.ViewModels;

public partial class FilesWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _currentDirectory = ".";

    [ObservableProperty]
    private ObservableCollection<BackupedFileDM> _files = new();

    [ObservableProperty]
    private ObservableCollection<FileVersionDM> _selectedFileVersions = new();

    [ObservableProperty]
    private BackupedFileDM? _selectedFile;

    public FilesWindowViewModel()
    {
        // Mock data for initial view
        Files.Add(new BackupedFileDM { FileName = "Folder1", RelativePath = "", BackupPath = "C:\\Backups", Size = 0 });
        Files.Add(new BackupedFileDM { FileName = "file1.txt", RelativePath = ".", BackupPath = "C:\\Backups", Size = 1024 });
    }

    [RelayCommand]
    private void NavigateToDirectory(BackupedFileDM folder)
    {
        CurrentDirectory = folder.FileName;
        // In real app, load files from this directory
    }

    partial void OnSelectedFileChanged(BackupedFileDM? value)
    {
        if (value != null)
        {
            // Mock versions for the selected file
            SelectedFileVersions.Clear();
            SelectedFileVersions.Add(new FileVersionDM 
            { 
                Id = 1, 
                FileName = value.FileName, 
                BackupPath = value.BackupPath, 
                RelativePath = value.RelativePath, 
                CurrentHash = "HASH1" 
            });
        }
    }
}
