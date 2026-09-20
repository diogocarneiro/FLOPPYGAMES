namespace FloppyGames.Core.Configuration;

/// <summary>
/// Resultado da análise de um ficheiro GAME.INI: ou uma <see cref="GameConfig"/> válida,
/// ou a lista completa de erros de validação encontrados.
/// </summary>
public sealed class GameIniParseResult
{
    private GameIniParseResult(GameConfig? config, IReadOnlyList<string> errors)
    {
        Config = config;
        Errors = errors;
    }

    public bool Success => Config is not null;

    public GameConfig? Config { get; }

    public IReadOnlyList<string> Errors { get; }

    public static GameIniParseResult Ok(GameConfig config) => new(config, Array.Empty<string>());

    public static GameIniParseResult Fail(IReadOnlyList<string> errors) => new(null, errors);
}
