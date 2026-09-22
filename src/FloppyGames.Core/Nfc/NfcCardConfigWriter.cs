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
                return NfcCardWriteCheck.Blocked(Strings.Core_Nfc_AuthenticationFailed(firstBlock.Sector));
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

    /// <summary>Escreve de facto no cartão. Chamar só depois de <see cref="Check"/> não devolver <c>Blocked</c>.</summary>
    public void Write(string readerName, MifareCardType cardType, GameConfig config)
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
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes} bytes",
    };
}
