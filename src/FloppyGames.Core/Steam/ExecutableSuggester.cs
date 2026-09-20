namespace FloppyGames.Core.Steam;

/// <summary>
/// Sugere qual, de entre os .exe encontrados na pasta de instalação de um jogo, é provavelmente
/// o executável principal. É apenas uma sugestão — o utilizador confirma ou corrige no Label Studio.
/// Lógica pura (sem tocar em disco), para ser trivialmente testável.
/// </summary>
public static class ExecutableSuggester
{
    private static readonly string[] IgnoredNamePatterns =
    [
        "unins", "crashhandler", "crashpad", "crashreport", "vcredist", "dxsetup", "directx",
        "redist", "installer", "setup", "helper", "battleye", "easyanticheat", "updater",
    ];

    public static string? Suggest(IReadOnlyList<string> executableFileNames, string installDirectoryName)
    {
        if (executableFileNames.Count == 0)
        {
            return null;
        }

        var candidates = executableFileNames
            .Where(name => !IgnoredNamePatterns.Any(pattern => name.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = executableFileNames.ToList();
        }

        var normalizedDir = Normalize(installDirectoryName);
        var byNameMatch = candidates.FirstOrDefault(
            name => Normalize(Path.GetFileNameWithoutExtension(name)) == normalizedDir);

        if (byNameMatch is not null)
        {
            return byNameMatch;
        }

        // Sem correspondência de nome: o executável principal costuma ter um nome curto
        // (ex. "cs2.exe" em vez de "steam_appid_generator_x64.exe").
        return candidates.OrderBy(name => name.Length).First();
    }

    private static string Normalize(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
