using FloppyGames.Core.Nfc;

namespace FloppyGames.Core.Tests.Nfc;

public class MifareCardLayoutTests
{
    [Fact]
    public void UsableDataBlocks_Classic1K_Returns47Blocks()
    {
        var blocks = MifareCardLayout.UsableDataBlocks(MifareCardType.Classic1K);

        Assert.Equal(47, blocks.Count);
    }

    [Fact]
    public void UsableCapacityBytes_Classic1K_Returns752Bytes()
    {
        Assert.Equal(752, MifareCardLayout.UsableCapacityBytes(MifareCardType.Classic1K));
    }

    [Fact]
    public void UsableDataBlocks_Classic1K_ExcludesManufacturerBlock()
    {
        var blocks = MifareCardLayout.UsableDataBlocks(MifareCardType.Classic1K);

        Assert.DoesNotContain(blocks, b => b.Sector == 0 && b.BlockInSector == 0);
    }

    [Fact]
    public void UsableDataBlocks_Classic1K_ExcludesEveryTrailerBlock()
    {
        var blocks = MifareCardLayout.UsableDataBlocks(MifareCardType.Classic1K);

        Assert.DoesNotContain(blocks, b => b.BlockInSector == 3);
    }

    [Fact]
    public void UsableDataBlocks_Classic1K_FirstUsableBlockIsSector0Block1()
    {
        var blocks = MifareCardLayout.UsableDataBlocks(MifareCardType.Classic1K);

        Assert.Equal(new MifareBlockAddress(0, 1, 1), blocks[0]);
    }

    [Fact]
    public void UsableDataBlocks_Classic4K_Returns215Blocks()
    {
        var blocks = MifareCardLayout.UsableDataBlocks(MifareCardType.Classic4K);

        Assert.Equal(215, blocks.Count);
    }

    [Fact]
    public void UsableCapacityBytes_Classic4K_Returns3440Bytes()
    {
        Assert.Equal(3440, MifareCardLayout.UsableCapacityBytes(MifareCardType.Classic4K));
    }

    [Fact]
    public void UsableDataBlocks_Classic4K_ExcludesLargeSectorTrailerBlock15()
    {
        var blocks = MifareCardLayout.UsableDataBlocks(MifareCardType.Classic4K);

        // Setor 32 (o primeiro dos "grandes", 16 blocos) tem o trailer no bloco 15, não no 3.
        Assert.DoesNotContain(blocks, b => b.Sector == 32 && b.BlockInSector == 15);
        Assert.Contains(blocks, b => b.Sector == 32 && b.BlockInSector == 14);
    }

    [Fact]
    public void UsableCapacityBytes_Unknown_ReturnsZero()
    {
        Assert.Equal(0, MifareCardLayout.UsableCapacityBytes(MifareCardType.Unknown));
    }
}
