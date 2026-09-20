namespace FloppyGames.Core.Steam;

/// <summary>
/// Descarrega a capa vertical ("library_600x900") do CDN público da Steam — o mesmo formato
/// de imagem já usado na Steam Library, e o mais parecido com a proporção de uma etiqueta de disquete.
/// Não precisa de autenticação nem de API key.
/// </summary>
public sealed class SteamCdnCoverArtProvider : ICoverArtProvider
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<byte[]?> TryDownloadCoverAsync(int appId, CancellationToken cancellationToken)
    {
        var url = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg";

        try
        {
            using var response = await HttpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}
