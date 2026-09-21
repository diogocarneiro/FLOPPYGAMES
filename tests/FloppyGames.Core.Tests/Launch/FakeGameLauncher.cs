using FloppyGames.Core.Configuration;
using FloppyGames.Core.Launch;

namespace FloppyGames.Core.Tests.Launch;

internal sealed class FakeGameLauncher : IGameLauncher
{
    public List<GameConfig> LaunchedConfigs { get; } = new();

    public void Launch(GameConfig config) => LaunchedConfigs.Add(config);
}
