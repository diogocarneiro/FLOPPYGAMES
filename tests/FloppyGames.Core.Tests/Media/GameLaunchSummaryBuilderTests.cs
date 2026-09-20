using FloppyGames.Core.Configuration;
using FloppyGames.Core.Media;
using FloppyGames.Core.Steam;
using FloppyGames.Core.Tests.Steam;

namespace FloppyGames.Core.Tests.Media;

public class GameLaunchSummaryBuilderTests : IDisposable
{
    private static readonly GameConfig Config = new() { Title = "Portal", AppId = 400, Process = "portal.exe" };

    private readonly string _driveRoot;

    public GameLaunchSummaryBuilderTests()
    {
        _driveRoot = Path.Combine(Path.GetTempPath(), "FloppyGamesTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_driveRoot);
        File.WriteAllText(Path.Combine(_driveRoot, "GAME.INI"), "[Game]\nTITLE=Portal\nAPPID=400\nPROCESS=portal.exe\n");
    }

    public void Dispose() => Directory.Delete(_driveRoot, recursive: true);

    [Fact]
    public void Build_SameMediaContent_ProducesSameCrc32AndPositiveSize()
    {
        var builder = new GameLaunchSummaryBuilder(new SteamLibraryScanner(new FakeSteamFileSystem(), new FakeSteamPathProvider(null)));

        var first = builder.Build(_driveRoot, Config);
        var second = builder.Build(_driveRoot, Config);

        Assert.Equal(first.MediaCrc32, second.MediaCrc32);
        Assert.True(first.MediaSizeBytes > 0);
    }

    [Fact]
    public void Build_MediaContentChanges_Crc32Changes()
    {
        var builder = new GameLaunchSummaryBuilder(new SteamLibraryScanner(new FakeSteamFileSystem(), new FakeSteamPathProvider(null)));
        var before = builder.Build(_driveRoot, Config);

        File.WriteAllText(Path.Combine(_driveRoot, "GAME.INI"), "[Game]\nTITLE=Portal 2\nAPPID=400\nPROCESS=portal2.exe\n");
        var after = builder.Build(_driveRoot, Config);

        Assert.NotEqual(before.MediaCrc32, after.MediaCrc32);
    }

    [Fact]
    public void Build_NotAFloppyLetter_ClassifiesAsUsb()
    {
        var builder = new GameLaunchSummaryBuilder(new SteamLibraryScanner(new FakeSteamFileSystem(), new FakeSteamPathProvider(null)));

        var summary = builder.Build(_driveRoot, Config);

        Assert.Equal(MediaKind.Usb, summary.MediaKind);
    }

    [Fact]
    public void Build_GameInstalledOnSteam_ReportsInstalledWithSizeOnDisk()
    {
        const string manifest = """
            "AppState"
            {
                "appid"        "400"
                "name"        "Portal"
                "installdir"        "Portal"
                "SizeOnDisk"        "1000"
            }
            """;
        var fs = new FakeSteamFileSystem().WithFile(@"C:\Steam\steamapps\appmanifest_400.acf", manifest);
        var builder = new GameLaunchSummaryBuilder(new SteamLibraryScanner(fs, new FakeSteamPathProvider(@"C:\Steam")));

        var summary = builder.Build(_driveRoot, Config);

        Assert.True(summary.IsInstalledOnSteam);
        Assert.Equal(1000, summary.InstalledSizeBytes);
    }

    [Fact]
    public void Build_GameNotInstalledOnSteam_ReportsNotInstalled()
    {
        var builder = new GameLaunchSummaryBuilder(new SteamLibraryScanner(new FakeSteamFileSystem(), new FakeSteamPathProvider(null)));

        var summary = builder.Build(_driveRoot, Config);

        Assert.False(summary.IsInstalledOnSteam);
        Assert.Null(summary.InstalledSizeBytes);
    }
}
