using System.Text.Json;
using System.Text.Json.Serialization;
using FloppyGames.Core.Configuration;
using FloppyGames.Core.Platforms;

namespace FloppyGames.Core.Catalog;

/// <summary>
/// Lê e consulta o catálogo partilhável local (<c>catalog/catalog.json</c>, versionado no
/// repositório junto com <c>samples/</c> — não é obtido por rede). Falha graciosamente (catálogo
/// vazio) se o ficheiro não existir ou estiver malformado, nunca lança.
/// </summary>
public sealed class GameCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _catalogPath;
    private readonly string _coversDirectory;
    private IReadOnlyList<CatalogEntry>? _entries;

    public GameCatalog(string? catalogPath = null)
    {
        _catalogPath = catalogPath ?? Path.Combine(AppContext.BaseDirectory, "Catalog", "catalog.json");
        _coversDirectory = Path.Combine(Path.GetDirectoryName(_catalogPath) ?? ".", "covers");
    }

    public IReadOnlyList<CatalogEntry> Entries => _entries ??= Load();

    /// <summary>Procura uma entrada correspondente ao jogo, pelo identificador próprio da sua plataforma.</summary>
    public CatalogEntry? TryFind(DiscoveredGame game) => game.Platform switch
    {
        GamePlatform.Steam => Entries.FirstOrDefault(e => e.Platform == GamePlatform.Steam && e.SteamAppId == game.SteamAppId),
        GamePlatform.Epic => Entries.FirstOrDefault(e => e.Platform == GamePlatform.Epic && e.EpicItemId == game.EpicItemId),
        GamePlatform.Gog => Entries.FirstOrDefault(e => e.Platform == GamePlatform.Gog && e.GogGameId == game.GogGameId),
        _ => null,
    };

    /// <summary>Caminho absoluto da imagem de capa da entrada, ou <c>null</c> se não tiver nenhuma associada.</summary>
    public string? ResolveCoverPath(CatalogEntry entry) =>
        string.IsNullOrWhiteSpace(entry.Cover) ? null : Path.Combine(_coversDirectory, entry.Cover);

    private IReadOnlyList<CatalogEntry> Load()
    {
        try
        {
            if (!File.Exists(_catalogPath))
            {
                return [];
            }

            var json = File.ReadAllText(_catalogPath);
            return JsonSerializer.Deserialize<List<CatalogEntry>>(json, SerializerOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }
}
