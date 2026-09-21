using System.Globalization;

namespace FloppyGames.Core.Localization;

/// <summary>Os 5 idiomas suportados, com o nome de cada um na sua própria língua (para o seletor).</summary>
public static class SupportedLanguages
{
    public static readonly IReadOnlyList<(string Code, string NativeName)> All =
    [
        ("fr", "Français"),
        ("en", "English"),
        ("pt", "Português"),
        ("es", "Español"),
        ("it", "Italiano"),
    ];

    public const string Default = "fr";
}

/// <summary>
/// Aplica o idioma escolhido a todo o processo: <see cref="CultureInfo.CurrentUICulture"/> (para
/// a resolução de <see cref="Strings"/>) e <see cref="CultureInfo.CurrentCulture"/> (para datas e
/// números lerem-se ao estilo de cada idioma). Define também os valores por omissão de thread
/// (<see cref="CultureInfo.DefaultThreadCurrentCulture"/>/<see cref="CultureInfo.DefaultThreadCurrentUICulture"/>),
/// para que trabalho em segundo plano (ex.: <c>Task.Run</c>) herde o idioma certo, não só a UI thread.
/// </summary>
public static class LocalizationManager
{
    private static readonly IReadOnlyDictionary<string, string> RegionByLanguage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["fr"] = "fr-FR",
        ["en"] = "en-US",
        ["pt"] = "pt-PT",
        ["es"] = "es-ES",
        ["it"] = "it-IT",
    };

    public static void Apply(string? languageCode)
    {
        var culture = ResolveCulture(languageCode);

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    private static CultureInfo ResolveCulture(string? languageCode)
    {
        var code = string.IsNullOrWhiteSpace(languageCode) ? SupportedLanguages.Default : languageCode;
        return CultureInfo.GetCultureInfo(RegionByLanguage.GetValueOrDefault(code, RegionByLanguage[SupportedLanguages.Default]));
    }
}
