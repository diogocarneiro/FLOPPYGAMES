using FloppyGames.Core.Launch;

namespace FloppyGames.Core.Tests.Launch;

/// <summary>
/// Dublê de teste de <see cref="IProcessGateway"/>. Por omissão devolve <see cref="ProcessToReturn"/>
/// imediatamente (ignora o timeout, para os testes não terem de esperar tempo real).
/// </summary>
internal sealed class FakeProcessGateway : IProcessGateway
{
    public IManagedProcess? ProcessToReturn { get; set; }

    public List<(IManagedProcess Process, bool Graceful)> TerminateCalls { get; } = new();

    public Task<IManagedProcess?> WaitForProcessAsync(string processName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ProcessToReturn);
    }

    public Task TerminateAsync(IManagedProcess process, bool graceful, CancellationToken cancellationToken)
    {
        TerminateCalls.Add((process, graceful));

        if (process is FakeManagedProcess fake)
        {
            fake.SimulateExit();
        }

        return Task.CompletedTask;
    }
}
