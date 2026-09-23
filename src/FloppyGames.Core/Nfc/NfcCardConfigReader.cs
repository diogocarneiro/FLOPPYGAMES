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

    /// <param name="extraKeys">
    /// Chaves extra a tentar depois da chave de fábrica (ex.: derivada de uma password de
    /// proteção configurada nas Definições, ver <see cref="NfcCardPasswordKey"/>) — sem isto, um
    /// cartão protegido deixaria de ser reconhecido ao ser aproximado do leitor.
    /// </param>
    public NfcCardScanResult Read(string readerName, string uid, MifareCardType cardType, IReadOnlyList<byte[]>? extraKeys = null)
    {
        var layout = MifareCardLayout.UsableDataBlocks(cardType);
        if (layout.Count == 0)
        {
            return NfcCardScanResult.UnsupportedCardType(uid);
        }

        var keysToTry = MifareKeys.CandidatesWith(extraKeys);
        var authenticatedSectors = new HashSet<int>();
        byte[] firstBlockData;

        try
        {
            if (!TryAuthenticateAndRead(readerName, layout[0], authenticatedSectors, keysToTry, out firstBlockData))
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

        if (neededBlockCount > 1)
        {
            // Os blocos que faltam podem abranger vários setores — autentica cada setor ainda por
            // ver e lê tudo numa só chamada em lote (ver IMifareCardGateway.ReadBlocks), em vez de
            // um bloco de cada vez: com o backend Proxmark3 isto é a diferença entre alguns
            // segundos e dezenas de segundos até a splash do Agent conseguir abrir.
            var remaining = layout.Skip(1).Take(neededBlockCount - 1).ToList();

            try
            {
                foreach (var sector in remaining.Select(b => b.Sector).Distinct())
                {
                    if (authenticatedSectors.Contains(sector))
                    {
                        continue;
                    }

                    if (!keysToTry.Any(key => _gateway.Authenticate(readerName, sector, key)))
                    {
                        return NfcCardScanResult.AuthenticationFailed(uid, sector);
                    }

                    authenticatedSectors.Add(sector);
                }

                blocks.AddRange(_gateway.ReadBlocks(readerName, [.. remaining.Select(b => b.AbsoluteBlock)]));
            }
            catch (Exception ex)
            {
                return NfcCardScanResult.ReaderCommunicationFailure(uid, ex.Message);
            }
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
        string readerName, MifareBlockAddress block, HashSet<int> authenticatedSectors, IReadOnlyList<byte[]> keysToTry, out byte[] data)
    {
        if (!authenticatedSectors.Contains(block.Sector))
        {
            if (!keysToTry.Any(key => _gateway.Authenticate(readerName, block.Sector, key)))
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
