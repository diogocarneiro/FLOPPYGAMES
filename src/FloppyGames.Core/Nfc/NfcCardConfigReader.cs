using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Nfc;

/// <summary>
/// Lê o GAME.INI gravado num cartão Mifare Classic: autentica setor a setor com a chave de
/// fábrica, lê só os blocos necessários (o primeiro bloco diz quantos mais são precisos — ver
/// <see cref="MifareConfigCodec"/>), e valida o resultado com o mesmo <c>GameIniParser</c> usado
/// para disquetes/pens. Espelha <c>GameMediaScanner</c>.
/// </summary>
public sealed class NfcCardConfigReader
{
    private readonly IMifareCardGateway _gateway;

    public NfcCardConfigReader(IMifareCardGateway gateway) => _gateway = gateway;

    public NfcCardScanResult Read(string readerName, string uid, MifareCardType cardType)
    {
        var layout = MifareCardLayout.UsableDataBlocks(cardType);
        if (layout.Count == 0)
        {
            return NfcCardScanResult.UnsupportedCardType(uid);
        }

        var authenticatedSectors = new HashSet<int>();
        byte[] firstBlockData;

        try
        {
            if (!TryAuthenticateAndRead(readerName, layout[0], authenticatedSectors, out firstBlockData))
            {
                return NfcCardScanResult.AuthenticationFailed(uid, layout[0].Sector);
            }
        }
        catch (Exception ex)
        {
            return NfcCardScanResult.ReaderCommunicationFailure(uid, ex.Message);
        }

        if (!MifareConfigCodec.TryReadDeclaredLength(firstBlockData, out var iniByteLength))
        {
            return NfcCardScanResult.CorruptOrEmptyData(uid);
        }

        var neededBlockCount = MifareConfigCodec.BlocksNeededFor(iniByteLength);
        if (neededBlockCount > layout.Count)
        {
            return NfcCardScanResult.CorruptOrEmptyData(uid);
        }

        var blocks = new List<byte[]> { firstBlockData };

        try
        {
            for (var i = 1; i < neededBlockCount; i++)
            {
                if (!TryAuthenticateAndRead(readerName, layout[i], authenticatedSectors, out var data))
                {
                    return NfcCardScanResult.AuthenticationFailed(uid, layout[i].Sector);
                }

                blocks.Add(data);
            }
        }
        catch (Exception ex)
        {
            return NfcCardScanResult.ReaderCommunicationFailure(uid, ex.Message);
        }

        var iniText = MifareConfigCodec.Decode(blocks);
        if (iniText is null)
        {
            return NfcCardScanResult.CorruptOrEmptyData(uid);
        }

        var parseResult = GameIniParser.Parse(iniText);
        return parseResult.Success
            ? NfcCardScanResult.Valid(uid, parseResult.Config!)
            : NfcCardScanResult.InvalidGameIni(uid, parseResult.Errors);
    }

    private bool TryAuthenticateAndRead(
        string readerName, MifareBlockAddress block, HashSet<int> authenticatedSectors, out byte[] data)
    {
        if (!authenticatedSectors.Contains(block.Sector))
        {
            if (!_gateway.Authenticate(readerName, block.Sector, MifareKeys.FactoryDefaultKeyA))
            {
                data = [];
                return false;
            }

            authenticatedSectors.Add(block.Sector);
        }

        data = _gateway.ReadBlock(readerName, block.AbsoluteBlock);
        return true;
    }
}
