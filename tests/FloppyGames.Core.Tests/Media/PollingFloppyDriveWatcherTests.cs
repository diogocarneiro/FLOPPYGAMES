using System.Threading;
using FloppyGames.Core.Media;
using Serilog.Core;

namespace FloppyGames.Core.Tests.Media;

public class PollingFloppyDriveWatcherTests
{
    [Fact]
    public void PollNow_TransitionNotReadyToReady_RaisesDriveArrivedOnce()
    {
        var probe = new FakeDriveReadinessProbe();
        var watcher = new PollingFloppyDriveWatcher(["A:\\"], probe, Logger.None);
        var arrivedCount = 0;
        watcher.DriveArrived += (_, _) => arrivedCount++;

        watcher.PollNow();

        probe.SetReady("A:\\", true);
        watcher.PollNow();
        watcher.PollNow();

        Assert.Equal(1, arrivedCount);
    }

    [Fact]
    public void PollNow_TransitionReadyToNotReady_RaisesDriveRemoved()
    {
        var probe = new FakeDriveReadinessProbe();
        probe.SetReady("A:\\", true);
        var watcher = new PollingFloppyDriveWatcher(["A:\\"], probe, Logger.None);
        watcher.PollNow();

        var removedCount = 0;
        watcher.DriveRemoved += (_, _) => removedCount++;

        probe.SetReady("A:\\", false);
        watcher.PollNow();
        watcher.PollNow();

        Assert.Equal(1, removedCount);
    }

    [Fact]
    public void PollNow_MultipleCandidateDrives_TracksEachIndependently()
    {
        var probe = new FakeDriveReadinessProbe();
        var watcher = new PollingFloppyDriveWatcher(["A:\\", "B:\\"], probe, Logger.None);
        var arrivedDrives = new List<string>();
        watcher.DriveArrived += (_, root) => arrivedDrives.Add(root);

        probe.SetReady("B:\\", true);
        watcher.PollNow();

        Assert.Equal(["B:\\"], arrivedDrives);
    }

    [Fact]
    public void PollNow_ProbeThrows_DoesNotPropagateAndKeepsPollingOtherDrives()
    {
        var probe = new ThrowingReadinessProbe(throwFor: "A:\\");
        var watcher = new PollingFloppyDriveWatcher(["A:\\", "B:\\"], probe, Logger.None);
        probe.SetReady("B:\\", true);

        var arrivedDrives = new List<string>();
        watcher.DriveArrived += (_, root) => arrivedDrives.Add(root);

        var exception = Record.Exception(() => watcher.PollNow());

        Assert.Null(exception);
        Assert.Equal(["B:\\"], arrivedDrives);
    }

    [Fact]
    public async Task PollNow_CalledConcurrentlyFromMultipleThreads_RaisesDriveArrivedExactlyOnce()
    {
        // Regressão: com Timer, ciclos de sondagem podem sobrepor-se em threads do ThreadPool.
        // Sem serialização interna, duas chamadas concorrentes liam _mounted como "não montado"
        // antes de qualquer uma escrever, disparando DriveArrived duas vezes para a mesma unidade.
        var probe = new FakeDriveReadinessProbe();
        probe.SetReady("A:\\", true);
        var watcher = new PollingFloppyDriveWatcher(["A:\\"], probe, Logger.None);

        var arrivedCount = 0;
        watcher.DriveArrived += (_, _) => Interlocked.Increment(ref arrivedCount);

        var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(watcher.PollNow));
        await Task.WhenAll(tasks);

        Assert.Equal(1, arrivedCount);
    }

    private sealed class ThrowingReadinessProbe(string throwFor) : IDriveReadinessProbe
    {
        private readonly Dictionary<string, bool> _ready = new(StringComparer.OrdinalIgnoreCase);

        public void SetReady(string driveRoot, bool ready) => _ready[driveRoot] = ready;

        public bool IsReady(string driveRoot)
        {
            if (string.Equals(driveRoot, throwFor, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Falha simulada de hardware.");
            }

            return _ready.GetValueOrDefault(driveRoot);
        }
    }
}
