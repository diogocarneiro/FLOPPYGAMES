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
///
/// As operações <c>ReadBlocks</c>/<c>WriteBlocks</c>/<c>TryMagicWriteBlocks</c> agrupam vários
/// comandos `hf mf ...` numa só invocação do <c>proxmark3.exe</c> (separados por `;`, sintaxe já
/// suportada pelo cliente) — medido ao vivo: ~2.3s para UM bloco isolado (dominado pelo arranque
/// do processo e ligação USB-CDC, não pela transação RF em si) contra ~1.2s para TRÊS blocos
/// combinados numa só invocação. Sem isto, gravar um cartão inteiro (47 blocos, desde que
/// <see cref="NfcCardConfigWriter"/> passou a limpar sempre o cartão todo) chegava a demorar ~3
/// minutos.
/// </summary>
public sealed class Pm3MifareCardGateway : IMifareCardGateway
{
    private const int SmallSectorCount = 32;
    private const int SmallSectorBlockCount = 4;
    private const int LargeSectorBlockCount = 16;
    private const int MaxAttempts = 3;

    /// <summary>
    /// Blocos por invocação em lote — grande o suficiente para reduzir a maioria das chamadas de
    /// processo, pequeno o suficiente para que uma falha de RF isolada num bloco não obrigue a
    /// reenviar dezenas de blocos já bem-sucedidos (o lote inteiro repete-se em caso de falha,
    /// ver <see cref="WithRetries"/>).
    /// </summary>
    private const int BatchSize = 10;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);
    private static readonly string[] FailureMarkers = ["[!!]", "Can't write block", "wupC1 error", "error="];

    /// <summary>O que o cliente imprime quando o cartão rejeita a chave (verificado ao vivo com `hf mf rdbl`).</summary>
    private const string AuthErrorMarker = "Auth error";

    private const string CommandEchoPrefix = "pm3 --> ";

    private readonly string _pm3ExecutablePath;
    private readonly Dictionary<(string Reader, int Sector), string> _sectorKeys = new();

    public Pm3MifareCardGateway(string pm3ExecutablePath) => _pm3ExecutablePath = pm3ExecutablePath;

    public bool Authenticate(string readerName, int sector, byte[] keyA) =>
        AuthenticateSectors(readerName, [sector], keyA).Contains(sector);

    /// <summary>
    /// Cada setor acaba num de três estados: autenticado (o bloco foi lido), rejeitado (o cartão
    /// respondeu <c>Auth error</c> — chave errada, resposta definitiva, verificado ao vivo) ou sem
    /// resposta clara (falha de RF/ligação). Só este último é repetido. Antes de distinguir
    /// "chave errada", cada chave errada custava <see cref="MaxAttempts"/> tentativas por setor —
    /// num cartão protegido por password, só tentar a chave de fábrica primeiro chegava a somar
    /// ~20s à leitura no Agent.
    /// </summary>
    public IReadOnlyCollection<int> AuthenticateSectors(string readerName, IReadOnlyList<int> sectors, byte[] keyA)
    {
        var keyHex = Convert.ToHexString(keyA);
        var authenticated = new HashSet<int>();

        foreach (var chunk in sectors.Chunk(BatchSize))
        {
            var undecided = chunk.ToList();

            for (var attempt = 0; attempt < MaxAttempts && undecided.Count > 0; attempt++)
            {
                if (attempt > 0)
                {
                    Thread.Sleep(RetryDelay);
                }

                var commandBySector = undecided.ToDictionary(sector => sector, sector => ReadBlockCommand(AbsoluteFirstBlockOfSector(sector), keyHex));
                string output;
                try
                {
                    output = Pm3CommandRunner.Run(
                        _pm3ExecutablePath, readerName, string.Join("; ", commandBySector.Values), BatchTimeout(commandBySector.Count));
                }
                catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
                {
                    continue;
                }

                undecided.RemoveAll(sector =>
                {
                    if (TryParseBlockLine(output, AbsoluteFirstBlockOfSector(sector), out _))
                    {
                        _sectorKeys[(readerName, sector)] = keyHex;
                        authenticated.Add(sector);
                        return true;
                    }

                    return CommandOutput(output, commandBySector[sector]).Contains(AuthErrorMarker, StringComparison.Ordinal);
                });
            }
        }

        return authenticated;
    }

    private static string ReadBlockCommand(int absoluteBlock, string keyHex) => $"hf mf rdbl --blk {absoluteBlock} -k {keyHex}";

    /// <summary>
    /// O excerto da saída que pertence a um comando concreto de um lote encadeado — o cliente
    /// ecoa cada comando como <c>pm3 --&gt; &lt;comando&gt;</c> antes da respetiva saída, e um
    /// "Auth error" num comando não interrompe os seguintes (verificado ao vivo).
    /// </summary>
    private static string CommandOutput(string output, string command)
    {
        var marker = CommandEchoPrefix + command;
        var start = output.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        start += marker.Length;
        var end = output.IndexOf(CommandEchoPrefix, start, StringComparison.Ordinal);
        return end < 0 ? output[start..] : output[start..end];
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

        // O backdoor mágico é o único caminho capaz de reescrever o bloco de fabrico (setor 0,
        // bloco 0 — UID/BCC/SAK/ATQA do cartão); uma escrita normal autenticada nunca lá chega.
        // O ID do cartão nunca deve mudar só por gravar o GAME.INI — por isso este bloco fica
        // sempre de fora, mesmo que uma chamada futura (bug ou não) tente passá-lo aqui, e não só
        // porque MifareCardLayout.UsableDataBlocks já o exclui do lado de quem chama.
        if (absoluteBlock == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(absoluteBlock), absoluteBlock, "O bloco de fabrico (UID) nunca pode ser escrito.");
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

    public byte[][] ReadBlocks(string readerName, IReadOnlyList<int> absoluteBlocks)
    {
        var results = new byte[absoluteBlocks.Count][];
        var offset = 0;

        foreach (var chunk in absoluteBlocks.Chunk(BatchSize))
        {
            var chunkData = ReadBlockChunk(readerName, chunk);
            Array.Copy(chunkData, 0, results, offset, chunkData.Length);
            offset += chunkData.Length;
        }

        return results;
    }

    private byte[][] ReadBlockChunk(string readerName, int[] blocks)
    {
        var keyHexByBlock = blocks.Select(b => RequireKey(readerName, b)).ToArray();
        var command = string.Join("; ", blocks.Select((b, i) => $"hf mf rdbl --blk {b} -k {keyHexByBlock[i]}"));
        var data = new byte[blocks.Length][];

        var ok = WithRetries(() =>
        {
            string output;
            try
            {
                output = Pm3CommandRunner.Run(_pm3ExecutablePath, readerName, command, BatchTimeout(blocks.Length));
            }
            catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
            {
                return false;
            }

            for (var i = 0; i < blocks.Length; i++)
            {
                if (!TryParseBlockLine(output, blocks[i], out data[i]))
                {
                    return false;
                }
            }

            return true;
        });

        if (!ok)
        {
            throw new InvalidOperationException($"Falha ao ler {blocks.Length} blocos em lote via Proxmark3 ({readerName}).");
        }

        return data;
    }

    public void WriteBlocks(string readerName, IReadOnlyList<(int AbsoluteBlock, byte[] Data)> blocks)
    {
        foreach (var chunk in blocks.Chunk(BatchSize))
        {
            WriteBlockChunk(readerName, chunk);
        }
    }

    private void WriteBlockChunk(string readerName, (int AbsoluteBlock, byte[] Data)[] chunk)
    {
        foreach (var (_, data) in chunk)
        {
            if (data.Length != 16)
            {
                throw new ArgumentException("Um bloco Mifare Classic tem sempre 16 bytes.", nameof(chunk));
            }
        }

        var command = string.Join(
            "; ", chunk.Select(b => $"hf mf wrbl --blk {b.AbsoluteBlock} -k {RequireKey(readerName, b.AbsoluteBlock)} -d {Convert.ToHexString(b.Data)}"));

        var ok = WithRetries(() =>
        {
            string output;
            try
            {
                output = Pm3CommandRunner.Run(_pm3ExecutablePath, readerName, command, BatchTimeout(chunk.Length));
            }
            catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
            {
                return false;
            }

            return CountOccurrences(output, "Write ( ok )") == chunk.Length;
        });

        if (!ok)
        {
            throw new InvalidOperationException($"Falha ao escrever {chunk.Length} blocos em lote via Proxmark3 ({readerName}).");
        }
    }

    public bool TryMagicWriteBlocks(string readerName, IReadOnlyList<(int AbsoluteBlock, byte[] Data)> blocks)
    {
        foreach (var (absoluteBlock, data) in blocks)
        {
            if (data.Length != 16)
            {
                throw new ArgumentException("Um bloco Mifare Classic tem sempre 16 bytes.", nameof(blocks));
            }

            // Mesma proteção do bloco de fabrico que a versão individual — ver TryMagicWriteBlock.
            if (absoluteBlock == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(blocks), absoluteBlock, "O bloco de fabrico (UID) nunca pode ser escrito.");
            }
        }

        foreach (var chunk in blocks.Chunk(BatchSize))
        {
            if (!TryMagicWriteBlockChunk(readerName, chunk))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryMagicWriteBlockChunk(string readerName, (int AbsoluteBlock, byte[] Data)[] chunk)
    {
        var command = string.Join("; ", chunk.Select(b => $"hf mf csetblk --blk {b.AbsoluteBlock} -d {Convert.ToHexString(b.Data)}"));

        return WithRetries(() =>
        {
            string output;
            try
            {
                output = Pm3CommandRunner.Run(_pm3ExecutablePath, readerName, command, BatchTimeout(chunk.Length));
            }
            catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
            {
                return false;
            }

            // Mesma exigência de prova dupla da versão individual (ver TryMagicWriteBlock), só que
            // a prova positiva agora tem de aparecer uma vez por bloco do lote.
            return CountOccurrences(output, "Writing block number") == chunk.Length
                && !FailureMarkers.Any(marker => output.Contains(marker, StringComparison.Ordinal));
        });
    }

    /// <summary>Cresce com o tamanho do lote — o custo fixo de arranque do processo (~2s) é pago uma só vez, mas cada bloco adicional ainda pode precisar de tempo real de RF.</summary>
    private static TimeSpan BatchTimeout(int blockCount) => TimeSpan.FromSeconds(Math.Max(20, 3 * blockCount));

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += needle.Length;
        }

        return count;
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
