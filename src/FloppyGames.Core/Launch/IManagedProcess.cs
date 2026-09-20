namespace FloppyGames.Core.Launch;

/// <summary>
/// Abstrai um processo do sistema operativo para que <see cref="GameSessionManager"/>
/// seja testável sem depender de processos reais.
/// </summary>
public interface IManagedProcess
{
    public int Id { get; }

    public bool HasExited { get; }

    /// <summary>Disparado quando o processo termina, por si próprio ou por ação externa.</summary>
    public event EventHandler? Exited;
}
