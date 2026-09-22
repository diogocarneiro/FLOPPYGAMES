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
        Assert.True(settings.PlayFloppySound);
        Assert.True(settings.CrtEffectEnabled);
        Assert.True(settings.SteamEnabled);
        Assert.False(settings.EpicEnabled);
        Assert.False(settings.GogEnabled);
        Assert.False(settings.NfcEnabled);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsPlatformToggles()
    {
        var store = new AgentSettingsStore(_settingsPath);

        store.Save(new AgentSettings { SteamEnabled = false, EpicEnabled = true, GogEnabled = true });
        var settings = store.Load();

        Assert.False(settings.SteamEnabled);
        Assert.True(settings.EpicEnabled);
        Assert.True(settings.GogEnabled);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsNfcEnabled()
    {
        var store = new AgentSettingsStore(_settingsPath);

        store.Save(new AgentSettings { NfcEnabled = true });
        var settings = store.Load();

        Assert.True(settings.NfcEnabled);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsNfcCardPassword()
    {
        var store = new AgentSettingsStore(_settingsPath);

        store.Save(new AgentSettings { NfcCardPassword = "hunter2" });
        var settings = store.Load();

        Assert.Equal("hunter2", settings.NfcCardPassword);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsPlayFloppySound()
    {
        var store = new AgentSettingsStore(_settingsPath);

        store.Save(new AgentSettings { PlayFloppySound = false });
        var settings = store.Load();

        Assert.False(settings.PlayFloppySound);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsCrtEffectEnabled()
    {
        var store = new AgentSettingsStore(_settingsPath);

        store.Save(new AgentSettings { CrtEffectEnabled = false });
        var settings = store.Load();

        Assert.False(settings.CrtEffectEnabled);
    }

    [Fact]
    public void Save_UpdatingOneField_PreservesTheOther()
    {
        var store = new AgentSettingsStore(_settingsPath);

        store.Save(new AgentSettings { SteamWebApiKey = "ABC123", PlayFloppySound = false });
        var current = store.Load();
        store.Save(current with { SteamWebApiKey = "XYZ789" });
        var settings = store.Load();

        Assert.Equal("XYZ789", settings.SteamWebApiKey);
        Assert.False(settings.PlayFloppySound);
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
