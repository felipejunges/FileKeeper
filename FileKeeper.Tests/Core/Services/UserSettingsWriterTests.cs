using FileKeeper.Core.Models.Options;
using FileKeeper.Core.Services;
using System.Text.Json;

namespace FileKeeper.Tests.Core.Services;

public sealed class UserSettingsWriterTests : IAsyncLifetime
{
    private string _settingsFilePath = null!;
    private string _testDirectory = null!;

    public Task InitializeAsync()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"filekeeper-user-settings-{Guid.NewGuid():N}");
        _settingsFilePath = Path.Combine(_testDirectory, "user-settings.json");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task SaveAsync_WhenOptionsAreValid_WritesFileKeeperSection()
    {
        var sut = new UserSettingsWriter(_settingsFilePath);
        var options = new UserSettingsOptions
        {
            SourceDirectories = ["/home/felipe/docs", "/home/felipe/images"],
            StorageDirectory = "/var/backups/filekeeper"
        };

        var result = await sut.SaveAsync(options, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.True(File.Exists(_settingsFilePath));

        var json = await File.ReadAllTextAsync(_settingsFilePath);
        using var document = JsonDocument.Parse(json);
        var fileKeeper = document.RootElement.GetProperty(UserSettingsOptions.SectionName);

        Assert.Equal(options.StorageDirectory, fileKeeper.GetProperty(nameof(UserSettingsOptions.StorageDirectory)).GetString());
        Assert.Equal(options.SourceDirectories[0], fileKeeper.GetProperty(nameof(UserSettingsOptions.SourceDirectories))[0].GetString());
        Assert.Equal(options.SourceDirectories[1], fileKeeper.GetProperty(nameof(UserSettingsOptions.SourceDirectories))[1].GetString());
    }

    [Fact]
    public async Task SaveAsync_WhenTokenIsCanceled_ThrowsOperationCanceledException()
    {
        var sut = new UserSettingsWriter(_settingsFilePath);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.SaveAsync(new UserSettingsOptions(), cts.Token));
    }
}

