using ErrorOr;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Models.Options;
using System.Text.Json;

namespace FileKeeper.Core.Services;

public class UserSettingsWriter : IUserSettingsWriter
{
    private readonly string _settingsFilePath;

    public UserSettingsWriter(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
    }

    public async Task<ErrorOr<Success>> SaveAsync(UserSettingsOptions options, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        try
        {
            var payload = new Dictionary<string, UserSettingsOptions>
            {
                [UserSettingsOptions.SectionName] = new()
                {
                    SourceDirectories = options.SourceDirectories,
                    StorageDirectory = options.StorageDirectory,
                    IgnoredFolders = options.IgnoredFolders
                }
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });

            var parentDirectory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            var tempPath = _settingsFilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, token);
            File.Move(tempPath, _settingsFilePath, overwrite: true);

            return Result.Success;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Error.Failure($"Failed to persist user settings: {ex.Message}");
        }
    }
}

