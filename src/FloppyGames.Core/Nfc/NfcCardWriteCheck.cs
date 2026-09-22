namespace FloppyGames.Core.Nfc;

/// <summary>Resultado da validação antes de gravar um GAME.INI num cartão — espelha <c>MediaWriteCheck</c>.</summary>
public sealed record NfcCardWriteCheck(NfcCardWriteCheckStatus Status, string? Message)
{
    public static NfcCardWriteCheck Ready() => new(NfcCardWriteCheckStatus.Ready, null);

    public static NfcCardWriteCheck NeedsConfirmation(string message) => new(NfcCardWriteCheckStatus.NeedsConfirmation, message);

    public static NfcCardWriteCheck Blocked(string message) => new(NfcCardWriteCheckStatus.Blocked, message);
}
