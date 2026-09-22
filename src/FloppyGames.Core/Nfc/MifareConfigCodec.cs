using System.Text;

namespace FloppyGames.Core.Nfc;

/// <summary>
/// Fatia o texto GAME.INI já produzido por <c>GameIniWriter.Write</c> em blocos de 16 bytes para
/// gravar num cartão Mifare Classic, e reconstrói-o na leitura. Não inventa nenhum formato
/// binário novo — só um prefixo de comprimento (2 bytes) à frente do texto UTF-8, para saber
/// quantos blocos ler de volta sem ter de esgotar todos os blocos utilizáveis do cartão.
/// </summary>
public static class MifareConfigCodec
{
    private const int LengthPrefixBytes = 2;
    private const int BlockSizeBytes = 16;
    private const int MaxSupportedPayloadBytes = ushort.MaxValue;

    public static int RequiredBytes(string iniText) => LengthPrefixBytes + Encoding.UTF8.GetByteCount(iniText);

    /// <summary>
    /// Codifica <paramref name="iniText"/> nos blocos de 16 bytes que devem ser escritos, pela
    /// ordem dos blocos utilizáveis do <paramref name="cardType"/> (ver <see cref="MifareCardLayout"/>).
    /// Só devolve os blocos necessários para o conteúdo — não é preciso escrever todos os blocos
    /// utilizáveis do cartão.
    /// </summary>
    public static byte[][] Encode(string iniText, MifareCardType cardType)
    {
        ArgumentNullException.ThrowIfNull(iniText);

        var iniBytes = Encoding.UTF8.GetBytes(iniText);
        var capacity = MifareCardLayout.UsableCapacityBytes(cardType);
        var required = LengthPrefixBytes + iniBytes.Length;

        if (required > capacity)
        {
            throw new InvalidOperationException(
                $"GAME.INI codificado ({required} bytes) excede a capacidade utilizável do cartão ({capacity} bytes).");
        }

        var payload = new byte[required];
        payload[0] = (byte)(iniBytes.Length >> 8);
        payload[1] = (byte)(iniBytes.Length & 0xFF);
        Array.Copy(iniBytes, 0, payload, LengthPrefixBytes, iniBytes.Length);

        var blockCount = BlocksNeededFor(iniBytes.Length);
        var blocks = new byte[blockCount][];
        for (var i = 0; i < blockCount; i++)
        {
            var block = new byte[BlockSizeBytes];
            var offset = i * BlockSizeBytes;
            var length = Math.Min(BlockSizeBytes, payload.Length - offset);
            Array.Copy(payload, offset, block, 0, length);
            blocks[i] = block;
        }

        return blocks;
    }

    /// <summary>Lê o comprimento declarado no primeiro bloco, para saber quantos blocos ler a seguir.</summary>
    public static bool TryReadDeclaredLength(byte[] firstBlock, out int iniByteLength)
    {
        if (firstBlock.Length < LengthPrefixBytes)
        {
            iniByteLength = 0;
            return false;
        }

        iniByteLength = (firstBlock[0] << 8) | firstBlock[1];
        return iniByteLength <= MaxSupportedPayloadBytes;
    }

    public static int BlocksNeededFor(int iniByteLength) =>
        (int)Math.Ceiling((LengthPrefixBytes + iniByteLength) / (double)BlockSizeBytes);

    /// <summary>
    /// Reconstrói o texto GAME.INI a partir dos blocos lidos pela mesma ordem em que foram
    /// escritos. <paramref name="blocksInOrder"/> deve conter pelo menos os blocos indicados por
    /// <see cref="BlocksNeededFor"/> para o comprimento declarado no primeiro bloco — caso
    /// contrário (cartão vazio, corrompido, ou de outro sistema) devolve <c>null</c>.
    /// </summary>
    public static string? Decode(IReadOnlyList<byte[]> blocksInOrder)
    {
        if (blocksInOrder.Count == 0 || !TryReadDeclaredLength(blocksInOrder[0], out var iniByteLength))
        {
            return null;
        }

        var neededBlocks = BlocksNeededFor(iniByteLength);
        if (blocksInOrder.Count < neededBlocks)
        {
            return null;
        }

        var payload = new byte[neededBlocks * BlockSizeBytes];
        for (var i = 0; i < neededBlocks; i++)
        {
            Array.Copy(blocksInOrder[i], 0, payload, i * BlockSizeBytes, BlockSizeBytes);
        }

        return Encoding.UTF8.GetString(payload, LengthPrefixBytes, iniByteLength);
    }
}
