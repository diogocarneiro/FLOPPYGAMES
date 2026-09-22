using System.Text.RegularExpressions;

namespace FloppyGames.Core.Nfc;

/// <summary>
/// Autentica e lê/escreve blocos de um cartão Mifare Classic através do cliente Proxmark3
/// (<c>hf mf rdbl</c>/<c>hf mf wrbl</c>/<c>hf mf csetblk</c>). Ao contrário do PC/SC, o Proxmark3
/// não tem uma sessão de autenticação persistente no dispositivo — cada comando `rdbl`/`wrbl` leva
/// sempre a chave consigo. Por isso <see cref="Authenticate"/> aqui faz uma leitura real (ao
/// primeiro bloco do setor) para confirmar a chave, e guarda-a em memória para os
/// <see cref="ReadBlock"/>/<see cref="WriteBlock"/> seguintes reutilizarem no mesmo comando.
///
/// Verificado contra hardware real nesta sessão (Proxmark3 RDV4, firmware Iceman, cartões Mifare
/// Classic 1K): leitura e escrita de blocos confirmadas byte a byte, incluindo pelo backdoor
/// mágico Gen1a. Também verificado nesta sessão: o acoplamento RF entre a antena do Proxmark3 e o
/// cartão é sensível ao posicionamento — a mesma operação, no mesmo cartão, falha por vezes e
/// funciona logo a seguir sem qualquer mudança de código. Por isso cada operação tenta algumas
/// vezes antes de desistir, em vez de reportar falha (ou, pior, ficar sem saber se algo foi
/// escrito) à primeira tentativa falhada.
/// </summary>
public sealed class Pm3MifareCardGateway : IMifareCardGateway
{
    private const int SmallSectorCount = 32;
    private const int SmallSectorBlockCount = 4;
    private const int LargeSectorBlockCount = 16;
    private const int MaxAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);
    private static readonly string[] FailureMarkers = ["[!!]", "Can't write block", "wupC1 error", "error="];

    private readonly string _pm3ExecutablePath;
    private readonly Dictionary<(string Reader, int Sector), string> _sectorKeys = new();

    public Pm3MifareCardGateway(string pm3ExecutablePath) => _pm3ExecutablePath = pm3ExecutablePath;

    public bool Authenticate(string readerName, int sector, byte[] keyA)
    {
        var keyHex = Convert.ToHexString(keyA);
        var firstBlock = AbsoluteFirstBlockOfSector(sector);

        var ok = WithRetries(() =>
        {
            string output;
            try
            {
                output = Pm3CommandRunner.Run(_pm3ExecutablePath, readerName, $"hf mf rdbl --blk {firstBlock} -k {keyHex}");
            }
            catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
            {
                return false;
            }

            return TryParseBlockLine(output, firstBlock, out _);
        });

        if (ok)
        {
            _sectorKeys[(readerName, sector)] = keyHex;
        }

        return ok;
    }

    public byte[] ReadBlock(string readerName, int absoluteBlock)
    {
        var keyHex = RequireKey(readerName, absoluteBlock);
        byte[] data = [];

        var ok = WithRetries(() =>
        {
            var output = Pm3CommandRunner.Run(_pm3ExecutablePath, readerName, $"hf mf rdbl --blk {absoluteBlock} -k {keyHex}");
            return TryParseBlockLine(output, absoluteBlock, out data);
        });

        if (!ok)
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

        var ok = WithRetries(() =>
        {
            var output = Pm3CommandRunner.Run(
                _pm3ExecutablePath, readerName, $"hf mf wrbl --blk {absoluteBlock} -k {keyHex} -d {dataHex}");
            return output.Contains("Write ( ok )", StringComparison.Ordinal);
        });

        if (!ok)
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

        return WithRetries(() =>
        {
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

            // "hf mf csetblk" não tem uma linha de sucesso explícita — só imprime "Writing block
            // number..." e, em caso de falha, uma ou mais linhas de erro. Verificado contra
            // hardware real: uma falha de acoplamento RF (não é exclusivo de cartões não-Gen1a)
            // dá "[#] wupC1 error" seguido de "[!!] Can't write block. error=-1" — por isso NUNCA
            // basta confirmar a ausência de uma frase específica (uma falha diferente, ex. de
            // ligação, passaria despercebida como sucesso). Exige as DUAS coisas: prova positiva
            // de que o comando arrancou ("Writing block number") e ausência de qualquer marcador
            // de erro do cliente (nível ERR é sempre prefixado com "[!!]").
            return output.Contains("Writing block number", StringComparison.Ordinal)
                && !FailureMarkers.Any(marker => output.Contains(marker, StringComparison.Ordinal));
        });
    }

    /// <summary>
    /// Tenta <paramref name="attempt"/> até <see cref="MaxAttempts"/> vezes, com uma pequena
    /// pausa entre tentativas — o acoplamento RF entre a antena e o cartão é sensível ao
    /// posicionamento físico, e uma falha isolada não significa que a operação seja impossível.
    /// </summary>
    private static bool WithRetries(Func<bool> attempt)
    {
        for (var i = 0; i < MaxAttempts; i++)
        {
            if (attempt())
            {
                return true;
            }

            if (i < MaxAttempts - 1)
            {
                Thread.Sleep(RetryDelay);
            }
        }

        return false;
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
