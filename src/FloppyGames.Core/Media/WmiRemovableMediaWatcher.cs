using System.Management;
using Serilog;

namespace FloppyGames.Core.Media;

/// <summary>
/// Implementação de <see cref="IRemovableMediaWatcher"/> baseada em WMI
/// (Win32_VolumeChangeEvent), reagindo a eventos em vez de sondar unidades periodicamente.
/// </summary>
public sealed class WmiRemovableMediaWatcher : IRemovableMediaWatcher
{
    private const int DeviceArrivalEventType = 2;
    private const int DeviceRemovalEventType = 3;

    private readonly ILogger _logger;
    private ManagementEventWatcher? _watcher;

    public WmiRemovableMediaWatcher(ILogger logger)
    {
        _logger = logger.ForContext<WmiRemovableMediaWatcher>();
    }

    public event EventHandler<string>? DriveArrived;

    public event EventHandler<string>? DriveRemoved;

    public void Start()
    {
        if (_watcher is not null)
        {
            return;
        }

        var query = new WqlEventQuery(
            "SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2 OR EventType = 3");

        _watcher = new ManagementEventWatcher(query);
        _watcher.EventArrived += OnEventArrived;
        _watcher.Start();

        _logger.Debug("A vigiar volumes via WMI (Win32_VolumeChangeEvent).");
    }

    public void Stop()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.EventArrived -= OnEventArrived;
        _watcher.Stop();
        _watcher.Dispose();
        _watcher = null;

        _logger.Debug("Vigilância de volumes WMI parada.");
    }

    private void OnEventArrived(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var driveName = e.NewEvent.Properties["DriveName"]?.Value as string;
            if (string.IsNullOrWhiteSpace(driveName))
            {
                return;
            }

            var eventType = Convert.ToInt32(e.NewEvent.Properties["EventType"]?.Value);
            var driveRoot = driveName.EndsWith('\\') ? driveName : driveName + "\\";

            switch (eventType)
            {
                case DeviceArrivalEventType:
                    _logger.Debug("Volume detetado: {Drive}", driveRoot);
                    DriveArrived?.Invoke(this, driveRoot);
                    break;

                case DeviceRemovalEventType:
                    _logger.Debug("Volume removido: {Drive}", driveRoot);
                    DriveRemoved?.Invoke(this, driveRoot);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Falha ao processar evento WMI de mudança de volume.");
        }
    }

    public void Dispose() => Stop();
}
