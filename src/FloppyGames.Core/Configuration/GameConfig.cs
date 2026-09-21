namespace FloppyGames.Core.Configuration;

/// <summary>
/// Configuração de um jogo lida a partir de um ficheiro GAME.INI, na raiz de um suporte amovível.
/// </summary>
public sealed record GameConfig
{
    public required string Title { get; init; }

    public GamePlatform Platform { get; init; } = GamePlatform.Steam;

    /// <summary>AppID da Steam — só relevante quando <see cref="Platform"/> é <see cref="GamePlatform.Steam"/>.</summary>
    public int? AppId { get; init; }

    public string? EpicNamespace { get; init; }

    public string? EpicItemId { get; init; }

    public string? EpicAppName { get; init; }

    /// <summary>ID interno do jogo na GOG (nome da subchave em <c>HKLM\...\GOG.com\Games</c>).</summary>
    public string? GogGameId { get; init; }

    public required string Process { get; init; }

    public string? Cover { get; init; }

    public string? Description { get; init; }

    public int WatchTimeoutSeconds { get; init; } = 30;

    public int LaunchDelaySeconds { get; init; } = 2;

    public bool GracefulShutdown { get; init; } = true;
}
