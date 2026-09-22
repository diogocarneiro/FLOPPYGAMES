using FloppyGames.Core.Nfc;

namespace FloppyGames.Core.Tests.Nfc;

internal sealed class FakeMifareCardGateway : IMifareCardGateway
{
    private readonly Dictionary<int, byte[]> _blocks = new();
    private readonly HashSet<int> _sectorsRequiringAuthFailure = new();

    public List<int> AuthenticatedSectors { get; } = new();

    public List<(int AbsoluteBlock, byte[] Data)> WriteCalls { get; } = new();

    public bool ThrowOnRead { get; set; }

    public bool ThrowOnWrite { get; set; }

    public bool SupportsMagicWrite { get; set; }

    public List<(int AbsoluteBlock, byte[] Data)> MagicWriteCalls { get; } = new();

    public void SetBlock(int absoluteBlock, byte[] data) => _blocks[absoluteBlock] = data;

    public void FailAuthenticationForSector(int sector) => _sectorsRequiringAuthFailure.Add(sector);

    /// <summary>Grava o conteúdo tal como <see cref="NfcCardConfigWriter.Write"/> faria, sem passar pela interface — útil para preparar cenários de leitura.</summary>
    public void SeedCardContent(string iniText, MifareCardType cardType)
    {
        var blocks = MifareConfigCodec.Encode(iniText, cardType);
        var layout = MifareCardLayout.UsableDataBlocks(cardType);
        for (var i = 0; i < blocks.Length; i++)
        {
            SetBlock(layout[i].AbsoluteBlock, blocks[i]);
        }
    }

    public bool Authenticate(string readerName, int sector, byte[] keyA)
    {
        if (_sectorsRequiringAuthFailure.Contains(sector))
        {
            return false;
        }

        AuthenticatedSectors.Add(sector);
        return true;
    }

    public byte[] ReadBlock(string readerName, int absoluteBlock)
    {
        if (ThrowOnRead)
        {
            throw new InvalidOperationException("Falha simulada de leitura.");
        }

        return _blocks.TryGetValue(absoluteBlock, out var data) ? data : new byte[16];
    }

    public void WriteBlock(string readerName, int absoluteBlock, byte[] data)
    {
        if (ThrowOnWrite)
        {
            throw new InvalidOperationException("Falha simulada de escrita.");
        }

        WriteCalls.Add((absoluteBlock, data));
        _blocks[absoluteBlock] = data;
    }

    public bool TryMagicWriteBlock(string readerName, int absoluteBlock, byte[] data)
    {
        if (!SupportsMagicWrite)
        {
            return false;
        }

        MagicWriteCalls.Add((absoluteBlock, data));
        _blocks[absoluteBlock] = data;
        return true;
    }
}
