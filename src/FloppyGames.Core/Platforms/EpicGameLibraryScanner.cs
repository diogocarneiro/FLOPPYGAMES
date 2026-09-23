using System.Text.Json;
using FloppyGames.Core.Configuration;
using FloppyGames.Core.Steam;

namespace FloppyGames.Core.Platforms;

/// <summary>
/// Lê os manifestos da Epic Games Launcher
/// (<c>%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item</c>, JSON simples, um ficheiro
/// por jogo instalado — formato confirmado contra um manifesto real desta máquina). Ao contrário
/// da Steam, o caminho é fixo: não precisa de registo para o encontrar.
/// </summary>
public sealed class EpicGameLibraryScanner
{
    private static readonly string DefaultManifestsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Epic", "EpicGamesLauncher", "Data", "Manifests");

    private static readonly string DefaultCatalogCachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Epic", "EpicGamesLauncher", "Data", "Catalog", "catcache.bin");

    /// <summary>
    /// A capa vertical original chega a ter 1200x1600 e 2.4 MB em PNG — mais do que cabe numa
    /// disquete. O CDN da Epic redimensiona e recomprime a pedido (verificado ao vivo: o mesmo
    /// ficheiro passa a um JPEG de ~87 KB), no mesmo formato de 600 px de largura da capa da Steam.
    /// </summary>
    private const string CoverResizeQuery = "?h=800&resize=1&w=600&quality=medium";

    private readonly ISteamFileSystem _fileSystem;
    private readonly string _manifestsDirectory;
    private readonly string _catalogCachePath;

    public EpicGameLibraryScanner(ISteamFileSystem fileSystem, string? manifestsDirectory = null, string? catalogCachePath = null)
    {
        _fileSystem = fileSystem;
        _manifestsDirectory = manifestsDirectory ?? DefaultManifestsDirectory;
        _catalogCachePath = catalogCachePath ?? DefaultCatalogCachePath;
    }

    public IReadOnlyList<DiscoveredGame> ScanInstalledGames()
    {
        if (!_fileSystem.DirectoryExists(_manifestsDirectory))
        {
            return [];
        }

        var games = new List<DiscoveredGame>();

        foreach (var manifestPath in _fileSystem.EnumerateFiles(_manifestsDirectory, "*.item"))
        {
            var game = TryReadManifest(manifestPath);
            if (game is not null)
            {
                games.Add(game);
            }
        }

        return games.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private DiscoveredGame? TryReadManifest(string manifestPath)
    {
        try
        {
            using var document = JsonDocument.Parse(_fileSystem.ReadAllText(manifestPath));
            var root = document.RootElement;

            var name = GetString(root, "DisplayName");
            var installLocation = GetString(root, "InstallLocation");
            var epicNamespace = GetString(root, "CatalogNamespace");
            var epicItemId = GetString(root, "CatalogItemId");
            var epicAppName = GetString(root, "AppName");

            if (name is null || installLocation is null || epicNamespace is null || epicItemId is null || epicAppName is null)
            {
                return null;
            }

            var installSizeBytes = root.TryGetProperty("InstallSize", out var sizeProperty)
                && sizeProperty.ValueKind == JsonValueKind.Number
                ? sizeProperty.GetInt64()
                : (long?)null;

            return new DiscoveredGame(
                name, GamePlatform.Epic, installLocation, installSizeBytes,
                EpicNamespace: epicNamespace, EpicItemId: epicItemId, EpicAppName: epicAppName);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// O URL da capa vertical (<c>DieselGameBoxTall</c>) de um jogo, a partir da cache de catálogo
    /// que a própria Epic Games Launcher mantém localmente (<c>catcache.bin</c>: JSON codificado em
    /// Base64, uma lista de itens de catálogo com <c>id</c> e <c>keyImages</c> — formato confirmado
    /// contra a cache real desta máquina, onde os três jogos Epic instalados tinham esta imagem).
    /// </summary>
    public string? TryResolveCoverUrl(string epicItemId)
    {
        if (!_fileSystem.FileExists(_catalogCachePath))
        {
            return null;
        }

        try
        {
            var json = Convert.FromBase64String(_fileSystem.ReadAllText(_catalogCachePath).Trim());
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (GetString(item, "id") != epicItemId
                    || !item.TryGetProperty("keyImages", out var images)
                    || images.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var url = images.EnumerateArray()
                    .Where(image => GetString(image, "type") == "DieselGameBoxTall")
                    .Select(image => GetString(image, "url"))
                    .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));

                return url is null ? null : url + CoverResizeQuery;
            }

            return null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
