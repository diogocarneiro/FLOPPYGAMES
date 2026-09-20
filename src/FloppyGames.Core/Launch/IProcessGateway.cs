namespace FloppyGames.Core.Launch;

/// <summary>
/// Abstrai as operações de processo necessárias ao ciclo de vida de um jogo:
/// esperar que arranque, e terminá-lo (com tentativa de encerramento suave primeiro).
/// </summary>
public interface IProcessGateway
{
    /// <summary>
    /// Sonda a lista de processos até encontrar um com o nome indicado, ou até expirar o timeout.
    /// Devolve <see langword="null"/> em caso de timeout.
    /// </summary>
    public Task<IManagedProcess?> WaitForProcessAsync(string processName, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>
    /// Termina o processo. Se <paramref name="graceful"/>, tenta primeiro um encerramento suave
    /// (fechar a janela principal) antes de forçar a terminação.
    /// </summary>
    public Task TerminateAsync(IManagedProcess process, bool graceful, CancellationToken cancellationToken);
}
