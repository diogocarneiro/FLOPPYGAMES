using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Nfc;

/// <summary>Resultado da leitura de um cartão NFC recém-detetado — espelha <c>GameMediaScanResult</c>.</summary>
public sealed record NfcCardScanResult
{
    public required string Uid { get; init; }

    public required NfcCardScanStatus Status { get; init; }

    public GameConfig? Config { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static NfcCardScanResult Valid(string uid, GameConfig config) =>
        new() { Uid = uid, Status = NfcCardScanStatus.Valid, Config = config };

    public static NfcCardScanResult UnsupportedCardType(string uid) =>
        new() { Uid = uid, Status = NfcCardScanStatus.UnsupportedCardType };

    public static NfcCardScanResult AuthenticationFailed(string uid, int sector) =>
        new() { Uid = uid, Status = NfcCardScanStatus.AuthenticationFailed, Errors = [$"Setor {sector}"] };

    public static NfcCardScanResult CorruptOrEmptyData(string uid) =>
        new() { Uid = uid, Status = NfcCardScanStatus.CorruptOrEmptyData };

    /// <summary>Cartão em branco (ou formatado) — comprimento declarado zero, nenhum jogo gravado.</summary>
    public static NfcCardScanResult Empty(string uid) =>
        new() { Uid = uid, Status = NfcCardScanStatus.Empty };

    public static NfcCardScanResult InvalidGameIni(string uid, IReadOnlyList<string> errors) =>
        new() { Uid = uid, Status = NfcCardScanStatus.InvalidGameIni, Errors = errors };

    public static NfcCardScanResult ReaderCommunicationFailure(string uid, string error) =>
        new() { Uid = uid, Status = NfcCardScanStatus.ReaderCommunicationFailure, Errors = [error] };
}
