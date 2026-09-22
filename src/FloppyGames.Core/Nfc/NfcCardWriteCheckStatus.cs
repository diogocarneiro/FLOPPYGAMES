namespace FloppyGames.Core.Nfc;

public enum NfcCardWriteCheckStatus
{
    Ready,
    NeedsConfirmation,
    Blocked,

    /// <summary>
    /// A chave de fábrica não autenticou o setor — o cartão pode já estar a ser usado noutro
    /// sistema. Distinto de <see cref="Blocked"/> porque, se o cartão for um clone "magic"
    /// (Gen1a/Gen2), ainda é possível escrever à mesma via <see cref="NfcCardConfigWriter.WriteMagic"/>.
    /// </summary>
    AuthenticationFailed,
}
