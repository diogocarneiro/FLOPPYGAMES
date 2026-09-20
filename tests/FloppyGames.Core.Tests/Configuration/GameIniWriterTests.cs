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
    public void Write_LaunchDelayZero_RoundTripsCorrectly()
    {
        var config = new GameConfig { Title = "Portal", AppId = 400, Process = "portal.exe", LaunchDelaySeconds = 0 };

        var result = GameIniParser.Parse(GameIniWriter.Write(config));

        Assert.True(result.Success);
        Assert.Equal(0, result.Config!.LaunchDelaySeconds);
    }
}
