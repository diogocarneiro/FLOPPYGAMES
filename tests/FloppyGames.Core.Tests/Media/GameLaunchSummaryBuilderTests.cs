using FloppyGames.Core.Configuration;
using FloppyGames.Core.Media;
using FloppyGames.Core.Settings;
using FloppyGames.Core.Steam;
using FloppyGames.Core.Tests.Steam;

namespace FloppyGames.Core.Tests.Media;

public class GameLaunchSummaryBuilderTests : IDisposable
{
    private const ulong OwnerSteamId64 = 76561197960265728UL + 12345UL;

    private static readonly GameConfig Config = new() { Title = "Portal", AppId = 400, Process = "portal.exe" };

    private readonly string _driveRoot;

    public GameLaunchSummaryBuilderTests()
    {
        _driveRoot = Path.Combine(Path.GetTempPath(), "FloppyGamesTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_driveRoot);
        File.WriteAllText(Path.Combine(_driveRoot, "GAME.INI"), "[Game]\nTITLE=Portal\nAPPID=400\nPROCESS=portal.exe\n");
    }

    public void Dispose() => Directory.Delete(_driveRoot, recursive: true);

    private GameLaunchSummaryBuilder BuildBuilder(
        FakeSteamFileSystem fs, string? steamPath, AchievementSummary? achievementsResult = null, string? apiKey = null,
        FakeSteamAchievementsProvider? achievementsProvider = null)
    {
        var settingsStore = new AgentSettingsStore(Path.Combine(_driveRoot, "settings.json"));
        if (apiKey is not null)
        {
            settingsStore.Save(new AgentSettings { SteamWebApiKey = apiKey });
        }

        return new GameLaunchSummaryBuilder(
            new SteamLibraryScanner(fs, new FakeSteamPathProvider(steamPath)),
            new SteamPlaytimeReader(fs, new FakeSteamPathProvider(steamPath)),
            achievementsProvider ?? new FakeSteamAchievementsProvider(achievementsResult),
            settingsStore);
    }

    [Fact]
    public async Task BuildAsync_MediaOnDrive_ReportsPositiveSize()
    {
        var builder = BuildBuilder(new FakeSteamFileSystem(), null);

        var summary = await builder.BuildAsync(_driveRoot, Config, CancellationToken.None);

        Assert.True(summary.MediaSizeBytes > 0);
    }

    [Fact]
    public async Task BuildAsync_NotAFloppyLetter_ClassifiesAsUsb()
    {
        var builder = BuildBuilder(new FakeSteamFileSystem(), null);

        var summary = await builder.BuildAsync(_driveRoot, Config, CancellationToken.None);

        Assert.Equal(MediaKind.Usb, summary.MediaKind);
    }

    [Fact]
    public async Task BuildAsync_GameNotInstalledOnSteam_ReportsNotInstalledAndOmitsInstallOnlyFields()
    {
        var builder = BuildBuilder(new FakeSteamFileSystem(), null);

        var summary = await builder.BuildAsync(_driveRoot, Config, CancellationToken.None);

        Assert.False(summary.IsInstalledOnSteam);
        Assert.Null(summary.InstalledSizeBytes);
        Assert.Null(summary.BuildId);
        Assert.Null(summary.LastUpdatedUtc);
        Assert.Null(summary.PlaytimeMinutes);
        Assert.Null(summary.Achievements);
    }

    [Fact]
    public async Task BuildAsync_GameInstalledOnSteam_ReportsSizeBuildAndUpdateDate()
    {
        const string manifest = """
            "AppState"
            {
                "appid"        "400"
                "name"        "Portal"
                "installdir"        "Portal"
                "SizeOnDisk"        "1000"
                "buildid"        "9988"
                "LastUpdated"        "1700000000"
            }
            """;
        var fs = new FakeSteamFileSystem().WithFile(@"C:\Steam\steamapps\appmanifest_400.acf", manifest);
        var builder = BuildBuilder(fs, @"C:\Steam");

        var summary = await builder.BuildAsync(_driveRoot, Config, CancellationToken.None);

        Assert.True(summary.IsInstalledOnSteam);
        Assert.Equal(1000, summary.InstalledSizeBytes);
        Assert.Equal("9988", summary.BuildId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000).UtcDateTime, summary.LastUpdatedUtc);
    }

    [Fact]
    public async Task BuildAsync_GameInstalledWithKnownOwner_ReadsPlaytimeFromLocalConfig()
    {
        const string manifest = """
            "AppState"
            {
                "appid"        "400"
                "name"        "Portal"
                "installdir"        "Portal"
                "LastOwner"        "76561197960278073"
            }
            """;
        const string localConfig = """
            "UserLocalConfigStore"
            {
                "Software"
                {
                    "Valve"
                    {
                        "Steam"
                        {
                            "apps"
                            {
                                "400"
                                {
                                    "LastPlayed"        "1690000000"
                                    "Playtime"        "125"
                                }
                            }
                        }
                    }
                }
            }
            """;
        var fs = new FakeSteamFileSystem()
            .WithFile(@"C:\Steam\steamapps\appmanifest_400.acf", manifest)
            .WithFile(@"C:\Steam\userdata\12345\config\localconfig.vdf", localConfig);
        var builder = BuildBuilder(fs, @"C:\Steam");

        var summary = await builder.BuildAsync(_driveRoot, Config, CancellationToken.None);

        Assert.Equal(125, summary.PlaytimeMinutes);
    }

    [Fact]
    public async Task BuildAsync_GameInstalledButNoLocalConfig_PlaytimeIsNull()
    {
        const string manifest = """
            "AppState"
            {
                "appid"        "400"
                "name"        "Portal"
                "installdir"        "Portal"
                "LastOwner"        "76561197960278073"
            }
            """;
        var fs = new FakeSteamFileSystem().WithFile(@"C:\Steam\steamapps\appmanifest_400.acf", manifest);
        var builder = BuildBuilder(fs, @"C:\Steam");

        var summary = await builder.BuildAsync(_driveRoot, Config, CancellationToken.None);

        Assert.True(summary.IsInstalledOnSteam);
        Assert.Null(summary.PlaytimeMinutes);
    }

    [Fact]
    public async Task BuildAsync_InstalledWithApiKeyConfigured_ReportsAchievements()
    {
        const string manifest = """
            "AppState"
            {
                "appid"        "400"
                "name"        "Portal"
                "installdir"        "Portal"
                "LastOwner"        "76561197960278073"
            }
            """;
        var fs = new FakeSteamFileSystem().WithFile(@"C:\Steam\steamapps\appmanifest_400.acf", manifest);
        var builder = BuildBuilder(fs, @"C:\Steam", achievementsResult: new AchievementSummary(7, 20), apiKey: "TESTKEY");

        var summary = await builder.BuildAsync(_driveRoot, Config, CancellationToken.None);

        Assert.NotNull(summary.Achievements);
        Assert.Equal(7, summary.Achievements!.Unlocked);
        Assert.Equal(20, summary.Achievements.Total);
    }

    [Fact]
    public async Task BuildAsync_InstalledWithoutApiKey_NeverCallsAchievementsProvider()
    {
        const string manifest = """
            "AppState"
            {
                "appid"        "400"
                "name"        "Portal"
                "installdir"        "Portal"
                "LastOwner"        "76561197960278073"
            }
            """;
        var fs = new FakeSteamFileSystem().WithFile(@"C:\Steam\steamapps\appmanifest_400.acf", manifest);
        var achievementsProvider = new FakeSteamAchievementsProvider(new AchievementSummary(7, 20));
        var builder = BuildBuilder(fs, @"C:\Steam", achievementsProvider: achievementsProvider);

        var summary = await builder.BuildAsync(_driveRoot, Config, CancellationToken.None);

        Assert.Null(summary.Achievements);
        Assert.Null(achievementsProvider.LastApiKey);
    }

    [Fact]
    public async Task BuildAsync_NotInstalled_NeverCallsAchievementsProviderEvenWithApiKey()
    {
        var fs = new FakeSteamFileSystem();
        var achievementsProvider = new FakeSteamAchievementsProvider(new AchievementSummary(7, 20));
        var builder = BuildBuilder(fs, null, apiKey: "TESTKEY", achievementsProvider: achievementsProvider);

        var summary = await builder.BuildAsync(_driveRoot, Config, CancellationToken.None);

        Assert.Null(summary.Achievements);
        Assert.Null(achievementsProvider.LastApiKey);
    }
}
