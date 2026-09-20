using FloppyGames.Core.Configuration;
using FloppyGames.Core.Media;
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

    private static GameLaunchSummaryBuilder BuildBuilder(FakeSteamFileSystem fs, string? steamPath) =>
        new(new SteamLibraryScanner(fs, new FakeSteamPathProvider(steamPath)), new SteamPlaytimeReader(fs, new FakeSteamPathProvider(steamPath)));

    [Fact]
    public void Build_MediaOnDrive_ReportsPositiveSize()
    {
        var builder = BuildBuilder(new FakeSteamFileSystem(), null);

        var summary = builder.Build(_driveRoot, Config);

        Assert.True(summary.MediaSizeBytes > 0);
    }

    [Fact]
    public void Build_NotAFloppyLetter_ClassifiesAsUsb()
    {
        var builder = BuildBuilder(new FakeSteamFileSystem(), null);

        var summary = builder.Build(_driveRoot, Config);

        Assert.Equal(MediaKind.Usb, summary.MediaKind);
    }

    [Fact]
    public void Build_GameNotInstalledOnSteam_ReportsNotInstalledAndOmitsInstallOnlyFields()
    {
        var builder = BuildBuilder(new FakeSteamFileSystem(), null);

        var summary = builder.Build(_driveRoot, Config);

        Assert.False(summary.IsInstalledOnSteam);
        Assert.Null(summary.InstalledSizeBytes);
        Assert.Null(summary.BuildId);
        Assert.Null(summary.LastUpdatedUtc);
        Assert.Null(summary.PlaytimeMinutes);
    }

    [Fact]
    public void Build_GameInstalledOnSteam_ReportsSizeBuildAndUpdateDate()
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

        var summary = builder.Build(_driveRoot, Config);

        Assert.True(summary.IsInstalledOnSteam);
        Assert.Equal(1000, summary.InstalledSizeBytes);
        Assert.Equal("9988", summary.BuildId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000).UtcDateTime, summary.LastUpdatedUtc);
    }

    [Fact]
    public void Build_GameInstalledWithKnownOwner_ReadsPlaytimeFromLocalConfig()
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

        var summary = builder.Build(_driveRoot, Config);

        Assert.Equal(125, summary.PlaytimeMinutes);
    }

    [Fact]
    public void Build_GameInstalledButNoLocalConfig_PlaytimeIsNull()
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

        var summary = builder.Build(_driveRoot, Config);

        Assert.True(summary.IsInstalledOnSteam);
        Assert.Null(summary.PlaytimeMinutes);
    }
}
