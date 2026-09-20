using FloppyGames.Core.Launch;

namespace FloppyGames.Core.Tests.Launch;

internal sealed class FakeSteamLauncher : ISteamLauncher
{
    public List<int> LaunchedAppIds { get; } = new();

    public void Launch(int appId) => LaunchedAppIds.Add(appId);
}
