using FloppyGames.Core.Nfc;

namespace FloppyGames.Core.Tests.Nfc;

public class MifareConfigCodecTests
{
    [Fact]
    public void EncodeThenDecode_ShortText_RoundTrips()
    {
        const string ini = "[Game]\nTITLE=Portal\nAPPID=400\nPROCESS=portal.exe\n";

        var blocks = MifareConfigCodec.Encode(ini, MifareCardType.Classic1K);
        var decoded = MifareConfigCodec.Decode(blocks);

        Assert.Equal(ini, decoded);
    }

    [Fact]
    public void RequiredBytes_IncludesTwoByteLengthPrefix()
    {
        Assert.Equal(7, MifareConfigCodec.RequiredBytes("ABCDE"));
    }

    [Fact]
    public void Encode_ContentExactlyAtClassic1KCapacity_Succeeds()
    {
        // 752 bytes de capacidade útil - 2 bytes de prefixo = 750 bytes de texto.
        var ini = new string('A', 750);

        var blocks = MifareConfigCodec.Encode(ini, MifareCardType.Classic1K);

        Assert.Equal(ini, MifareConfigCodec.Decode(blocks));
    }

    [Fact]
    public void Encode_ContentOneByteOverClassic1KCapacity_Throws()
    {
        var ini = new string('A', 751);

        Assert.Throws<InvalidOperationException>(() => MifareConfigCodec.Encode(ini, MifareCardType.Classic1K));
    }

    [Fact]
    public void Encode_PadsFinalBlockToSixteenBytes()
    {
        var blocks = MifareConfigCodec.Encode("A", MifareCardType.Classic1K);

        Assert.All(blocks, b => Assert.Equal(16, b.Length));
    }

    [Fact]
    public void Decode_FewerBlocksThanDeclaredLength_ReturnsNull()
    {
        var blocks = MifareConfigCodec.Encode(new string('A', 100), MifareCardType.Classic1K);

        var decoded = MifareConfigCodec.Decode(blocks[..^1]);

        Assert.Null(decoded);
    }

    [Fact]
    public void Decode_EmptyBlockList_ReturnsNull()
    {
        Assert.Null(MifareConfigCodec.Decode([]));
    }

    [Fact]
    public void Decode_AllZeroFirstBlock_ReturnsEmptyString()
    {
        // Um cartão em branco/nunca gravado lê como zeros — comprimento declarado 0, não corrupção.
        var decoded = MifareConfigCodec.Decode([new byte[16]]);

        Assert.Equal(string.Empty, decoded);
    }

    [Fact]
    public void BlocksNeededFor_ExactMultipleOfBlockSize_DoesNotRoundUpUnnecessarily()
    {
        // 14 bytes de texto + 2 de prefixo = 16 bytes = exatamente 1 bloco.
        Assert.Equal(1, MifareConfigCodec.BlocksNeededFor(14));
        Assert.Equal(2, MifareConfigCodec.BlocksNeededFor(15));
    }
}
