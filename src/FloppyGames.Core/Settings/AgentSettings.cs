namespace FloppyGames.Core.Settings;

/// <summary>
/// Definições do Agent persistidas localmente, fora do Registo: a chave opcional da Steam Web
/// API (só usada para mostrar o número de conquistas — sem ela, essa linha simplesmente não
/// aparece), se o som do motor de disquete deve tocar quando uma disquete física é detetada, e
/// se o varrimento CRT animado deve aparecer no ecrã de arranque.
/// </summary>
public sealed record AgentSettings
{
    public string? SteamWebApiKey { get; init; }

    public bool PlayFloppySound { get; init; } = true;

    public bool CrtEffectEnabled { get; init; } = true;
}
