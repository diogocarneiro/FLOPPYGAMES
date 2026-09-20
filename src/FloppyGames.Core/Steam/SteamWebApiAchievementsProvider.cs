using System.Text.Json;

namespace FloppyGames.Core.Steam;

/// <summary>
/// Lê o número de conquistas desbloqueadas via <c>ISteamUserStats/GetPlayerAchievements</c> —
/// a única fonte fiável para isto: localmente a Steam guarda conquistas como bits dentro de stats
/// inteiros, num cache binário não documentado e sem esquema estável entre jogos. Requer uma
/// chave grátis (steamcommunity.com/dev/apikey) configurada nas Definições; sem chave, com o
/// perfil privado, ou em qualquer falha de rede/parsing, devolve <c>null</c> — nunca inventa um número.
/// </summary>
public sealed class SteamWebApiAchievementsProvider : ISteamAchievementsProvider
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(8) };

    public async Task<AchievementSummary?> TryGetSummaryAsync(
        string apiKey, ulong steamId64, int appId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var url = "https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v0001/" +
                   $"?appid={appId}&key={Uri.EscapeDataString(apiKey)}&steamid={steamId64}";

        try
        {
            using var response = await HttpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("playerstats", out var playerStats)
                || !playerStats.TryGetProperty("success", out var successProperty)
                || successProperty.ValueKind != JsonValueKind.True
                || !playerStats.TryGetProperty("achievements", out var achievements)
                || achievements.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var total = 0;
            var unlocked = 0;

            foreach (var achievement in achievements.EnumerateArray())
            {
                total++;
                if (achievement.TryGetProperty("achieved", out var achievedProperty)
                    && achievedProperty.ValueKind == JsonValueKind.Number
                    && achievedProperty.GetInt32() != 0)
                {
                    unlocked++;
                }
            }

            return total == 0 ? null : new AchievementSummary(unlocked, total);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            return null;
        }
    }
}
