using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Platforms;

/// <summary>
/// Descarrega a capa vertical de um jogo, seja qual for a plataforma, sem autenticação nem API
/// key: Steam pelo CDN público (<c>library_600x900</c>), Epic pelo URL guardado na cache de
/// catálogo local da Epic Games Launcher, GOG pelo URL guardado na base de dados do GOG Galaxy
/// (ver <see cref="EpicGameLibraryScanner.TryResolveCoverUrl"/> e
/// <see cref="GogGameLibraryScanner.TryResolveCoverUrl"/>).
/// </summary>
public sealed class GameCoverArtProvider
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly EpicGameLibraryScanner _epicScanner;
    private readonly GogGameLibraryScanner _gogScanner;

    public GameCoverArtProvider(EpicGameLibraryScanner epicScanner, GogGameLibraryScanner gogScanner)
    {
        _epicScanner = epicScanner;
        _gogScanner = gogScanner;
    }

    public Task<byte[]?> TryDownloadCoverAsync(DiscoveredGame game, CancellationToken cancellationToken) =>
        TryDownloadCoverAsync(game.Platform, game.SteamAppId, game.EpicItemId, game.GogGameId, cancellationToken);

    public Task<byte[]?> TryDownloadCoverAsync(GameConfig config, CancellationToken cancellationToken) =>
        TryDownloadCoverAsync(config.Platform, config.AppId, config.EpicItemId, config.GogGameId, cancellationToken);

    private async Task<byte[]?> TryDownloadCoverAsync(
        GamePlatform platform, int? steamAppId, string? epicItemId, string? gogGameId, CancellationToken cancellationToken)
    {
        try
        {
            // Resolver o URL da Epic/GOG lê ficheiros locais (a cache da Epic tem ~5 MB) — nunca na
            // thread de quem chama, que costuma ser a de UI.
            var url = await Task.Run(() => ResolveCoverUrl(platform, steamAppId, epicItemId, gogGameId), cancellationToken);
            if (url is null)
            {
                return null;
            }

            using var response = await HttpClient.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadAsByteArrayAsync(cancellationToken)
                : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    private string? ResolveCoverUrl(GamePlatform platform, int? steamAppId, string? epicItemId, string? gogGameId) => platform switch
    {
        GamePlatform.Steam when steamAppId is { } appId => $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg",
        GamePlatform.Epic when !string.IsNullOrWhiteSpace(epicItemId) => _epicScanner.TryResolveCoverUrl(epicItemId),
        GamePlatform.Gog when !string.IsNullOrWhiteSpace(gogGameId) => _gogScanner.TryResolveCoverUrl(gogGameId),
        _ => null,
    };
}
