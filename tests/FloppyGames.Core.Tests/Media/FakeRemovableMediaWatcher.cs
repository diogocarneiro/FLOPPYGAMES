using FloppyGames.Core.Media;

namespace FloppyGames.Core.Tests.Media;

/// <summary>Dublê de teste de <see cref="IRemovableMediaWatcher"/> — dispara eventos sob controlo do teste, sem WMI.</summary>
internal sealed class FakeRemovableMediaWatcher : IRemovableMediaWatcher
{
    public int StartCalls { get; private set; }

    public int StopCalls { get; private set; }

    public event EventHandler<string>? DriveArrived;

    public event EventHandler<string>? DriveRemoved;

    public void Start() => StartCalls++;

    public void Stop() => StopCalls++;

    public void RaiseArrived(string driveRoot) => DriveArrived?.Invoke(this, driveRoot);

    public void RaiseRemoved(string driveRoot) => DriveRemoved?.Invoke(this, driveRoot);

    public void Dispose()
    {
    }
}
