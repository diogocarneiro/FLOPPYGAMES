using FloppyGames.Core.Launch;
using FloppyGames.Core.Nfc;
using FloppyGames.Core.Tests.Nfc;
using Serilog.Core;

namespace FloppyGames.Core.Tests.Launch;

public class NfcCardSessionManagerTests
{
    /// <summary>
    /// Só um teto para eventos que devem chegar quase de imediato — nunca é esperado que expire.
    /// Generoso de propósito: no runner do GitHub (2 núcleos, testes em paralelo) o ThreadPool
    /// pode demorar segundos a correr as continuações, e 2s chegavam a falhar sem haver bug.
    /// </summary>
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(30);

    private const string Reader = "ACME PC/SC Reader 0";
    private const string Uid = "04A1B2C3";

    private static string BuildIni(int launchDelaySeconds = 0, bool gracefulShutdown = true) => $"""
        [Game]
        TITLE=Portal
        APPID=400
        PROCESS=portal.exe

        [Options]
        LaunchDelaySeconds={launchDelaySeconds}
        GracefulShutdown={(gracefulShutdown ? "true" : "false")}
        """;

    private static (
        FakeNfcCardWatcher CardWatcher,
        FakeGameLauncher Launcher,
        FakeProcessGateway Gateway,
        NfcCardSessionManager Manager) CreateManager(string ini)
    {
        var cardWatcher = new FakeNfcCardWatcher();
        var mifareGateway = new FakeMifareCardGateway();
        mifareGateway.SeedCardContent(ini, MifareCardType.Classic1K);
        var mediaService = new NfcGameMediaService(cardWatcher, new NfcCardConfigReader(mifareGateway), Logger.None);
        var launcher = new FakeGameLauncher();
        var processGateway = new FakeProcessGateway();
        var manager = new NfcCardSessionManager(mediaService, launcher, processGateway, Logger.None);
        return (cardWatcher, launcher, processGateway, manager);
    }

    private static void RaiseArrived(FakeNfcCardWatcher watcher) =>
        watcher.RaiseArrived(new NfcCardPresence(Reader, Uid, MifareCardType.Classic1K));

    private static void RaiseRemoved(FakeNfcCardWatcher watcher) =>
        watcher.RaiseRemoved(new NfcCardPresence(Reader, Uid, MifareCardType.Classic1K));

    [Fact]
    public void CardInserted_RaisesLaunchStartingSynchronouslyBeforeAnyDelay()
    {
        var (cardWatcher, _, _, manager) = CreateManager(BuildIni(launchDelaySeconds: 5));
        using var _ = manager;

        NfcCardLaunchStartingEventArgs? received = null;
        manager.LaunchStarting += (_, e) => received = e;

        RaiseArrived(cardWatcher);

        Assert.NotNull(received);
        Assert.Equal("Portal", received!.Config.Title);
        Assert.Equal(Uid, received.Uid);
    }

    [Fact]
    public async Task CardInserted_ProcessAppearsWithinTimeout_LaunchesSteamAndRaisesGameLaunched()
    {
        var (cardWatcher, launcher, gateway, manager) = CreateManager(BuildIni());
        using var _ = manager;
        gateway.ProcessToReturn = new FakeManagedProcess();

        var tcs = new TaskCompletionSource<NfcCardLaunchedEventArgs>();
        manager.GameLaunched += (_, e) => tcs.TrySetResult(e);
        manager.GameLaunchFailed += (_, e) => tcs.TrySetException(new Exception($"Falhou inesperadamente: {e.Reason}"));

        RaiseArrived(cardWatcher);

        var result = await tcs.Task.WaitAsync(EventTimeout);

        Assert.Equal([400], launcher.LaunchedConfigs.Select(c => c.AppId));
        Assert.Equal(Uid, result.Uid);
    }

    [Fact]
    public async Task CardInserted_ProcessNeverAppears_RaisesGameLaunchFailedWithTimeoutReason()
    {
        var (cardWatcher, _, gateway, manager) = CreateManager(BuildIni());
        using var _ = manager;
        gateway.ProcessToReturn = null;

        var tcs = new TaskCompletionSource<GameLaunchFailureReason>();
        manager.GameLaunchFailed += (_, e) => tcs.TrySetResult(e.Reason);
        manager.GameLaunched += (_, _) => tcs.TrySetException(new Exception("Não devia ter lançado com sucesso."));

        RaiseArrived(cardWatcher);

        var reason = await tcs.Task.WaitAsync(EventTimeout);

        Assert.Equal(GameLaunchFailureReason.Timeout, reason);
    }

    [Fact]
    public async Task CardRemoved_DuringLaunchDelay_CancelsLaunch_NeverInvokesLauncher()
    {
        var (cardWatcher, launcher, _, manager) = CreateManager(BuildIni(launchDelaySeconds: 5));
        using var _ = manager;

        var tcs = new TaskCompletionSource<GameLaunchFailureReason>();
        manager.GameLaunchFailed += (_, e) => tcs.TrySetResult(e.Reason);

        RaiseArrived(cardWatcher);
        RaiseRemoved(cardWatcher);

        var reason = await tcs.Task.WaitAsync(EventTimeout);

        Assert.Equal(GameLaunchFailureReason.MediaRemovedDuringLaunch, reason);
        Assert.Empty(launcher.LaunchedConfigs);
    }

    [Fact]
    public async Task CardRemoved_AfterGameLaunched_TerminatesUsingConfiguredGracefulShutdown()
    {
        var (cardWatcher, _, gateway, manager) = CreateManager(BuildIni(gracefulShutdown: true));
        using var _ = manager;
        var fakeProcess = new FakeManagedProcess();
        gateway.ProcessToReturn = fakeProcess;

        var launchedTcs = new TaskCompletionSource();
        manager.GameLaunched += (_, _) => launchedTcs.TrySetResult();
        RaiseArrived(cardWatcher);
        await launchedTcs.Task.WaitAsync(EventTimeout);

        var stoppedTcs = new TaskCompletionSource();
        manager.GameStopped += (_, _) => stoppedTcs.TrySetResult();

        RaiseRemoved(cardWatcher);
        await stoppedTcs.Task.WaitAsync(EventTimeout);

        var call = Assert.Single(gateway.TerminateCalls);
        Assert.Same(fakeProcess, call.Process);
        Assert.True(call.Graceful);
    }

    [Fact]
    public async Task GameProcessExitsOnItsOwn_RaisesGameStopped_AndSubsequentRemovalDoesNotTerminateAgain()
    {
        var (cardWatcher, _, gateway, manager) = CreateManager(BuildIni());
        using var _ = manager;
        var fakeProcess = new FakeManagedProcess();
        gateway.ProcessToReturn = fakeProcess;

        var launchedTcs = new TaskCompletionSource();
        manager.GameLaunched += (_, _) => launchedTcs.TrySetResult();
        RaiseArrived(cardWatcher);
        await launchedTcs.Task.WaitAsync(EventTimeout);

        var stoppedCount = 0;
        manager.GameStopped += (_, _) => stoppedCount++;

        fakeProcess.SimulateExit();

        RaiseRemoved(cardWatcher);

        Assert.Equal(1, stoppedCount);
        Assert.Empty(gateway.TerminateCalls);
    }
}
