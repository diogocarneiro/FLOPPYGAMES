using FloppyGames.Core.Settings;

namespace FloppyGames.Core.Tests.Settings;

public class AgentSettingsStoreTests : IDisposable
{
    private readonly string _settingsPath;

    public AgentSettingsStoreTests()
    {
        _settingsPath = Path.Combine(Path.GetTempPath(), "FloppyGamesTests_" + Guid.NewGuid(), "settings.json");
    }

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_FileDoesNotExist_ReturnsEmptySettings()
    {
        var store = new AgentSettingsStore(_settingsPath);

        var settings = store.Load();

        Assert.Null(settings.SteamWebApiKey);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var store = new AgentSettingsStore(_settingsPath);

        store.Save(new AgentSettings { SteamWebApiKey = "ABC123" });
        var settings = store.Load();

        Assert.Equal("ABC123", settings.SteamWebApiKey);
    }

    [Fact]
    public void Save_CreatesParentDirectory()
    {
        var store = new AgentSettingsStore(_settingsPath);

        store.Save(new AgentSettings { SteamWebApiKey = "ABC123" });

        Assert.True(File.Exists(_settingsPath));
    }

    [Fact]
    public void Load_CorruptFile_ReturnsEmptySettingsInsteadOfThrowing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(_settingsPath, "not valid json {{{");
        var store = new AgentSettingsStore(_settingsPath);

        var settings = store.Load();

        Assert.Null(settings.SteamWebApiKey);
    }
}
