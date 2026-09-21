using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Platforms;

/// <summary>
/// Um jogo instalado localmente, seja qual for a plataforma — a forma unificada que o Label
/// Studio lista, para não ter de conhecer os detalhes de cada launcher.
/// </summary>
public sealed record DiscoveredGame(
    string Name,
    GamePlatform Platform,
    string InstallPath,
    long? InstalledSizeBytes = null,
    int? SteamAppId = null,
    string? EpicNamespace = null,
    string? EpicItemId = null,
    string? EpicAppName = null,
    string? GogGameId = null);
