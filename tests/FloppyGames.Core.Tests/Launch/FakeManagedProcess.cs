using FloppyGames.Core.Launch;

namespace FloppyGames.Core.Tests.Launch;

internal sealed class FakeManagedProcess : IManagedProcess
{
    private static int _nextId = 1000;

    public int Id { get; } = _nextId++;

    public bool HasExited { get; private set; }

    public event EventHandler? Exited;

    public void SimulateExit()
    {
        if (HasExited)
        {
            return;
        }

        HasExited = true;
        Exited?.Invoke(this, EventArgs.Empty);
    }
}
