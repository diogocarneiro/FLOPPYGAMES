namespace FloppyGames.Core.Configuration;

/// <summary>
/// Configuração de um jogo lida a partir de um ficheiro GAME.INI, na raiz de um suporte amovível.
/// </summary>
public sealed record GameConfig
{
    public required string Title { get; init; }

    public required int AppId { get; init; }

    public required string Process { get; init; }

    public string? Cover { get; init; }

    public string? Description { get; init; }

    public int WatchTimeoutSeconds { get; init; } = 30;

    public int LaunchDelaySeconds { get; init; } = 2;

    public bool GracefulShutdown { get; init; } = true;
}
