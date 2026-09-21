namespace FloppyGames.Core.Settings;

/// <summary>
/// Definições do Agent persistidas localmente, fora do Registo: a chave opcional da Steam Web
/// API (só usada para mostrar o número de conquistas — sem ela, essa linha simplesmente não
/// aparece), se o som do motor de disquete deve tocar quando uma disquete física é detetada, se
/// o varrimento CRT animado deve aparecer no ecrã de arranque, e o idioma da app (partilhado
/// pelo Agent e pelo Label Studio — este último não tem seletor próprio, só lê este valor).
/// </summary>
public sealed record AgentSettings
{
    public string? SteamWebApiKey { get; init; }

    public bool PlayFloppySound { get; init; } = true;

    public bool CrtEffectEnabled { get; init; } = true;

    public string Language { get; init; } = Localization.SupportedLanguages.Default;
}
