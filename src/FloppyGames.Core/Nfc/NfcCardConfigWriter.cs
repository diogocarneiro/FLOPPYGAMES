using FloppyGames.Core.Configuration;
using FloppyGames.Core.Localization;

namespace FloppyGames.Core.Nfc;

/// <summary>
/// Escreve um <see cref="GameConfig"/> num cartão Mifare Classic, validando primeiro que o tipo
/// de cartão é suportado, que o conteúdo cabe no espaço utilizável, e avisando antes de
/// sobrescrever dados já gravados — espelha <c>FloppyMediaWriter</c>. Nunca grava a capa: um
/// cartão Mifare 1K/4K não tem espaço para imagens (ver <see cref="MifareCardLayout"/>).
/// </summary>
public sealed class NfcCardConfigWriter
{
    private static readonly byte[] BlankBlock = new byte[16];

    /// <summary>
    /// Blocos por atualização de progresso — cada grupo corresponde a uma só chamada em lote ao
    /// gateway (ver <see cref="IMifareCardGateway.WriteBlocks"/>), para a UI continuar a ver
    /// atualizações periódicas em vez de tudo de uma vez só no fim de uma gravação de cartão
    /// inteiro.
    /// </summary>
    private const int ProgressChunkSize = 10;

    private readonly IMifareCardGateway _gateway;

    public NfcCardConfigWriter(IMifareCardGateway gateway) => _gateway = gateway;

    /// <param name="extraKeys">Chaves extra a tentar depois da de fábrica — ver <see cref="NfcCardConfigReader.Read"/>.</param>
    public NfcCardWriteCheck Check(string readerName, MifareCardType cardType, GameConfig config, IReadOnlyList<byte[]>? extraKeys = null)
    {
        if (cardType == MifareCardType.Unknown)
        {
            return NfcCardWriteCheck.Blocked(Strings.Core_Nfc_UnsupportedCardType);
        }

        var iniText = GameIniWriter.Write(config);
        var required = MifareConfigCodec.RequiredBytes(iniText);
        var capacity = MifareCardLayout.UsableCapacityBytes(cardType);

        if (required > capacity)
        {
            return NfcCardWriteCheck.Blocked(Strings.Core_Nfc_TooLarge(FormatBytes(required), FormatBytes(capacity)));
        }

        var layout = MifareCardLayout.UsableDataBlocks(cardType);
        var firstBlock = layout[0];
        var keysToTry = MifareKeys.CandidatesWith(extraKeys);

        try
        {
            if (!keysToTry.Any(key => _gateway.Authenticate(readerName, firstBlock.Sector, key)))
            {
                return NfcCardWriteCheck.AuthenticationFailed(Strings.Core_Nfc_AuthenticationFailed(firstBlock.Sector));
            }

            var firstBlockData = _gateway.ReadBlock(readerName, firstBlock.AbsoluteBlock);
            if (MifareConfigCodec.TryReadDeclaredLength(firstBlockData, out var existingLength) && existingLength > 0)
            {
                return NfcCardWriteCheck.NeedsConfirmation(Strings.Core_Nfc_ExistingData);
            }
        }
        catch (Exception ex)
        {
            return NfcCardWriteCheck.Blocked(Strings.Core_Nfc_ReaderCommunicationFailure(ex.Message));
        }

        return NfcCardWriteCheck.Ready();
    }

    /// <summary>
    /// Escreve de facto no cartão. Chamar só depois de <see cref="Check"/> não devolver
    /// <c>Blocked</c>. Percorre SEMPRE todos os blocos utilizáveis do cartão, não só os que o
    /// novo jogo precisa: os primeiros levam o conteúdo real, o resto é limpo a zeros. Isto
    /// garante que nenhum resto de um jogo anterior (gravado com um GAME.INI mais comprido) fica
    /// para trás num bloco que o novo conteúdo já não usa — mesmo sem essa limpeza a leitura já
    /// ficaria correta (o comprimento declarado no primeiro bloco diz onde parar), mas isto evita
    /// qualquer dúvida ao inspecionar o cartão diretamente. Cada bloco é uma comunicação real com
    /// o hardware (pode demorar segundos, sobretudo com o backend Proxmark3 e as suas tentativas
    /// automáticas) — por isso esta chamada é sempre potencialmente lenta e NUNCA deve correr na
    /// thread de UI; <paramref name="progress"/> existe precisamente para a UI poder mostrar
    /// "bloco X de Y" enquanto espera numa thread à parte.
    /// </summary>
    public void Write(
        string readerName, MifareCardType cardType, GameConfig config,
        IProgress<NfcCardWriteProgress>? progress = null, IReadOnlyList<byte[]>? extraKeys = null)
    {
        var iniText = GameIniWriter.Write(config);
        var contentBlocks = MifareConfigCodec.Encode(iniText, cardType);
        var layout = MifareCardLayout.UsableDataBlocks(cardType);
        var keysToTry = MifareKeys.CandidatesWith(extraKeys);

        AuthenticateAllSectorsOrThrow(readerName, layout, keysToTry);

        WriteChunked(readerName, BuildFullCardWrites(layout, contentBlocks), progress);
    }

    /// <summary>
    /// Tenta gravar sem qualquer autenticação, através do backdoor "modo mágico" Gen1a/Gen2 (ver
    /// <see cref="IMifareCardGateway.TryMagicWriteBlock"/>) — a única forma de recuperar um
    /// cartão cujas chaves atuais são desconhecidas, e só funciona nesse tipo de cartão clone,
    /// nunca num Mifare Classic genuíno da NXP. Chamar só depois do utilizador confirmar
    /// explicitamente (é uma escrita mais "bruta", sem a confirmação normal de "já tem dados").
    /// Tal como <see cref="Write"/>, percorre sempre todos os blocos utilizáveis (conteúdo real
    /// seguido de zeros) para nunca deixar restos de um jogo anterior. Devolve <c>false</c> ao
    /// primeiro bloco que falhar — nesse caso o cartão pode ter ficado parcialmente escrito, tal
    /// como aconteceria ao formatar qualquer cartão a meio.
    /// </summary>
    public bool WriteMagic(string readerName, MifareCardType cardType, GameConfig config, IProgress<NfcCardWriteProgress>? progress = null)
    {
        var iniText = GameIniWriter.Write(config);
        var contentBlocks = MifareConfigCodec.Encode(iniText, cardType);
        var layout = MifareCardLayout.UsableDataBlocks(cardType);

        return TryMagicWriteChunked(readerName, BuildFullCardWrites(layout, contentBlocks), progress);
    }

    /// <summary>
    /// Troca a chave de fábrica pela chave derivada de <paramref name="password"/> (ver
    /// <see cref="NfcCardPasswordKey"/>) nos trailers de TODOS os setores utilizáveis do cartão
    /// (não só os que o jogo atual ocupa — <see cref="Write"/> limpa sempre o cartão inteiro, por
    /// isso a proteção acompanha esse âmbito, para não deixar setores "esquecidos" com a chave de
    /// fábrica) — os bits de acesso e o byte de utilizador ficam exatamente os de fábrica
    /// (<c>FF 07 80 69</c>), só a chave muda, para que o cartão continue reescrevível mais tarde
    /// por quem souber a password. Autentica cada setor com a chave de fábrica antes de reescrever
    /// o respetivo trailer — por isso só funciona logo a seguir a um <see cref="Write"/> bem
    /// sucedido, antes de qualquer outra proteção ser aplicada. Ação explicitamente opt-in por
    /// cartão: nunca deve ser chamada automaticamente no fim de uma gravação normal. Devolve
    /// <c>false</c> ao primeiro setor que falhar a autenticar — o cartão pode ficar com alguns
    /// setores protegidos e outros não, tal como uma gravação normal interrompida a meio.
    /// </summary>
    public bool ProtectWithPassword(
        string readerName, MifareCardType cardType, string password,
        IProgress<NfcCardWriteProgress>? progress = null)
    {
        var key = NfcCardPasswordKey.Derive(password);
        var trailerData = BuildTrailerBlock(key);

        var layout = MifareCardLayout.UsableDataBlocks(cardType);
        var sectors = layout.Select(b => b.Sector).Distinct().ToArray();

        var authenticated = _gateway.AuthenticateSectors(readerName, sectors, MifareKeys.FactoryDefaultKeyA);
        if (authenticated.Count != sectors.Length)
        {
            return false;
        }

        var trailerWrites = sectors.Select(sector => (MifareCardLayout.TrailerAbsoluteBlock(sector), sector, trailerData)).ToArray();
        WriteChunked(readerName, trailerWrites, progress);
        return true;
    }

    /// <summary>Key A + bits de acesso de transporte de fábrica (<c>FF 07 80</c>) + byte de utilizador (<c>69</c>) + Key B — só a chave muda em relação a um trailer de fábrica.</summary>
    private static byte[] BuildTrailerBlock(byte[] key) => [.. key, 0xFF, 0x07, 0x80, 0x69, .. key];

    private static (int AbsoluteBlock, int Sector, byte[] Data)[] BuildFullCardWrites(
        IReadOnlyList<MifareBlockAddress> layout, byte[][] contentBlocks) =>
        [.. layout.Select((block, i) => (block.AbsoluteBlock, block.Sector, i < contentBlocks.Length ? contentBlocks[i] : BlankBlock))];

    /// <summary>
    /// Autentica todos os setores necessários, tentando cada chave candidata (fábrica, depois
    /// eventuais chaves extra) num só lote por chave em vez de setor a setor — ver
    /// <see cref="IMifareCardGateway.AuthenticateSectors"/>. Lança se algum setor não autenticar
    /// com nenhuma das chaves tentadas.
    /// </summary>
    private void AuthenticateAllSectorsOrThrow(string readerName, IReadOnlyList<MifareBlockAddress> layout, IReadOnlyList<byte[]> keysToTry)
    {
        var allSectors = layout.Select(b => b.Sector).Distinct().ToArray();
        var authenticatedSectors = new HashSet<int>();

        foreach (var key in keysToTry)
        {
            var remaining = allSectors.Where(s => !authenticatedSectors.Contains(s)).ToArray();
            if (remaining.Length == 0)
            {
                break;
            }

            authenticatedSectors.UnionWith(_gateway.AuthenticateSectors(readerName, remaining, key));
        }

        var failedSector = allSectors.FirstOrDefault(s => !authenticatedSectors.Contains(s), -1);
        if (failedSector != -1)
        {
            throw new InvalidOperationException($"Falha de autenticação no setor {failedSector} ao gravar.");
        }
    }

    /// <summary>Escreve <paramref name="writes"/> em grupos de <see cref="ProgressChunkSize"/>, reportando progresso depois de cada grupo — ver <see cref="ProgressChunkSize"/>.</summary>
    private void WriteChunked(
        string readerName, IReadOnlyList<(int AbsoluteBlock, int Sector, byte[] Data)> writes, IProgress<NfcCardWriteProgress>? progress)
    {
        for (var start = 0; start < writes.Count; start += ProgressChunkSize)
        {
            var count = Math.Min(ProgressChunkSize, writes.Count - start);
            var chunk = new (int AbsoluteBlock, byte[] Data)[count];
            for (var i = 0; i < count; i++)
            {
                chunk[i] = (writes[start + i].AbsoluteBlock, writes[start + i].Data);
            }

            _gateway.WriteBlocks(readerName, chunk);
            ReportChunkProgress(writes, start, count, progress);
        }
    }

    /// <summary>Como <see cref="WriteChunked"/>, mas pelo backdoor mágico — devolve <c>false</c> ao primeiro grupo que falhar.</summary>
    private bool TryMagicWriteChunked(
        string readerName, IReadOnlyList<(int AbsoluteBlock, int Sector, byte[] Data)> writes, IProgress<NfcCardWriteProgress>? progress)
    {
        for (var start = 0; start < writes.Count; start += ProgressChunkSize)
        {
            var count = Math.Min(ProgressChunkSize, writes.Count - start);
            var chunk = new (int AbsoluteBlock, byte[] Data)[count];
            for (var i = 0; i < count; i++)
            {
                chunk[i] = (writes[start + i].AbsoluteBlock, writes[start + i].Data);
            }

            if (!_gateway.TryMagicWriteBlocks(readerName, chunk))
            {
                return false;
            }

            ReportChunkProgress(writes, start, count, progress);
        }

        return true;
    }

    private static void ReportChunkProgress(
        IReadOnlyList<(int AbsoluteBlock, int Sector, byte[] Data)> writes, int start, int count, IProgress<NfcCardWriteProgress>? progress)
    {
        for (var i = 0; i < count; i++)
        {
            var index = start + i;
            var (absoluteBlock, sector, data) = writes[index];
            progress?.Report(new NfcCardWriteProgress(index + 1, writes.Count, sector, absoluteBlock, data));
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes} bytes",
    };
}
