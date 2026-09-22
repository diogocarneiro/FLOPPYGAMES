namespace FloppyGames.Core.Settings;

/// <summary>
/// Definições do Agent persistidas localmente, fora do Registo: a chave opcional da Steam Web
/// API (só usada para mostrar o número de conquistas — sem ela, essa linha simplesmente não
/// aparece), se o som do motor de disquete deve tocar quando uma disquete física é detetada, se
/// o varrimento CRT animado deve aparecer no ecrã de arranque, o idioma da app (partilhado pelo
/// Agent e pelo Label Studio — este último não tem seletor próprio, só lê este valor), e quais
/// plataformas aparecem como opção no seletor do Label Studio (Steam ligada por omissão; Epic e
/// GOG desligadas — não afeta o Agent, que continua a lançar qualquer GAME.INI já criado
/// independentemente disto), e se a deteção de cartões NFC/RFID está ativa (desligada por
/// omissão — capacidade opcional que depende de um leitor PC/SC físico, nunca verificada com
/// hardware real; máquinas sem leitor e sem este opt-in nunca tocam no subsistema PC/SC).
/// </summary>
public sealed record AgentSettings
{
    public string? SteamWebApiKey { get; init; }

    public bool PlayFloppySound { get; init; } = true;

    public bool CrtEffectEnabled { get; init; } = true;

    public string Language { get; init; } = Localization.SupportedLanguages.Default;

    public bool SteamEnabled { get; init; } = true;

    public bool EpicEnabled { get; init; }

    public bool GogEnabled { get; init; }

    public bool NfcEnabled { get; init; }
}
