using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FileKeeper.Core;
using FileKeeper.Core.Interfaces.UI;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Models.Entities;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FileKeeper.UI.ViewModels;

public partial class BackupWindowViewModel : ViewModelBase, IInitializable
{
    [ObservableProperty] private Backup? _backup;

    public event Action? RequestClose;
    private Window? _window;

    private readonly IDeleteBackupUseCase _deleteBackupUseCase;
    private readonly IRestoreBackupUseCase _restoreBackupUseCase;
    private readonly IFolderPickerService  _folderPickerService;

    public BackupWindowViewModel(
        IDeleteBackupUseCase deleteBackupUseCase,
        IRestoreBackupUseCase restoreBackupUseCase,
        IFolderPickerService folderPickerService)
    {
        _deleteBackupUseCase = deleteBackupUseCase;
        _restoreBackupUseCase = restoreBackupUseCase;
        _folderPickerService = folderPickerService;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
    
    [RelayCommand]
    private async Task DeleteBackup(CancellationToken cancellationToken)
    {
        if (Backup is null) return;
        
        var box = MessageBoxManager.GetMessageBoxStandard(
            "Confirm backup deletion", 
            $"Confirm delete backup {Backup.Id}?",
            ButtonEnum.YesNo);
        
        var boxResult = await box.ShowWindowDialogAsync(_window!);
        if (boxResult != ButtonResult.Yes)
            return;
        
        var result = await _deleteBackupUseCase.ExecuteAsync(Backup.Id, cancellationToken);

        if (result.IsError)
        {
            // TODO: pensar sobre: abstrair para uma interface própria
            var messageBox = MessageBoxManager.GetMessageBoxStandard(
                "Error while deleting backup",
                result.FirstError.Description,
                ButtonEnum.Ok,
                Icon.Error
            );

            await messageBox.ShowWindowDialogAsync(_window!);

            return;
        }
        
        WeakReferenceMessenger.Default.Send(new BackupDeletedMessage(Backup.Id));
        
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private async Task RestoreBackup(CancellationToken cancellationToken)
    {
        if (Backup is null) return;

        var destinationFolder = await _folderPickerService.PickFolderAsync("Select destination folder", cancellationToken);

        if (string.IsNullOrWhiteSpace(destinationFolder))
            return;

        var box = MessageBoxManager.GetMessageBoxStandard(
            "Confirm backup restoration", 
            $"Confirm restore backup {Backup.Id} into folder {destinationFolder}?",
            ButtonEnum.YesNo);
        
        var boxResult = await box.ShowWindowDialogAsync(_window!);
        if (boxResult != ButtonResult.Yes)
            return;

        var result = await _restoreBackupUseCase.ExecuteAsync(Backup.Id, destinationFolder, cancellationToken);

        if (result.IsError)
        {
            // TODO: pensar sobre: abstrair para uma interface própria
            var messageBox = MessageBoxManager.GetMessageBoxStandard(
                "Error while restoring backup",
                result.FirstError.Description,
                ButtonEnum.Ok,
                Icon.Error
            );

            await messageBox.ShowWindowDialogAsync(_window!);
        }
    }

    public void Setbackup(Backup backup)=> Backup = backup;
    public void SetWindow(Window window) => _window = window;
}