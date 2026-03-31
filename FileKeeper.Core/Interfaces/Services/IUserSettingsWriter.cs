using ErrorOr;
using FileKeeper.Core.Models.Options;

namespace FileKeeper.Core.Interfaces.Services;

public interface IUserSettingsWriter
{
    Task<ErrorOr<Success>> SaveAsync(UserSettingsOptions options, CancellationToken token);
}

