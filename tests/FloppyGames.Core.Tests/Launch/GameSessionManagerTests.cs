using FloppyGames.Core.Launch;
using FloppyGames.Core.Media;
using FloppyGames.Core.Tests.Media;
using Serilog.Core;

namespace FloppyGames.Core.Tests.Launch;

public class GameSessionManagerTests
{
    private const string DriveRoot = "E:\\";

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
        FakeRemovableMediaWatcher MediaWatcher,
        FakeSteamLauncher Launcher,
        FakeProcessGateway Gateway,
        GameSessionManager Manager) CreateManager(string ini)
    {
        var inspector = new FakeDriveInspector().WithRemovableDrive(DriveRoot).WithGameIni(DriveRoot, ini);
        var mediaWatcher = new FakeRemovableMediaWatcher();
        var mediaService = new RemovableGameMediaService(mediaWatcher, new GameMediaScanner(inspector), Logger.None);
        var launcher = new FakeSteamLauncher();
        var gateway = new FakeProcessGateway();
        var manager = new GameSessionManager(mediaService, launcher, gateway, Logger.None);
        return (mediaWatcher, launcher, gateway, manager);
    }

    [Fact]
    public void MediaInserted_RaisesLaunchStartingSynchronouslyBeforeAnyDelay()
    {
        var (mediaWatcher, _, _, manager) = CreateManager(BuildIni(launchDelaySeconds: 5));
        using var _ = manager;

        GameLaunchStartingEventArgs? received = null;
        manager.LaunchStarting += (_, e) => received = e;

        mediaWatcher.RaiseArrived(DriveRoot);

        Assert.NotNull(received);
        Assert.Equal("Portal", received!.Config.Title);
    }

    [Fact]
    public async Task MediaInserted_ProcessAppearsWithinTimeout_LaunchesSteamAndRaisesGameLaunched()
    {
        var (mediaWatcher, launcher, gateway, manager) = CreateManager(BuildIni());
        using var _ = manager;
        gateway.ProcessToReturn = new FakeManagedProcess();

        var tcs = new TaskCompletionSource<GameLaunchedEventArgs>();
        manager.GameLaunched += (_, e) => tcs.TrySetResult(e);
        manager.GameLaunchFailed += (_, e) => tcs.TrySetException(new Exception($"Falhou inesperadamente: {e.Reason}"));

        mediaWatcher.RaiseArrived(DriveRoot);

        var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal([400], launcher.LaunchedAppIds);
        Assert.Equal(DriveRoot, result.DriveRoot);
    }

    [Fact]
    public async Task MediaInserted_ProcessNeverAppears_RaisesGameLaunchFailedWithTimeoutReason()
    {
        var (mediaWatcher, _, gateway, manager) = CreateManager(BuildIni());
        using var _ = manager;
        gateway.ProcessToReturn = null;

        var tcs = new TaskCompletionSource<string>();
        manager.GameLaunchFailed += (_, e) => tcs.TrySetResult(e.Reason);
        manager.GameLaunched += (_, _) => tcs.TrySetException(new Exception("Não devia ter lançado com sucesso."));

        mediaWatcher.RaiseArrived(DriveRoot);

        var reason = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Contains("Tempo esgotado", reason);
    }

    [Fact]
    public async Task MediaRemoved_DuringLaunchDelay_CancelsLaunch_NeverInvokesSteamLauncher()
    {
        var (mediaWatcher, launcher, _, manager) = CreateManager(BuildIni(launchDelaySeconds: 5));
        using var _ = manager;

        var tcs = new TaskCompletionSource<string>();
        manager.GameLaunchFailed += (_, e) => tcs.TrySetResult(e.Reason);

        mediaWatcher.RaiseArrived(DriveRoot);
        mediaWatcher.RaiseRemoved(DriveRoot);

        var reason = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Contains("removida", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(launcher.LaunchedAppIds);
    }

    [Fact]
    public async Task MediaRemoved_AfterGameLaunched_TerminatesUsingConfiguredGracefulShutdown()
    {
        var (mediaWatcher, _, gateway, manager) = CreateManager(BuildIni(gracefulShutdown: true));
        using var _ = manager;
        var fakeProcess = new FakeManagedProcess();
        gateway.ProcessToReturn = fakeProcess;

        var launchedTcs = new TaskCompletionSource();
        manager.GameLaunched += (_, _) => launchedTcs.TrySetResult();
        mediaWatcher.RaiseArrived(DriveRoot);
        await launchedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var stoppedTcs = new TaskCompletionSource();
        manager.GameStopped += (_, _) => stoppedTcs.TrySetResult();

        mediaWatcher.RaiseRemoved(DriveRoot);
        await stoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var call = Assert.Single(gateway.TerminateCalls);
        Assert.Same(fakeProcess, call.Process);
        Assert.True(call.Graceful);
    }

    [Fact]
    public async Task MediaRemoved_AfterGameLaunched_WithGracefulShutdownFalse_TerminatesForcefully()
    {
        var (mediaWatcher, _, gateway, manager) = CreateManager(BuildIni(gracefulShutdown: false));
        using var _ = manager;
        gateway.ProcessToReturn = new FakeManagedProcess();

        var launchedTcs = new TaskCompletionSource();
        manager.GameLaunched += (_, _) => launchedTcs.TrySetResult();
        mediaWatcher.RaiseArrived(DriveRoot);
        await launchedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var stoppedTcs = new TaskCompletionSource();
        manager.GameStopped += (_, _) => stoppedTcs.TrySetResult();
        mediaWatcher.RaiseRemoved(DriveRoot);
        await stoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var call = Assert.Single(gateway.TerminateCalls);
        Assert.False(call.Graceful);
    }

    [Fact]
    public async Task GameProcessExitsOnItsOwn_RaisesGameStopped_AndSubsequentEjectDoesNotTerminateAgain()
    {
        var (mediaWatcher, _, gateway, manager) = CreateManager(BuildIni());
        using var _ = manager;
        var fakeProcess = new FakeManagedProcess();
        gateway.ProcessToReturn = fakeProcess;

        var launchedTcs = new TaskCompletionSource();
        manager.GameLaunched += (_, _) => launchedTcs.TrySetResult();
        mediaWatcher.RaiseArrived(DriveRoot);
        await launchedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var stoppedCount = 0;
        manager.GameStopped += (_, _) => stoppedCount++;

        fakeProcess.SimulateExit();

        // O eject depois de o jogo já ter saído sozinho não deve tentar terminar nada outra vez.
        mediaWatcher.RaiseRemoved(DriveRoot);

        Assert.Equal(1, stoppedCount);
        Assert.Empty(gateway.TerminateCalls);
    }
}
