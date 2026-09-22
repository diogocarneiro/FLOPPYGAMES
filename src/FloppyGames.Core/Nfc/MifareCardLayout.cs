namespace FloppyGames.Core.Nfc;

/// <summary>
/// Calcula quais os blocos de dados de um cartão Mifare Classic que são seguros para gravar
/// dados arbitrários: exclui o bloco de fabrico (setor 0, bloco 0 — só leitura, contém o UID) e
/// o bloco trailer de cada setor (guarda as chaves e os bits de acesso — nunca escrito, sob pena
/// de bloquear o setor permanentemente). Nos setores "grandes" do Mifare 4K (setores 32-39, com
/// 16 blocos cada em vez de 4), o trailer é o último dos 16 blocos, não o quarto.
/// </summary>
public static class MifareCardLayout
{
    private const int BlockSizeBytes = 16;
    private const int SmallSectorCount = 32;
    private const int SmallSectorBlockCount = 4;
    private const int LargeSectorBlockCount = 16;

    public static IReadOnlyList<MifareBlockAddress> UsableDataBlocks(MifareCardType cardType)
    {
        var sectorCount = SectorCount(cardType);
        var blocks = new List<MifareBlockAddress>();

        var absoluteBlock = 0;
        for (var sector = 0; sector < sectorCount; sector++)
        {
            var blocksInSector = BlocksInSector(sector);
            var trailerBlockInSector = blocksInSector - 1;

            for (var blockInSector = 0; blockInSector < blocksInSector; blockInSector++)
            {
                var isManufacturerBlock = sector == 0 && blockInSector == 0;
                var isTrailerBlock = blockInSector == trailerBlockInSector;

                if (!isManufacturerBlock && !isTrailerBlock)
                {
                    blocks.Add(new MifareBlockAddress(sector, blockInSector, absoluteBlock));
                }

                absoluteBlock++;
            }
        }

        return blocks;
    }

    public static int UsableCapacityBytes(MifareCardType cardType) => UsableDataBlocks(cardType).Count * BlockSizeBytes;

    private static int SectorCount(MifareCardType cardType) => cardType switch
    {
        MifareCardType.Classic1K => 16,
        MifareCardType.Classic4K => 40,
        _ => 0,
    };

    private static int BlocksInSector(int sector) => sector < SmallSectorCount ? SmallSectorBlockCount : LargeSectorBlockCount;
}
