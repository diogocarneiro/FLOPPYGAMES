using System.Text.RegularExpressions;

namespace FloppyGames.Core.Nfc;

/// <summary>
/// Une o backend PC/SC (leitores genéricos como o ACR122U) e o backend Proxmark3 num só conjunto
/// de interfaces, para o resto do FloppyGames nunca precisar de saber qual dos dois está a falar
/// com o hardware. O nome do leitor decide o encaminhamento: um Proxmark3 é sempre identificado
/// pela sua porta COM (ex. "COM7"), o que nunca corresponde ao formato descritivo que o PC/SC
/// devolve para os seus leitores (ex. "ACS ACR122U PICC Interface 0") — não é preciso nenhum
/// estado partilhado entre <see cref="ListConnectedReaders"/> e as chamadas seguintes.
/// </summary>
public sealed partial class CompositeNfcBackend : INfcReaderDetector, INfcCardPresenceProbe, IMifareCardGateway
{
    private readonly INfcReaderDetector _pcscReaders;
    private readonly INfcCardPresenceProbe _pcscProbe;
    private readonly IMifareCardGateway _pcscGateway;
    private readonly INfcReaderDetector _pm3Readers;
    private readonly INfcCardPresenceProbe _pm3Probe;
    private readonly IMifareCardGateway _pm3Gateway;

    public CompositeNfcBackend(
        INfcReaderDetector pcscReaders, INfcCardPresenceProbe pcscProbe, IMifareCardGateway pcscGateway,
        INfcReaderDetector pm3Readers, INfcCardPresenceProbe pm3Probe, IMifareCardGateway pm3Gateway)
    {
        _pcscReaders = pcscReaders;
        _pcscProbe = pcscProbe;
        _pcscGateway = pcscGateway;
        _pm3Readers = pm3Readers;
        _pm3Probe = pm3Probe;
        _pm3Gateway = pm3Gateway;
    }

    public IReadOnlyList<string> ListConnectedReaders()
    {
        var readers = new List<string>();
        readers.AddRange(_pcscReaders.ListConnectedReaders());
        readers.AddRange(_pm3Readers.ListConnectedReaders());
        return readers;
    }

    public bool TryGetPresentCard(string readerName, out string? uid, out MifareCardType cardType) =>
        IsProxmark3Reader(readerName)
            ? _pm3Probe.TryGetPresentCard(readerName, out uid, out cardType)
            : _pcscProbe.TryGetPresentCard(readerName, out uid, out cardType);

    public bool Authenticate(string readerName, int sector, byte[] keyA) =>
        IsProxmark3Reader(readerName)
            ? _pm3Gateway.Authenticate(readerName, sector, keyA)
            : _pcscGateway.Authenticate(readerName, sector, keyA);

    public byte[] ReadBlock(string readerName, int absoluteBlock) =>
        IsProxmark3Reader(readerName)
            ? _pm3Gateway.ReadBlock(readerName, absoluteBlock)
            : _pcscGateway.ReadBlock(readerName, absoluteBlock);

    public void WriteBlock(string readerName, int absoluteBlock, byte[] data)
    {
        if (IsProxmark3Reader(readerName))
        {
            _pm3Gateway.WriteBlock(readerName, absoluteBlock, data);
        }
        else
        {
            _pcscGateway.WriteBlock(readerName, absoluteBlock, data);
        }
    }

    public bool TryMagicWriteBlock(string readerName, int absoluteBlock, byte[] data) =>
        IsProxmark3Reader(readerName)
            ? _pm3Gateway.TryMagicWriteBlock(readerName, absoluteBlock, data)
            : _pcscGateway.TryMagicWriteBlock(readerName, absoluteBlock, data);

    private static bool IsProxmark3Reader(string readerName) => ComPortNamePattern().IsMatch(readerName);

    [GeneratedRegex(@"^COM\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex ComPortNamePattern();
}
