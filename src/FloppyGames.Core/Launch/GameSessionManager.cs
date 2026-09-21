using FloppyGames.Core.Configuration;
using FloppyGames.Core.Media;
using Serilog;

namespace FloppyGames.Core.Launch;

/// <summary>
/// Orquestra o ciclo de vida de um jogo a partir dos eventos de mídia: atraso de lançamento,
/// invocação do launcher da plataforma, confirmação de arranque, e encerramento automático no eject.
/// Suporta múltiplas unidades em simultâneo, sem interferência entre elas — cada uma tem o seu
/// próprio lançamento/sessão rastreado independentemente por <c>driveRoot</c>.
/// </summary>
public sealed class GameSessionManager : IDisposable
{
    private readonly RemovableGameMediaService _mediaService;
    private readonly IGameLauncher _launcher;
    private readonly IProcessGateway _processGateway;
    private readonly ILogger _logger;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, GameSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> _pendingLaunches = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public GameSessionManager(
        RemovableGameMediaService mediaService, IGameLauncher launcher, IProcessGateway processGateway, ILogger logger)
    {
        _mediaService = mediaService;
        _launcher = launcher;
        _processGateway = processGateway;
        _logger = logger.ForContext<GameSessionManager>();

        _mediaService.MediaInserted += OnMediaInserted;
        _mediaService.MediaRemoved += OnMediaRemoved;
    }

    /// <summary>Disparado assim que a mídia é reconhecida, antes do atraso de lançamento — momento certo para mostrar a splash.</summary>
    public event EventHandler<GameLaunchStartingEventArgs>? LaunchStarting;

    /// <summary>Disparado quando o processo do jogo é confirmado em execução.</summary>
    public event EventHandler<GameLaunchedEventArgs>? GameLaunched;

    /// <summary>Disparado quando o timeout expira sem o processo aparecer, ou o lançamento é cancelado (disquete removida antes de confirmar).</summary>
    public event EventHandler<GameLaunchFailedEventArgs>? GameLaunchFailed;

    /// <summary>Disparado quando o jogo termina — por remoção do suporte, ou por ter saído sozinho.</summary>
    public event EventHandler<GameStoppedEventArgs>? GameStopped;

    private void OnMediaInserted(object? sender, MediaInsertedEventArgs e) => _ = HandleMediaInsertedAsync(e.DriveRoot, e.Config);

    private async Task HandleMediaInsertedAsync(string driveRoot, GameConfig config)
    {
        var cts = new CancellationTokenSource();
        lock (_lock)
        {
            _pendingLaunches[driveRoot] = cts;
        }

        try
        {
            LaunchStarting?.Invoke(this, new GameLaunchStartingEventArgs(driveRoot, config));

            if (config.LaunchDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(config.LaunchDelaySeconds), cts.Token);
            }

            _logger.Information(
                "A lançar {Title} (plataforma {Platform}).", config.Title, config.Platform);
            _launcher.Launch(config);

            var process = await _processGateway.WaitForProcessAsync(
                config.Process, TimeSpan.FromSeconds(config.WatchTimeoutSeconds), cts.Token);

            if (process is null)
            {
                _logger.Warning(
                    "Timeout à espera de {Process} para {Title} em {Drive}.", config.Process, config.Title, driveRoot);
                GameLaunchFailed?.Invoke(
                    this, new GameLaunchFailedEventArgs(driveRoot, config, "Tempo esgotado à espera do processo do jogo."));
                return;
            }

            lock (_lock)
            {
                _sessions[driveRoot] = new GameSession(config, process);
            }

            process.Exited += (_, _) => OnGameProcessExited(driveRoot);

            _logger.Information("{Title} confirmado em execução (PID {Pid}) em {Drive}.", config.Title, process.Id, driveRoot);
            GameLaunched?.Invoke(this, new GameLaunchedEventArgs(driveRoot, config));
        }
        catch (OperationCanceledException)
        {
            _logger.Information(
                "Lançamento de {Title} cancelado — disquete removida antes de confirmar o arranque.", config.Title);
            GameLaunchFailed?.Invoke(
                this, new GameLaunchFailedEventArgs(driveRoot, config, "Disquete removida antes de confirmar o arranque."));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Falha inesperada ao lançar {Title} em {Drive}.", config.Title, driveRoot);
            GameLaunchFailed?.Invoke(this, new GameLaunchFailedEventArgs(driveRoot, config, "Erro inesperado ao lançar o jogo."));
        }
        finally
        {
            lock (_lock)
            {
                _pendingLaunches.Remove(driveRoot);
            }

            cts.Dispose();
        }
    }

    private void OnGameProcessExited(string driveRoot)
    {
        GameSession? session;
        lock (_lock)
        {
            if (!_sessions.Remove(driveRoot, out session))
            {
                return;
            }
        }

        _logger.Information("{Title} terminou por si próprio.", session.Config.Title);
        GameStopped?.Invoke(this, new GameStoppedEventArgs(driveRoot, session.Config));
    }

    private void OnMediaRemoved(object? sender, MediaRemovedEventArgs e) => _ = HandleMediaRemovedAsync(e.DriveRoot, e.Config);

    private async Task HandleMediaRemovedAsync(string driveRoot, GameConfig config)
    {
        CancellationTokenSource? pending;
        GameSession? session;

        lock (_lock)
        {
            _pendingLaunches.TryGetValue(driveRoot, out pending);
            _sessions.Remove(driveRoot, out session);
        }

        // Disquete removida antes de o lançamento em curso confirmar o processo: cancela-o.
        pending?.Cancel();

        if (session is null || session.Process.HasExited)
        {
            return;
        }

        _logger.Information(
            "A terminar {Title} (processo {Process}) após remoção do suporte em {Drive}.",
            config.Title, config.Process, driveRoot);

        await _processGateway.TerminateAsync(session.Process, config.GracefulShutdown, CancellationToken.None);

        GameStopped?.Invoke(this, new GameStoppedEventArgs(driveRoot, config));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _mediaService.MediaInserted -= OnMediaInserted;
        _mediaService.MediaRemoved -= OnMediaRemoved;

        lock (_lock)
        {
            foreach (var cts in _pendingLaunches.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }

            _pendingLaunches.Clear();
        }
    }
}
