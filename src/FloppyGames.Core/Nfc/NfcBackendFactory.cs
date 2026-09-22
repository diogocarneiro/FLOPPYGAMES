namespace FloppyGames.Core.Nfc;

/// <summary>Conjunto pronto a usar de deteção/leitura/escrita NFC, já composto a partir dos backends disponíveis.</summary>
public sealed record NfcBackend(
    INfcReaderDetector ReaderDetector,
    INfcCardPresenceProbe CardPresenceProbe,
    IMifareCardGateway CardGateway,
    IDisposable? Disposable);

/// <summary>
/// Compõe o backend PC/SC (sempre disponível) com o backend Proxmark3 (só se
/// <c>tools/proxmark3/proxmark3.exe</c> estiver presente na pasta de saída) — usado igualmente
/// pelo Agent e pelo Label Studio, para não duplicar esta decisão em dois sítios.
/// </summary>
public static class NfcBackendFactory
{
    public static NfcBackend Create()
    {
        var pcscGateway = new PcscMifareCardGateway();
        INfcReaderDetector pcscReaders = new PcscReaderDetector();
        INfcCardPresenceProbe pcscProbe = new PcscNfcCardPresenceProbe();

        var pm3ExecutablePath = Pm3ToolLocator.FindExecutablePath();
        if (pm3ExecutablePath is null)
        {
            return new NfcBackend(pcscReaders, pcscProbe, pcscGateway, pcscGateway);
        }

        var composite = new CompositeNfcBackend(
            pcscReaders, pcscProbe, pcscGateway,
            new Pm3ReaderDetector(),
            new Pm3NfcCardPresenceProbe(pm3ExecutablePath),
            new Pm3MifareCardGateway(pm3ExecutablePath));

        return new NfcBackend(composite, composite, composite, pcscGateway);
    }
}
