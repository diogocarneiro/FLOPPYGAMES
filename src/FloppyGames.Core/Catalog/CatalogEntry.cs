using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Catalog;

/// <summary>
/// Uma entrada do catálogo partilhável (<c>catalog/catalog.json</c>, versionado no próprio
/// repositório — sem rede envolvida): o processo verificado a vigiar e uma descrição já traduzida
/// nos 5 idiomas suportados, para não obrigar cada utilizador a preencher isto à mão outra vez
/// para um jogo que já foi catalogado. <see cref="Cover"/> é opcional — o nome de um ficheiro de
/// imagem em <c>catalog/covers/</c>, só usado quando presente (nunca inventado).
/// </summary>
public sealed record CatalogEntry
{
    public GamePlatform Platform { get; init; }

    public int? SteamAppId { get; init; }

    public string? EpicNamespace { get; init; }

    public string? EpicItemId { get; init; }

    public string? EpicAppName { get; init; }

    public string? GogGameId { get; init; }

    public required string Title { get; init; }

    public required string Process { get; init; }

    /// <summary>Chave = código de idioma de 2 letras ("fr", "en", "pt", "es", "it").</summary>
    public Dictionary<string, string> Description { get; init; } = [];

    public string? Cover { get; init; }
}
