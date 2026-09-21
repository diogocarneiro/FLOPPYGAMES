namespace FloppyGames.Core.Launch;

/// <summary>
/// Porquê um lançamento falhou. Um enum estável e agnóstico de idioma em vez de uma string em
/// português — quem mostra isto ao utilizador (a UI do Agent) é que decide o texto, na língua
/// escolhida.
/// </summary>
public enum GameLaunchFailureReason
{
    Timeout,
    MediaRemovedDuringLaunch,
    UnexpectedError,
}
