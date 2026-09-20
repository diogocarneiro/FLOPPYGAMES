using System.Diagnostics;

namespace FloppyGames.Core.Launch;

/// <summary>Adapta um <see cref="Process"/> real ao contrato <see cref="IManagedProcess"/>.</summary>
public sealed class Win32ManagedProcess : IManagedProcess, IDisposable
{
    public Win32ManagedProcess(Process process)
    {
        Process = process;
        Process.EnableRaisingEvents = true;
        Process.Exited += (_, e) => Exited?.Invoke(this, e);
    }

    public Process Process { get; }

    public int Id => Process.Id;

    public bool HasExited => Process.HasExited;

    public event EventHandler? Exited;

    public void Dispose() => Process.Dispose();
}
