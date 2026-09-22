using System.Text.RegularExpressions;

namespace FloppyGames.Core.Nfc;

/// <summary>
/// Autentica e lê/escreve blocos de um cartão Mifare Classic através do cliente Proxmark3
/// (<c>hf mf rdbl</c>/<c>hf mf wrbl</c>). Ao contrário do PC/SC, o Proxmark3 não tem uma sessão de
/// autenticação persistente no dispositivo — cada comando `rdbl`/`wrbl` leva sempre a chave consigo.
/// Por isso <see cref="Authenticate"/> aqui faz uma leitura real (ao primeiro bloco do setor) para
/// confirmar a chave, e guarda-a em memória para os <see cref="ReadBlock"/>/<see cref="WriteBlock"/>
/// seguintes reutilizarem no mesmo comando.
///
/// Verificado contra hardware real nesta sessão (Proxmark3 RDV4, firmware Iceman, cartão Mifare
/// Classic 1K Gen1a): leitura e escrita de blocos confirmadas byte a byte.
/// </summary>
public sealed class Pm3MifareCardGateway : IMifareCardGateway
{
    private const int SmallSectorCount = 32;
    private const int SmallSectorBlockCount = 4;
    private const int LargeSectorBlockCount = 16;

    private readonly string _pm3ExecutablePath;
    private readonly Dictionary<(string Reader, int Sector), string> _sectorKeys = new();

    public Pm3MifareCardGateway(string pm3ExecutablePath) => _pm3ExecutablePath = pm3ExecutablePath;

    public bool Authenticate(string readerName, int sector, byte[] keyA)
    {
        var keyHex = Convert.ToHexString(keyA);
        var firstBlock = AbsoluteFirstBlockOfSector(sector);

        string output;
        try
        {
            output = Pm3CommandRunner.Run(_pm3ExecutablePath, readerName, $"hf mf rdbl --blk {firstBlock} -k {keyHex}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
        {
            return false;
        }

        if (!TryParseBlockLine(output, firstBlock, out _))
        {
            return false;
        }

        _sectorKeys[(readerName, sector)] = keyHex;
        return true;
    }

    public byte[] ReadBlock(string readerName, int absoluteBlock)
    {
        var keyHex = RequireKey(readerName, absoluteBlock);
        var output = Pm3CommandRunner.Run(_pm3ExecutablePath, readerName, $"hf mf rdbl --blk {absoluteBlock} -k {keyHex}");

        if (!TryParseBlockLine(output, absoluteBlock, out var data))
        {
            throw new InvalidOperationException($"Falha ao ler o bloco {absoluteBlock} via Proxmark3 ({readerName}).");
        }

        return data;
    }

    public void WriteBlock(string readerName, int absoluteBlock, byte[] data)
    {
        if (data.Length != 16)
        {
            throw new ArgumentException("Um bloco Mifare Classic tem sempre 16 bytes.", nameof(data));
        }

        var keyHex = RequireKey(readerName, absoluteBlock);
        var dataHex = Convert.ToHexString(data);
        var output = Pm3CommandRunner.Run(
            _pm3ExecutablePath, readerName, $"hf mf wrbl --blk {absoluteBlock} -k {keyHex} -d {dataHex}");

        if (!output.Contains("Write ( ok )", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Falha ao escrever o bloco {absoluteBlock} via Proxmark3 ({readerName}).");
        }
    }

    public bool TryMagicWriteBlock(string readerName, int absoluteBlock, byte[] data)
    {
        if (data.Length != 16)
        {
            throw new ArgumentException("Um bloco Mifare Classic tem sempre 16 bytes.", nameof(data));
        }

        var dataHex = Convert.ToHexString(data);
        string output;
        try
        {
            output = Pm3CommandRunner.Run(
                _pm3ExecutablePath, readerName, $"hf mf csetblk --blk {absoluteBlock} -d {dataHex}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
        {
            return false;
        }

        // "hf mf csetblk" só imprime algo além da linha "Writing block number..." quando falha
        // (cartão não respondeu ao modo mágico Gen1a, ou o backdoor não é suportado).
        return !output.Contains("Can't write block", StringComparison.Ordinal);
    }

    private string RequireKey(string readerName, int absoluteBlock)
    {
        var sector = SectorOf(absoluteBlock);
        if (!_sectorKeys.TryGetValue((readerName, sector), out var keyHex))
        {
            throw new InvalidOperationException(
                $"Bloco {absoluteBlock} (setor {sector}) acedido antes de autenticar via Proxmark3.");
        }

        return keyHex;
    }

    private static int AbsoluteFirstBlockOfSector(int sector) =>
        sector < SmallSectorCount ? sector * SmallSectorBlockCount : 128 + (sector - SmallSectorCount) * LargeSectorBlockCount;

    private static int SectorOf(int absoluteBlock) =>
        absoluteBlock < SmallSectorCount * SmallSectorBlockCount
            ? absoluteBlock / SmallSectorBlockCount
            : SmallSectorCount + (absoluteBlock - SmallSectorCount * SmallSectorBlockCount) / LargeSectorBlockCount;

    private static bool TryParseBlockLine(string output, int blockNumber, out byte[] data)
    {
        var match = BlockLinePattern(blockNumber).Match(output);
        if (!match.Success)
        {
            data = [];
            return false;
        }

        var hex = match.Groups[1].Value.Replace(" ", string.Empty, StringComparison.Ordinal);
        data = Convert.FromHexString(hex);
        return true;
    }

    private static Regex BlockLinePattern(int blockNumber) =>
        new(@$"(?<![0-9]){blockNumber}\s*\|\s*((?:[0-9A-Fa-f]{{2}}\s+){{15}}[0-9A-Fa-f]{{2}})\s*\|");
}
