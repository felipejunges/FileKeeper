using CommunityToolkit.Mvvm.Messaging.Messages;

namespace FileKeeper.Core;

public class BackupDeletedMessage : ValueChangedMessage<long>
{
    public BackupDeletedMessage(long backupId) : base(backupId) { }
}