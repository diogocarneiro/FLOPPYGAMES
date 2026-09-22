using FloppyGames.Core.Configuration;
using FloppyGames.Core.Nfc;
using Serilog;

namespace FloppyGames.Core.Launch;

/// <summary>
/// Orquestra o ciclo de vida de um jogo a partir dos eventos de um cartão NFC: atraso de
/// lançamento, invocação do launcher da plataforma, confirmação de arranque, e encerramento
/// automático quando o cartão é afastado do leitor. Espelha <see cref="GameSessionManager"/>
/// para disquetes/pens — mantido como uma classe paralela e não uma reutilização direta, porque
/// um cartão não tem um <c>driveRoot</c> real (ver decisão de arquitetura no ROADMAP): é indexado
/// por UID em vez de letra de unidade.
/// </summary>
public sealed class NfcCardSessionManager : IDisposable
{
    private readonly NfcGameMediaService _mediaService;
    private readonly IGameLauncher _launcher;
    private readonly IProcessGateway _processGateway;
    private readonly ILogger _logger;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, GameSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> _pendingLaunches = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public NfcCardSessionManager(
        NfcGameMediaService mediaService, IGameLauncher launcher, IProcessGateway processGateway, ILogger logger)
    {
        _mediaService = mediaService;
        _launcher = launcher;
        _processGateway = processGateway;
        _logger = logger.ForContext<NfcCardSessionManager>();

        _mediaService.CardInserted += OnCardInserted;
        _mediaService.CardRemoved += OnCardRemoved;
    }

    /// <summary>Disparado assim que o cartão é reconhecido, antes do atraso de lançamento — momento certo para mostrar a splash.</summary>
    public event EventHandler<NfcCardLaunchStartingEventArgs>? LaunchStarting;

    /// <summary>Disparado quando o processo do jogo é confirmado em execução.</summary>
    public event EventHandler<NfcCardLaunchedEventArgs>? GameLaunched;

    /// <summary>Disparado quando o timeout expira sem o processo aparecer, ou o lançamento é cancelado (cartão afastado antes de confirmar).</summary>
    public event EventHandler<NfcCardLaunchFailedEventArgs>? GameLaunchFailed;

    /// <summary>Disparado quando o jogo termina — por remoção do cartão, ou por ter saído sozinho.</summary>
    public event EventHandler<NfcCardStoppedEventArgs>? GameStopped;

    private void OnCardInserted(object? sender, CardInsertedEventArgs e) => _ = HandleCardInsertedAsync(e.Uid, e.Config);

    private async Task HandleCardInsertedAsync(string uid, GameConfig config)
    {
        var cts = new CancellationTokenSource();
        lock (_lock)
        {
            _pendingLaunches[uid] = cts;
        }

        try
        {
            LaunchStarting?.Invoke(this, new NfcCardLaunchStartingEventArgs(uid, config));

            if (config.LaunchDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(config.LaunchDelaySeconds), cts.Token);
            }

            _logger.Information("A lançar {Title} (plataforma {Platform}) a partir de um cartão NFC.", config.Title, config.Platform);
            _launcher.Launch(config);

            var process = await _processGateway.WaitForProcessAsync(
                config.Process, TimeSpan.FromSeconds(config.WatchTimeoutSeconds), cts.Token);

            if (process is null)
            {
                _logger.Warning(
                    "Timeout à espera de {Process} para {Title} (cartão NFC UID {Uid}).", config.Process, config.Title, uid);
                GameLaunchFailed?.Invoke(
                    this, new NfcCardLaunchFailedEventArgs(uid, config, GameLaunchFailureReason.Timeout));
                return;
            }

            lock (_lock)
            {
                _sessions[uid] = new GameSession(config, process);
            }

            process.Exited += (_, _) => OnGameProcessExited(uid);

            _logger.Information("{Title} confirmado em execução (PID {Pid}) via cartão NFC UID {Uid}.", config.Title, process.Id, uid);
            GameLaunched?.Invoke(this, new NfcCardLaunchedEventArgs(uid, config));
        }
        catch (OperationCanceledException)
        {
            _logger.Information(
                "Lançamento de {Title} cancelado — cartão NFC afastado antes de confirmar o arranque.", config.Title);
            GameLaunchFailed?.Invoke(
                this, new NfcCardLaunchFailedEventArgs(uid, config, GameLaunchFailureReason.MediaRemovedDuringLaunch));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Falha inesperada ao lançar {Title} via cartão NFC UID {Uid}.", config.Title, uid);
            GameLaunchFailed?.Invoke(
                this, new NfcCardLaunchFailedEventArgs(uid, config, GameLaunchFailureReason.UnexpectedError));
        }
        finally
        {
            lock (_lock)
            {
                _pendingLaunches.Remove(uid);
            }

            cts.Dispose();
        }
    }

    private void OnGameProcessExited(string uid)
    {
        GameSession? session;
        lock (_lock)
        {
            if (!_sessions.Remove(uid, out session))
            {
                return;
            }
        }

        _logger.Information("{Title} terminou por si próprio.", session.Config.Title);
        GameStopped?.Invoke(this, new NfcCardStoppedEventArgs(uid, session.Config));
    }

    private void OnCardRemoved(object? sender, CardRemovedEventArgs e) => _ = HandleCardRemovedAsync(e.Uid, e.Config);

    private async Task HandleCardRemovedAsync(string uid, GameConfig config)
    {
        CancellationTokenSource? pending;
        GameSession? session;

        lock (_lock)
        {
            _pendingLaunches.TryGetValue(uid, out pending);
            _sessions.Remove(uid, out session);
        }

        // Cartão afastado antes de o lançamento em curso confirmar o processo: cancela-o.
        pending?.Cancel();

        if (session is null || session.Process.HasExited)
        {
            return;
        }

        _logger.Information(
            "A terminar {Title} (processo {Process}) após remoção do cartão NFC UID {Uid}.",
            config.Title, config.Process, uid);

        await _processGateway.TerminateAsync(session.Process, config.GracefulShutdown, CancellationToken.None);

        GameStopped?.Invoke(this, new NfcCardStoppedEventArgs(uid, config));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _mediaService.CardInserted -= OnCardInserted;
        _mediaService.CardRemoved -= OnCardRemoved;

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
