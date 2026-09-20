namespace FloppyGames.Core.Steam;

/// <summary>Obtém o resumo de conquistas (desbloqueadas/total) de um jogo para um utilizador Steam.</summary>
public interface ISteamAchievementsProvider
{
    public Task<AchievementSummary?> TryGetSummaryAsync(string apiKey, ulong steamId64, int appId, CancellationToken cancellationToken);
}
