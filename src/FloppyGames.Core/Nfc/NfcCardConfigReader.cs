using System.Collections.Concurrent;
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

    /// <summary>
    /// A chave que funcionou da última vez em cada cartão (por UID) — tentada primeiro na leitura
    /// seguinte do mesmo cartão. Com o Proxmark3 cada tentativa com a chave errada ainda custa uma
    /// invocação do cliente; num cartão protegido por password, deixar de tentar primeiro a chave
    /// de fábrica poupa essa volta de cada vez que o cartão é aproximado. Só em memória: o pior
    /// caso depois de reiniciar é voltar à ordem normal (fábrica primeiro).
    /// </summary>
    private readonly ConcurrentDictionary<string, byte[]> _lastKeyByUid = new(StringComparer.OrdinalIgnoreCase);

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

        var keysToTry = PreferLastKnownKey(uid, MifareKeys.CandidatesWith(extraKeys));
        byte[]? cardKey;
        byte[] firstBlockData;

        try
        {
            cardKey = keysToTry.FirstOrDefault(key => _gateway.Authenticate(readerName, layout[0].Sector, key));
            if (cardKey is null)
            {
                return NfcCardScanResult.AuthenticationFailed(uid, layout[0].Sector);
            }

            _lastKeyByUid[uid] = cardKey;
            firstBlockData = _gateway.ReadBlock(readerName, layout[0].AbsoluteBlock);
        }
        catch (Exception ex)
        {
            return NfcCardScanResult.ReaderCommunicationFailure(uid, ex.Message);
        }

        if (!MifareConfigCodec.TryReadDeclaredLength(firstBlockData, out var iniByteLength))
        {
            return NfcCardScanResult.CorruptOrEmptyData(uid);
        }

        if (iniByteLength == 0)
        {
            return NfcCardScanResult.Empty(uid);
        }

        var neededBlockCount = MifareConfigCodec.BlocksNeededFor(iniByteLength);
        if (neededBlockCount > layout.Count)
        {
            return NfcCardScanResult.CorruptOrEmptyData(uid);
        }

        var blocks = new List<byte[]> { firstBlockData };

        if (neededBlockCount > 1)
        {
            // Os blocos que faltam podem abranger vários setores — autentica-os todos em lote,
            // começando pela chave que já abriu o setor 0 (um cartão protegido pelo Label Studio
            // tem a mesma chave em todos os setores), e lê tudo numa só chamada em lote (ver
            // IMifareCardGateway.ReadBlocks): com o backend Proxmark3 isto é a diferença entre
            // alguns segundos e dezenas de segundos até a splash do Agent conseguir abrir.
            var remaining = layout.Skip(1).Take(neededBlockCount - 1).ToList();
            var pendingSectors = remaining.Select(b => b.Sector).Where(s => s != layout[0].Sector).ToHashSet();

            try
            {
                foreach (var key in keysToTry.OrderBy(key => ReferenceEquals(key, cardKey) ? 0 : 1))
                {
                    if (pendingSectors.Count == 0)
                    {
                        break;
                    }

                    pendingSectors.ExceptWith(_gateway.AuthenticateSectors(readerName, [.. pendingSectors.Order()], key));
                }

                if (pendingSectors.Count > 0)
                {
                    return NfcCardScanResult.AuthenticationFailed(uid, pendingSectors.Min());
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

    private IReadOnlyList<byte[]> PreferLastKnownKey(string uid, IReadOnlyList<byte[]> candidates) =>
        _lastKeyByUid.TryGetValue(uid, out var lastKey)
            ? [.. candidates.OrderBy(key => key.AsSpan().SequenceEqual(lastKey) ? 0 : 1)]
            : candidates;
}
