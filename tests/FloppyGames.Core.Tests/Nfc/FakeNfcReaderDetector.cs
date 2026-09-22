using FloppyGames.Core.Nfc;

namespace FloppyGames.Core.Tests.Nfc;

internal sealed class FakeNfcReaderDetector : INfcReaderDetector
{
    public List<string> Readers { get; } = new();

    public IReadOnlyList<string> ListConnectedReaders() => Readers;
}
