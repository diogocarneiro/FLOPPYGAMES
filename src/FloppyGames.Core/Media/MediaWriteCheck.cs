namespace FloppyGames.Core.Media;

public enum MediaWriteCheckStatus
{
    Ready,
    NeedsConfirmation,
    Blocked,
}

/// <summary>Resultado da validação antes de escrever um GAME.INI + capa para um suporte.</summary>
public sealed record MediaWriteCheck(MediaWriteCheckStatus Status, string? Message)
{
    public static MediaWriteCheck Ready() => new(MediaWriteCheckStatus.Ready, null);

    public static MediaWriteCheck NeedsConfirmation(string message) => new(MediaWriteCheckStatus.NeedsConfirmation, message);

    public static MediaWriteCheck Blocked(string message) => new(MediaWriteCheckStatus.Blocked, message);
}
