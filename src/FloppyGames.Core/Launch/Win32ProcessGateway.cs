using System.Diagnostics;
using System.IO;

namespace FloppyGames.Core.Launch;

/// <summary>
/// Implementação real de <see cref="IProcessGateway"/>. A espera pelo arranque do processo
/// é feita por sondagem limitada no tempo (não há forma leve de subscrever "processo X arrancou"
/// no Windows sem WMI com latência própria) — mas, uma vez encontrado, a deteção de saída
/// passa a ser inteiramente orientada a eventos via <see cref="Process.Exited"/>.
/// </summary>
public sealed class Win32ProcessGateway : IProcessGateway
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan GracefulShutdownGrace = TimeSpan.FromSeconds(5);

    public async Task<IManagedProcess?> WaitForProcessAsync(string processName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var bareName = Path.GetFileNameWithoutExtension(processName);
        var deadline = DateTime.UtcNow + timeout;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidates = Process.GetProcessesByName(bareName);
            if (candidates.Length > 0)
            {
                for (var i = 1; i < candidates.Length; i++)
                {
                    candidates[i].Dispose();
                }

                return new Win32ManagedProcess(candidates[0]);
            }

            if (DateTime.UtcNow >= deadline)
            {
                return null;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    public async Task TerminateAsync(IManagedProcess process, bool graceful, CancellationToken cancellationToken)
    {
        if (process is not Win32ManagedProcess { Process: var native })
        {
            return;
        }

        try
        {
            if (native.HasExited)
            {
                return;
            }

            if (graceful)
            {
                native.CloseMainWindow();

                using var timeoutCts = new CancellationTokenSource(GracefulShutdownGrace);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                try
                {
                    await native.WaitForExitAsync(linkedCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // O encerramento suave não terminou a tempo — cai para o kill abaixo.
                }
            }

            if (!native.HasExited)
            {
                native.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // O processo já tinha terminado entre a verificação e a ação — nada a fazer.
        }
    }
}
