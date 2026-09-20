using System.Globalization;

namespace FloppyGames.Core.Configuration;

/// <summary>
/// Analisa o conteúdo de um ficheiro GAME.INI e produz uma <see cref="GameConfig"/> validada.
/// Não há preenchimento silencioso de valores inválidos: qualquer campo malformado
/// (obrigatório ou opcional) resulta em falha da análise, com todos os erros reportados de uma vez.
/// </summary>
public static class GameIniParser
{
    private const string GameSection = "Game";
    private const string OptionsSection = "Options";

    public static GameIniParseResult Parse(string iniText)
    {
        ArgumentNullException.ThrowIfNull(iniText);

        var sections = ParseSections(iniText);
        var errors = new List<string>();

        sections.TryGetValue(GameSection, out var game);
        sections.TryGetValue(OptionsSection, out var options);

        var title = RequireString(game, "TITLE", errors);
        var process = RequireString(game, "PROCESS", errors);
        var appId = RequirePositiveInt(game, "APPID", errors);

        var cover = game?.GetValueOrDefault("COVER");

        var watchTimeoutSeconds = OptionalPositiveInt(options, "WatchTimeoutSeconds", 30, errors);
        var launchDelaySeconds = OptionalNonNegativeInt(options, "LaunchDelaySeconds", 2, errors);
        var gracefulShutdown = OptionalBool(options, "GracefulShutdown", true, errors);

        if (errors.Count > 0)
        {
            return GameIniParseResult.Fail(errors);
        }

        var config = new GameConfig
        {
            Title = title!,
            AppId = appId!.Value,
            Process = process!,
            Cover = string.IsNullOrWhiteSpace(cover) ? null : cover,
            WatchTimeoutSeconds = watchTimeoutSeconds,
            LaunchDelaySeconds = launchDelaySeconds,
            GracefulShutdown = gracefulShutdown,
        };

        return GameIniParseResult.Ok(config);
    }

    private static string? RequireString(IReadOnlyDictionary<string, string>? section, string key, List<string> errors)
    {
        var value = section?.GetValueOrDefault(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"Campo obrigatório em falta: [Game] {key}.");
            return null;
        }

        return value;
    }

    private static int? RequirePositiveInt(IReadOnlyDictionary<string, string>? section, string key, List<string> errors)
    {
        var raw = section?.GetValueOrDefault(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            errors.Add($"Campo obrigatório em falta: [Game] {key}.");
            return null;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            errors.Add($"[Game] {key} tem de ser um número inteiro positivo (valor recebido: '{raw}').");
            return null;
        }

        return parsed;
    }

    private static int OptionalPositiveInt(IReadOnlyDictionary<string, string>? section, string key, int defaultValue, List<string> errors)
    {
        var raw = section?.GetValueOrDefault(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            errors.Add($"[Options] {key} tem de ser um número inteiro positivo (valor recebido: '{raw}').");
            return defaultValue;
        }

        return parsed;
    }

    private static int OptionalNonNegativeInt(IReadOnlyDictionary<string, string>? section, string key, int defaultValue, List<string> errors)
    {
        var raw = section?.GetValueOrDefault(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        {
            errors.Add($"[Options] {key} tem de ser um número inteiro não negativo (valor recebido: '{raw}').");
            return defaultValue;
        }

        return parsed;
    }

    private static bool OptionalBool(IReadOnlyDictionary<string, string>? section, string key, bool defaultValue, List<string> errors)
    {
        var raw = section?.GetValueOrDefault(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!bool.TryParse(raw, out var parsed))
        {
            errors.Add($"[Options] {key} tem de ser 'true' ou 'false' (valor recebido: '{raw}').");
            return defaultValue;
        }

        return parsed;
    }

    private static Dictionary<string, Dictionary<string, string>> ParseSections(string iniText)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? currentSection = null;

        using var reader = new StringReader(iniText);
        string? rawLine;
        while ((rawLine = reader.ReadLine()) is not null)
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var sectionName = line[1..^1].Trim();
                if (!sections.TryGetValue(sectionName, out currentSection))
                {
                    currentSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    sections[sectionName] = currentSection;
                }

                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0 || currentSection is null)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            currentSection[key] = value;
        }

        return sections;
    }
}
