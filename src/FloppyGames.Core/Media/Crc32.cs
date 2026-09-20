namespace FloppyGames.Core.Media;

/// <summary>
/// CRC-32 (IEEE 802.3, polinómio 0xEDB88320) — o mesmo algoritmo usado em ZIP/PNG/Ethernet.
/// Implementação própria em vez de uma dependência externa: o cálculo é trivial e usado apenas
/// para dar ao ecrã de arranque uma "verificação de integridade do suporte" no estilo retro.
/// </summary>
public static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    public static uint Compute(ReadOnlySpan<byte> data) => Append(0xFFFFFFFF, data) ^ 0xFFFFFFFF;

    /// <summary>Continua um cálculo de CRC32 a partir de um estado anterior — para combinar vários ficheiros.</summary>
    public static uint Append(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    public static uint InitialState => 0xFFFFFFFF;

    public static uint Finalize(uint crc) => crc ^ 0xFFFFFFFF;

    private static uint[] BuildTable()
    {
        var table = new uint[256];

        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[i] = c;
        }

        return table;
    }
}
