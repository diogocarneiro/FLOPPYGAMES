using FloppyGames.Core.Steam;

namespace FloppyGames.Core.Tests.Steam;

internal sealed class FakeSteamPathProvider(string? path) : ISteamPathProvider
{
    public string? GetSteamInstallPath() => path;
}
