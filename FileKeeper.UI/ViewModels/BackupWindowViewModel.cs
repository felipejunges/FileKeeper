using CommunityToolkit.Mvvm.ComponentModel;
using FileKeeper.Core.Models.Entities;

namespace FileKeeper.UI.ViewModels;

public partial class BackupWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private Backup _backup;

    public BackupWindowViewModel(Backup backup)
    {
        _backup = backup;
    }
}
