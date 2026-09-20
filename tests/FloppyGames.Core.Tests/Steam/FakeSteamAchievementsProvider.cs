using FloppyGames.Core.Steam;

namespace FloppyGames.Core.Tests.Steam;

internal sealed class FakeSteamAchievementsProvider(AchievementSummary? result) : ISteamAchievementsProvider
{
    public string? LastApiKey { get; private set; }

    public Task<AchievementSummary?> TryGetSummaryAsync(string apiKey, ulong steamId64, int appId, CancellationToken cancellationToken)
    {
        LastApiKey = apiKey;
        return Task.FromResult(result);
    }
}
