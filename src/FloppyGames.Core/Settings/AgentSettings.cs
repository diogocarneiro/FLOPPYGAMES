namespace FloppyGames.Core.Settings;

/// <summary>
/// Definições do Agent persistidas localmente, fora do Registo. Por agora só guarda a chave
/// opcional da Steam Web API, usada exclusivamente para mostrar o número de conquistas no
/// ecrã de arranque — sem ela, essa linha simplesmente não aparece.
/// </summary>
public sealed record AgentSettings
{
    public string? SteamWebApiKey { get; init; }
}
