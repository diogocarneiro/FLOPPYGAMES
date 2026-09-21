using FloppyGames.Core.Configuration;
using FloppyGames.Core.Media;
using FloppyGames.Core.Platforms;
using FloppyGames.Core.Settings;
using FloppyGames.Core.Steam;
using FloppyGames.Core.Tests.Steam;

namespace FloppyGames.Core.Tests.Media;

public class GameLaunchSummaryBuilderTests : IDisposable
{
    private static readonly GameConfig SteamConfig = new()
    {
        Title = "Portal", Platform = GamePlatform.Steam, AppId = 400, Process = "portal.exe",
    };

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
        FakeSteamAchievementsProvider? achievementsProvider = null, string? epicManifestsDirectory = null)
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
            settingsStore,
            new EpicGameLibraryScanner(fs, epicManifestsDirectory ?? Path.Combine(_driveRoot, "EpicManifests")),
            new GogGameLibraryScanner());
    }

    [Fact]
    public async Task BuildAsync_MediaOnDrive_ReportsPositiveSize()
    {
        var builder = BuildBuilder(new FakeSteamFileSystem(), null);

        var summary = await builder.BuildAsync(_driveRoot, SteamConfig, CancellationToken.None);

        Assert.True(summary.MediaSizeBytes > 0);
    }

    [Fact]
    public async Task BuildAsync_NotAFloppyLetter_ClassifiesAsUsb()
    {
        var builder = BuildBuilder(new FakeSteamFileSystem(), null);

        var summary = await builder.BuildAsync(_driveRoot, SteamConfig, CancellationToken.None);

        Assert.Equal(MediaKind.Usb, summary.MediaKind);
    }

    [Fact]
    public async Task BuildAsync_SteamGameNotInstalled_ReportsNotInstalledAndOmitsInstallOnlyFields()
    {
        var builder = BuildBuilder(new FakeSteamFileSystem(), null);

        var summary = await builder.BuildAsync(_driveRoot, SteamConfig, CancellationToken.None);

        Assert.Equal(GamePlatform.Steam, summary.Platform);
        Assert.False(summary.IsInstalled);
        Assert.Null(summary.InstalledSizeBytes);
        Assert.Null(summary.BuildId);
        Assert.Null(summary.LastUpdatedUtc);
        Assert.Null(summary.PlaytimeMinutes);
        Assert.Null(summary.Achievements);
    }

    [Fact]
    public async Task BuildAsync_SteamGameInstalled_ReportsSizeBuildAndUpdateDate()
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

        var summary = await builder.BuildAsync(_driveRoot, SteamConfig, CancellationToken.None);

        Assert.True(summary.IsInstalled);
        Assert.Equal(1000, summary.InstalledSizeBytes);
        Assert.Equal("9988", summary.BuildId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000).UtcDateTime, summary.LastUpdatedUtc);
    }

    [Fact]
    public async Task BuildAsync_SteamGameInstalledWithKnownOwner_ReadsPlaytimeFromLocalConfig()
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

        var summary = await builder.BuildAsync(_driveRoot, SteamConfig, CancellationToken.None);

        Assert.Equal(125, summary.PlaytimeMinutes);
    }

    [Fact]
    public async Task BuildAsync_SteamGameInstalledButNoLocalConfig_PlaytimeIsNull()
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

        var summary = await builder.BuildAsync(_driveRoot, SteamConfig, CancellationToken.None);

        Assert.True(summary.IsInstalled);
        Assert.Null(summary.PlaytimeMinutes);
    }

    [Fact]
    public async Task BuildAsync_SteamInstalledWithApiKeyConfigured_ReportsAchievements()
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

        var summary = await builder.BuildAsync(_driveRoot, SteamConfig, CancellationToken.None);

        Assert.NotNull(summary.Achievements);
        Assert.Equal(7, summary.Achievements!.Unlocked);
        Assert.Equal(20, summary.Achievements.Total);
    }

    [Fact]
    public async Task BuildAsync_SteamInstalledWithoutApiKey_NeverCallsAchievementsProvider()
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

        var summary = await builder.BuildAsync(_driveRoot, SteamConfig, CancellationToken.None);

        Assert.Null(summary.Achievements);
        Assert.Null(achievementsProvider.LastApiKey);
    }

    [Fact]
    public async Task BuildAsync_SteamNotInstalled_NeverCallsAchievementsProviderEvenWithApiKey()
    {
        var fs = new FakeSteamFileSystem();
        var achievementsProvider = new FakeSteamAchievementsProvider(new AchievementSummary(7, 20));
        var builder = BuildBuilder(fs, null, apiKey: "TESTKEY", achievementsProvider: achievementsProvider);

        var summary = await builder.BuildAsync(_driveRoot, SteamConfig, CancellationToken.None);

        Assert.Null(summary.Achievements);
        Assert.Null(achievementsProvider.LastApiKey);
    }

    [Fact]
    public async Task BuildAsync_EpicGameInstalled_ReportsInstalledWithSizeAndNoSteamOnlyFields()
    {
        const string manifest = """
            {
                "DisplayName": "INSIDE",
                "InstallLocation": "C:\\Program Files\\Epic Games\\Inside",
                "InstallSize": 2246081768,
                "CatalogNamespace": "13bb5776b9e1424d84ce42d9ba61c0ca",
                "CatalogItemId": "6fdb5feba66846cd8623a6d15ca68080",
                "AppName": "Marigold"
            }
            """;
        var epicDir = Path.Combine(_driveRoot, "EpicManifests");
        var fs = new FakeSteamFileSystem().WithFile(Path.Combine(epicDir, "game.item"), manifest);
        var builder = BuildBuilder(fs, null, epicManifestsDirectory: epicDir);

        var epicConfig = new GameConfig
        {
            Title = "INSIDE",
            Platform = GamePlatform.Epic,
            EpicNamespace = "13bb5776b9e1424d84ce42d9ba61c0ca",
            EpicItemId = "6fdb5feba66846cd8623a6d15ca68080",
            EpicAppName = "Marigold",
            Process = "INSIDE.exe",
        };

        var summary = await builder.BuildAsync(_driveRoot, epicConfig, CancellationToken.None);

        Assert.Equal(GamePlatform.Epic, summary.Platform);
        Assert.True(summary.IsInstalled);
        Assert.Equal(2246081768, summary.InstalledSizeBytes);
        Assert.Null(summary.BuildId);
        Assert.Null(summary.PlaytimeMinutes);
        Assert.Null(summary.Achievements);
    }

    [Fact]
    public async Task BuildAsync_EpicGameNotInstalled_ReportsNotInstalled()
    {
        var epicDir = Path.Combine(_driveRoot, "EpicManifests");
        var builder = BuildBuilder(new FakeSteamFileSystem(), null, epicManifestsDirectory: epicDir);

        var epicConfig = new GameConfig
        {
            Title = "INSIDE",
            Platform = GamePlatform.Epic,
            EpicNamespace = "ns",
            EpicItemId = "item",
            EpicAppName = "app",
            Process = "INSIDE.exe",
        };

        var summary = await builder.BuildAsync(_driveRoot, epicConfig, CancellationToken.None);

        Assert.False(summary.IsInstalled);
        Assert.Null(summary.InstalledSizeBytes);
    }

    [Fact]
    public async Task BuildAsync_GogGameWithUnknownId_ReportsNotInstalled()
    {
        var builder = BuildBuilder(new FakeSteamFileSystem(), null);

        var gogConfig = new GameConfig
        {
            Title = "Some Game",
            Platform = GamePlatform.Gog,
            GogGameId = "0000000000-unlikely-to-exist",
            Process = "game.exe",
        };

        var summary = await builder.BuildAsync(_driveRoot, gogConfig, CancellationToken.None);

        Assert.Equal(GamePlatform.Gog, summary.Platform);
        Assert.False(summary.IsInstalled);
    }
}
