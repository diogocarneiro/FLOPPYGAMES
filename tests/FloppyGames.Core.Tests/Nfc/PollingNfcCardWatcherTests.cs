using FloppyGames.Core.Nfc;
using Serilog.Core;

namespace FloppyGames.Core.Tests.Nfc;

public class PollingNfcCardWatcherTests
{
    private const string Reader = "ACME PC/SC Reader 0";

    [Fact]
    public void PollNow_NoReaderConnected_RaisesNoEvents()
    {
        var readerDetector = new FakeNfcReaderDetector();
        var probe = new FakeNfcCardPresenceProbe();
        var watcher = new PollingNfcCardWatcher(readerDetector, probe, Logger.None);

        var anyEventRaised = false;
        watcher.CardArrived += (_, _) => anyEventRaised = true;
        watcher.CardRemoved += (_, _) => anyEventRaised = true;

        watcher.PollNow();

        Assert.False(anyEventRaised);
    }

    [Fact]
    public void PollNow_CardPresented_RaisesCardArrivedOnce()
    {
        var readerDetector = new FakeNfcReaderDetector { Readers = { Reader } };
        var probe = new FakeNfcCardPresenceProbe();
        var watcher = new PollingNfcCardWatcher(readerDetector, probe, Logger.None);
        var arrivedCount = 0;
        watcher.CardArrived += (_, _) => arrivedCount++;

        watcher.PollNow();

        probe.SetPresent(Reader, "04A1B2C3", MifareCardType.Classic1K);
        watcher.PollNow();
        watcher.PollNow();

        Assert.Equal(1, arrivedCount);
    }

    [Fact]
    public void PollNow_CardRemoved_RaisesCardRemovedWithSameUid()
    {
        var readerDetector = new FakeNfcReaderDetector { Readers = { Reader } };
        var probe = new FakeNfcCardPresenceProbe();
        probe.SetPresent(Reader, "04A1B2C3", MifareCardType.Classic1K);
        var watcher = new PollingNfcCardWatcher(readerDetector, probe, Logger.None);
        watcher.PollNow();

        NfcCardPresence? removed = null;
        watcher.CardRemoved += (_, e) => removed = e;

        probe.SetAbsent(Reader);
        watcher.PollNow();

        Assert.NotNull(removed);
        Assert.Equal("04A1B2C3", removed!.Uid);
    }

    [Fact]
    public void PollNow_DifferentCardPresentedWithoutRemovalInBetween_RaisesRemovedThenArrived()
    {
        var readerDetector = new FakeNfcReaderDetector { Readers = { Reader } };
        var probe = new FakeNfcCardPresenceProbe();
        probe.SetPresent(Reader, "CARD-A", MifareCardType.Classic1K);
        var watcher = new PollingNfcCardWatcher(readerDetector, probe, Logger.None);
        watcher.PollNow();

        var events = new List<(string Kind, string Uid)>();
        watcher.CardArrived += (_, e) => events.Add(("arrived", e.Uid));
        watcher.CardRemoved += (_, e) => events.Add(("removed", e.Uid));

        probe.SetPresent(Reader, "CARD-B", MifareCardType.Classic1K);
        watcher.PollNow();

        Assert.Equal([("removed", "CARD-A"), ("arrived", "CARD-B")], events);
    }

    [Fact]
    public void PollNow_ReaderDetectorThrows_DoesNotPropagate()
    {
        var readerDetector = new ThrowingReaderDetector();
        var probe = new FakeNfcCardPresenceProbe();
        var watcher = new PollingNfcCardWatcher(readerDetector, probe, Logger.None);

        var exception = Record.Exception(() => watcher.PollNow());

        Assert.Null(exception);
    }

    [Fact]
    public void PollNow_PresenceProbeThrows_DoesNotPropagate()
    {
        var readerDetector = new FakeNfcReaderDetector { Readers = { Reader } };
        var probe = new ThrowingPresenceProbe();
        var watcher = new PollingNfcCardWatcher(readerDetector, probe, Logger.None);

        var exception = Record.Exception(() => watcher.PollNow());

        Assert.Null(exception);
    }

    [Fact]
    public void PollNow_ReaderUnpluggedWhileCardPresent_RaisesCardRemoved()
    {
        var readerDetector = new FakeNfcReaderDetector { Readers = { Reader } };
        var probe = new FakeNfcCardPresenceProbe();
        probe.SetPresent(Reader, "04A1B2C3", MifareCardType.Classic1K);
        var watcher = new PollingNfcCardWatcher(readerDetector, probe, Logger.None);
        watcher.PollNow();

        var removedCount = 0;
        watcher.CardRemoved += (_, _) => removedCount++;

        readerDetector.Readers.Clear();
        watcher.PollNow();

        Assert.Equal(1, removedCount);
    }

    private sealed class ThrowingReaderDetector : INfcReaderDetector
    {
        public IReadOnlyList<string> ListConnectedReaders() => throw new InvalidOperationException("Falha simulada de PC/SC.");
    }

    private sealed class ThrowingPresenceProbe : INfcCardPresenceProbe
    {
        public bool TryGetPresentCard(string readerName, out string? uid, out MifareCardType cardType) =>
            throw new InvalidOperationException("Falha simulada de PC/SC.");
    }
}
