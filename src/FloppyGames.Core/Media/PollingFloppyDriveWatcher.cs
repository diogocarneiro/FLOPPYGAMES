using System.Threading;
using Serilog;

namespace FloppyGames.Core.Media;

/// <summary>
/// Deteta a troca de disco numa drive de disquetes já ligada (ex.: A:\), algo que o Windows
/// não notifica de forma fiável por eventos — a letra de unidade mantém-se atribuída enquanto
/// a drive estiver ligada, mudando apenas o estado "pronta" consoante haja ou não disco lá dentro.
/// Por isso, ao contrário de <see cref="WmiRemovableMediaWatcher"/>, esta implementação sonda
/// ativamente (a um intervalo modesto) um conjunto limitado de letras candidatas.
/// </summary>
public sealed class PollingFloppyDriveWatcher : IRemovableMediaWatcher
{
    public static readonly IReadOnlyList<string> DefaultCandidateDriveRoots = ["A:\\", "B:\\"];

    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(1.5);

    private readonly IReadOnlyList<string> _candidateDriveRoots;
    private readonly IDriveReadinessProbe _probe;
    private readonly TimeSpan _pollInterval;
    private readonly ILogger _logger;
    private readonly HashSet<string> _mounted = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _pollLock = new();
    private Timer? _timer;

    public PollingFloppyDriveWatcher(
        IReadOnlyList<string> candidateDriveRoots,
        IDriveReadinessProbe probe,
        ILogger logger,
        TimeSpan? pollInterval = null)
    {
        _candidateDriveRoots = candidateDriveRoots;
        _probe = probe;
        _logger = logger.ForContext<PollingFloppyDriveWatcher>();
        _pollInterval = pollInterval ?? DefaultPollInterval;
    }

    public event EventHandler<string>? DriveArrived;

    public event EventHandler<string>? DriveRemoved;

    public void Start()
    {
        if (_timer is not null)
        {
            return;
        }

        _logger.Debug(
            "A sondar candidatos a unidade de disquete a cada {IntervalMs} ms: {Drives}",
            _pollInterval.TotalMilliseconds, string.Join(", ", _candidateDriveRoots));

        _timer = new Timer(_ => PollNow(), null, TimeSpan.Zero, _pollInterval);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>
    /// Executa um ciclo de sondagem imediatamente. Público para permitir testes determinísticos
    /// sem depender do temporizador; o temporizador interno chama o mesmo método.
    /// </summary>
    /// <remarks>
    /// <see cref="Timer"/> não garante que os seus callbacks não se sobreponham — se um ciclo
    /// demorar mais do que o intervalo, o próximo pode arrancar antes do anterior terminar.
    /// O <see cref="_pollLock"/> serializa os ciclos para que a transição de estado em
    /// <see cref="_mounted"/> nunca seja lida e escrita por duas execuções em simultâneo
    /// (o que duplicaria eventos <see cref="DriveArrived"/>/<see cref="DriveRemoved"/>).
    /// </remarks>
    public void PollNow()
    {
        lock (_pollLock)
        {
            foreach (var root in _candidateDriveRoots)
            {
                bool ready;

                try
                {
                    ready = _probe.IsReady(root);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Falha ao sondar {Drive} — a ignorar este ciclo.", root);
                    continue;
                }

                var wasMounted = _mounted.Contains(root);

                if (ready && !wasMounted)
                {
                    _mounted.Add(root);
                    _logger.Debug("Disco de disquete detetado em {Drive}.", root);
                    DriveArrived?.Invoke(this, root);
                }
                else if (!ready && wasMounted)
                {
                    _mounted.Remove(root);
                    _logger.Debug("Disco de disquete removido de {Drive}.", root);
                    DriveRemoved?.Invoke(this, root);
                }
            }
        }
    }

    public void Dispose() => Stop();
}
