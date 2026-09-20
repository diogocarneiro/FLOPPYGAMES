namespace FloppyGames.Core.Media;

/// <summary>
/// Funde várias fontes de deteção de mídia (ex.: eventos WMI para pens USB +
/// sondagem dedicada para disquetes) num único <see cref="IRemovableMediaWatcher"/>,
/// para que <see cref="RemovableGameMediaService"/> não precise de saber quantas
/// fontes existem nem como cada uma deteta a sua mídia.
/// </summary>
public sealed class CompositeRemovableMediaWatcher : IRemovableMediaWatcher
{
    private readonly IReadOnlyList<IRemovableMediaWatcher> _watchers;

    public CompositeRemovableMediaWatcher(IEnumerable<IRemovableMediaWatcher> watchers)
    {
        _watchers = watchers.ToArray();

        foreach (var watcher in _watchers)
        {
            watcher.DriveArrived += (_, driveRoot) => DriveArrived?.Invoke(this, driveRoot);
            watcher.DriveRemoved += (_, driveRoot) => DriveRemoved?.Invoke(this, driveRoot);
        }
    }

    public event EventHandler<string>? DriveArrived;

    public event EventHandler<string>? DriveRemoved;

    public void Start()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Start();
        }
    }

    public void Stop()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Stop();
        }
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }
    }
}
