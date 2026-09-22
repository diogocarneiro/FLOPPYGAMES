namespace FloppyGames.Core.Nfc;

public enum NfcCardScanStatus
{
    Valid,
    UnsupportedCardType,
    AuthenticationFailed,
    CorruptOrEmptyData,
    InvalidGameIni,
    ReaderCommunicationFailure,
}
