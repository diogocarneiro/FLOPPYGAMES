using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Tests.Configuration;

public class GameIniWriterTests
{
    [Fact]
    public void Write_ThenParse_RoundTripsToEquivalentConfig()
    {
        var original = new GameConfig
        {
            Title = "Counter-Strike 2",
            AppId = 730,
            Process = "cs2.exe",
            Cover = "cover.jpg",
            Description = "FPS competitivo 5v5.",
            WatchTimeoutSeconds = 45,
            LaunchDelaySeconds = 3,
            GracefulShutdown = false,
        };

        var ini = GameIniWriter.Write(original);
        var result = GameIniParser.Parse(ini);

        Assert.True(result.Success);
        Assert.Equal(original, result.Config);
    }

    [Fact]
    public void Write_WithoutCover_OmitsCoverLine()
    {
        var config = new GameConfig { Title = "Portal", AppId = 400, Process = "portal.exe", Cover = null };

        var ini = GameIniWriter.Write(config);

        Assert.DoesNotContain("COVER=", ini);
    }

    [Fact]
    public void Write_WithoutDescription_OmitsDescriptionLine()
    {
        var config = new GameConfig { Title = "Portal", AppId = 400, Process = "portal.exe", Description = null };

        var ini = GameIniWriter.Write(config);

        Assert.DoesNotContain("DESCRIPTION=", ini);
    }

    [Fact]
    public void Write_LaunchDelayZero_RoundTripsCorrectly()
    {
        var config = new GameConfig { Title = "Portal", AppId = 400, Process = "portal.exe", LaunchDelaySeconds = 0 };

        var result = GameIniParser.Parse(GameIniWriter.Write(config));

        Assert.True(result.Success);
        Assert.Equal(0, result.Config!.LaunchDelaySeconds);
    }

    [Fact]
    public void Write_ThenParse_EpicConfig_RoundTrips()
    {
        var original = new GameConfig
        {
            Title = "INSIDE",
            Platform = GamePlatform.Epic,
            EpicNamespace = "13bb5776b9e1424d84ce42d9ba61c0ca",
            EpicItemId = "6fdb5feba66846cd8623a6d15ca68080",
            EpicAppName = "Marigold",
            Process = "INSIDE.exe",
        };

        var result = GameIniParser.Parse(GameIniWriter.Write(original));

        Assert.True(result.Success);
        Assert.Equal(original, result.Config);
    }

    [Fact]
    public void Write_ThenParse_GogConfig_RoundTrips()
    {
        var original = new GameConfig
        {
            Title = "Some Game",
            Platform = GamePlatform.Gog,
            GogGameId = "1234567890",
            Process = "game.exe",
        };

        var result = GameIniParser.Parse(GameIniWriter.Write(original));

        Assert.True(result.Success);
        Assert.Equal(original, result.Config);
    }

    [Fact]
    public void Write_EpicConfig_OmitsAppIdLine()
    {
        var config = new GameConfig
        {
            Title = "INSIDE",
            Platform = GamePlatform.Epic,
            EpicNamespace = "ns",
            EpicItemId = "item",
            EpicAppName = "app",
            Process = "INSIDE.exe",
        };

        var ini = GameIniWriter.Write(config);

        Assert.DoesNotContain("APPID=", ini);
    }
}
