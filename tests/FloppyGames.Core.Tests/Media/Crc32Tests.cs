using System.Text;
using FloppyGames.Core.Media;

namespace FloppyGames.Core.Tests.Media;

public class Crc32Tests
{
    [Fact]
    public void Compute_KnownString_MatchesKnownCrc32()
    {
        // "123456789" é o vetor de teste de referência para CRC-32/ISO-HDLC (o algoritmo usado aqui).
        var bytes = Encoding.ASCII.GetBytes("123456789");

        var crc = Crc32.Compute(bytes);

        Assert.Equal(0xCBF43926u, crc);
    }

    [Fact]
    public void Compute_EmptyInput_ReturnsZero()
    {
        Assert.Equal(0u, Crc32.Compute(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Compute_DifferentInputs_ProduceDifferentChecksums()
    {
        var a = Crc32.Compute(Encoding.ASCII.GetBytes("hello"));
        var b = Crc32.Compute(Encoding.ASCII.GetBytes("world"));

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void AppendThenFinalize_MultipleChunks_MatchesComputeOverConcatenatedBytes()
    {
        var partOne = Encoding.ASCII.GetBytes("hello ");
        var partTwo = Encoding.ASCII.GetBytes("world");
        var whole = Encoding.ASCII.GetBytes("hello world");

        var crc = Crc32.InitialState;
        crc = Crc32.Append(crc, partOne);
        crc = Crc32.Append(crc, partTwo);
        var chunked = Crc32.Finalize(crc);

        Assert.Equal(Crc32.Compute(whole), chunked);
    }
}
