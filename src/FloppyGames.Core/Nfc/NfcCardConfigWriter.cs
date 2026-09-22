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
    private readonly IMifareCardGateway _gateway;

    public NfcCardConfigWriter(IMifareCardGateway gateway) => _gateway = gateway;

    public NfcCardWriteCheck Check(string readerName, MifareCardType cardType, GameConfig config)
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

        try
        {
            if (!_gateway.Authenticate(readerName, firstBlock.Sector, MifareKeys.FactoryDefaultKeyA))
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
    /// <c>Blocked</c>. Cada bloco é uma comunicação real com o hardware (pode demorar segundos,
    /// sobretudo com o backend Proxmark3 e as suas tentativas automáticas) — por isso esta chamada
    /// é sempre potencialmente lenta e NUNCA deve correr na thread de UI; <paramref name="progress"/>
    /// existe precisamente para a UI poder mostrar "bloco X de Y" enquanto espera numa thread à parte.
    /// </summary>
    public void Write(string readerName, MifareCardType cardType, GameConfig config, IProgress<(int Current, int Total)>? progress = null)
    {
        var iniText = GameIniWriter.Write(config);
        var blocks = MifareConfigCodec.Encode(iniText, cardType);
        var layout = MifareCardLayout.UsableDataBlocks(cardType);

        var authenticatedSectors = new HashSet<int>();
        for (var i = 0; i < blocks.Length; i++)
        {
            var block = layout[i];
            if (!authenticatedSectors.Contains(block.Sector))
            {
                if (!_gateway.Authenticate(readerName, block.Sector, MifareKeys.FactoryDefaultKeyA))
                {
                    throw new InvalidOperationException($"Falha de autenticação no setor {block.Sector} ao gravar.");
                }

                authenticatedSectors.Add(block.Sector);
            }

            _gateway.WriteBlock(readerName, block.AbsoluteBlock, blocks[i]);
            progress?.Report((i + 1, blocks.Length));
        }
    }

    /// <summary>
    /// Tenta gravar sem qualquer autenticação, através do backdoor "modo mágico" Gen1a/Gen2 (ver
    /// <see cref="IMifareCardGateway.TryMagicWriteBlock"/>) — a única forma de recuperar um
    /// cartão cujas chaves atuais são desconhecidas, e só funciona nesse tipo de cartão clone,
    /// nunca num Mifare Classic genuíno da NXP. Chamar só depois do utilizador confirmar
    /// explicitamente (é uma escrita mais "bruta", sem a confirmação normal de "já tem dados").
    /// Devolve <c>false</c> ao primeiro bloco que falhar — nesse caso o cartão pode ter ficado
    /// parcialmente escrito, tal como aconteceria ao formatar qualquer cartão a meio.
    /// </summary>
    public bool WriteMagic(string readerName, MifareCardType cardType, GameConfig config, IProgress<(int Current, int Total)>? progress = null)
    {
        var iniText = GameIniWriter.Write(config);
        var blocks = MifareConfigCodec.Encode(iniText, cardType);
        var layout = MifareCardLayout.UsableDataBlocks(cardType);

        for (var i = 0; i < blocks.Length; i++)
        {
            if (!_gateway.TryMagicWriteBlock(readerName, layout[i].AbsoluteBlock, blocks[i]))
            {
                return false;
            }

            progress?.Report((i + 1, blocks.Length));
        }

        return true;
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes} bytes",
    };
}
