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
        var blocks = MifareConfigCodec.Encode(iniText, cardType);
        var layout = MifareCardLayout.UsableDataBlocks(cardType);
        var keysToTry = MifareKeys.CandidatesWith(extraKeys);

        var authenticatedSectors = new HashSet<int>();
        for (var i = 0; i < layout.Count; i++)
        {
            var block = layout[i];
            if (!authenticatedSectors.Contains(block.Sector))
            {
                if (!keysToTry.Any(key => _gateway.Authenticate(readerName, block.Sector, key)))
                {
                    throw new InvalidOperationException($"Falha de autenticação no setor {block.Sector} ao gravar.");
                }

                authenticatedSectors.Add(block.Sector);
            }

            var data = i < blocks.Length ? blocks[i] : BlankBlock;
            _gateway.WriteBlock(readerName, block.AbsoluteBlock, data);
            progress?.Report(new NfcCardWriteProgress(i + 1, layout.Count, block.Sector, block.AbsoluteBlock, data));
        }
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
        var blocks = MifareConfigCodec.Encode(iniText, cardType);
        var layout = MifareCardLayout.UsableDataBlocks(cardType);

        for (var i = 0; i < layout.Count; i++)
        {
            var block = layout[i];
            var data = i < blocks.Length ? blocks[i] : BlankBlock;
            if (!_gateway.TryMagicWriteBlock(readerName, block.AbsoluteBlock, data))
            {
                return false;
            }

            progress?.Report(new NfcCardWriteProgress(i + 1, layout.Count, block.Sector, block.AbsoluteBlock, data));
        }

        return true;
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

        for (var i = 0; i < sectors.Length; i++)
        {
            var sector = sectors[i];
            if (!_gateway.Authenticate(readerName, sector, MifareKeys.FactoryDefaultKeyA))
            {
                return false;
            }

            var trailerBlock = MifareCardLayout.TrailerAbsoluteBlock(sector);
            _gateway.WriteBlock(readerName, trailerBlock, trailerData);
            progress?.Report(new NfcCardWriteProgress(i + 1, sectors.Length, sector, trailerBlock, trailerData));
        }

        return true;
    }

    /// <summary>Key A + bits de acesso de transporte de fábrica (<c>FF 07 80</c>) + byte de utilizador (<c>69</c>) + Key B — só a chave muda em relação a um trailer de fábrica.</summary>
    private static byte[] BuildTrailerBlock(byte[] key) => [.. key, 0xFF, 0x07, 0x80, 0x69, .. key];

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes} bytes",
    };
}
