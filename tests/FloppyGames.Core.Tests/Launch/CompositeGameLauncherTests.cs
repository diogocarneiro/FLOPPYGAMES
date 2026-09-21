using FloppyGames.Core.Configuration;
using FloppyGames.Core.Launch;

namespace FloppyGames.Core.Tests.Launch;

public class CompositeGameLauncherTests
{
    private static GameConfig Config(GamePlatform platform) => new() { Title = "Game", Platform = platform, Process = "game.exe" };

    [Fact]
    public void Launch_DispatchesToTheLauncherRegisteredForThePlatform()
    {
        var steamLauncher = new FakeGameLauncher();
        var epicLauncher = new FakeGameLauncher();
        var composite = new CompositeGameLauncher(new Dictionary<GamePlatform, IGameLauncher>
        {
            [GamePlatform.Steam] = steamLauncher,
            [GamePlatform.Epic] = epicLauncher,
        });

        composite.Launch(Config(GamePlatform.Epic));

        Assert.Single(epicLauncher.LaunchedConfigs);
        Assert.Empty(steamLauncher.LaunchedConfigs);
    }

    [Fact]
    public void Launch_PlatformWithoutRegisteredLauncher_Throws()
    {
        var composite = new CompositeGameLauncher(new Dictionary<GamePlatform, IGameLauncher>
        {
            [GamePlatform.Steam] = new FakeGameLauncher(),
        });

        Assert.Throws<InvalidOperationException>(() => composite.Launch(Config(GamePlatform.Gog)));
    }
}
