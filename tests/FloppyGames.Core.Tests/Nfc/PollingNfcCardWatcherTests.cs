using System.Diagnostics;
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

    /// <summary>
    /// Regressão: com um <c>lock</c> simples, uma segunda chamada a <see cref="PollingNfcCardWatcher.PollNow"/>
    /// enquanto um ciclo lento está em curso ficava bloqueada à espera — exatamente o que o
    /// <see cref="Timer"/> interno faz a cada segundo. Com muitos ciclos lentos seguidos (ex. o
    /// leitor Proxmark3 a re-tentar contra o lock exclusivo por porta COM partilhado com uma
    /// gravação deliberada), isto empilhava centenas de threads do ThreadPool bloqueadas até
    /// derrubar o processo inteiro sem log nenhum — verificado ao vivo. Este teste confirma que
    /// uma segunda chamada agora devolve de imediato em vez de esperar.
    /// </summary>
    [Fact]
    public void PollNow_CalledWhileAnotherCycleInProgress_ReturnsImmediatelyInsteadOfBlocking()
    {
        var readerDetector = new FakeNfcReaderDetector { Readers = { Reader } };
        var probe = new BlockingPresenceProbe();
        var watcher = new PollingNfcCardWatcher(readerDetector, probe, Logger.None);

        // Uma thread dedicada, não Task.Run: no runner do GitHub (2 núcleos, outros testes em
        // paralelo) o ThreadPool pode demorar segundos a arrancar a tarefa, e o teste falhava sem
        // o ciclo lento sequer ter começado.
        var firstCall = new Thread(watcher.PollNow) { IsBackground = true };
        firstCall.Start();
        Assert.True(probe.CallStarted.Wait(TimeSpan.FromSeconds(30)), "O primeiro ciclo nunca chegou à sonda.");

        var stopwatch = Stopwatch.StartNew();
        watcher.PollNow();
        stopwatch.Stop();

        probe.ReleaseGate.Set();
        Assert.True(firstCall.Join(TimeSpan.FromSeconds(30)));

        // Com o lock antigo, esta segunda chamada ficava bloqueada até a sonda libertar — ou seja,
        // até ReleaseGate (só depois dela) ou o limite de 5s da sonda. Uma margem de 2s continua a
        // distinguir as duas situações sem falhar numa máquina lenta.
        Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"PollNow devia devolver de imediato, mas demorou {stopwatch.ElapsedMilliseconds}ms.");
    }

    private sealed class BlockingPresenceProbe : INfcCardPresenceProbe
    {
        public ManualResetEventSlim CallStarted { get; } = new(false);

        public ManualResetEventSlim ReleaseGate { get; } = new(false);

        public bool TryGetPresentCard(string readerName, out string? uid, out MifareCardType cardType)
        {
            CallStarted.Set();
            ReleaseGate.Wait(TimeSpan.FromSeconds(5));
            uid = null;
            cardType = MifareCardType.Unknown;
            return false;
        }
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
