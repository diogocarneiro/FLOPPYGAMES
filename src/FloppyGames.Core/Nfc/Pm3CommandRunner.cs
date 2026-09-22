using System.Diagnostics;

namespace FloppyGames.Core.Nfc;

/// <summary>
/// Invoca o cliente Proxmark3 como processo externo (<c>proxmark3.exe -p &lt;porta&gt; -c
/// "&lt;comando&gt;"</c>) e devolve o texto combinado de stdout — nunca liga (link) o executável
/// GPL ao FloppyGames, só o corre como uma ferramenta separada (ver <c>tools/proxmark3/NOTICE.md</c>).
///
/// O Windows abre uma porta COM em modo exclusivo (sem partilha) — duas chamadas simultâneas à
/// mesma porta (ex. o <see cref="PollingNfcCardWatcher"/> em segundo plano, a sondar a cada
/// segundo, sobreposto a um <c>Check</c>/<c>Write</c>/<c>WriteMagic</c> deliberado no mesmo
/// instante) fazem uma delas falhar com "invalid serial port" — não é uma falha do cartão nem do
/// leitor, é uma colisão de acesso. Verificado ao vivo nesta sessão: os falsos "falha de
/// autenticação"/"cartão não é magic" desapareceram por completo depois de serializar aqui.
/// Por isso todas as chamadas à MESMA porta, de qualquer classe Pm3*, passam por um lock
/// exclusivo por porta — nunca duas em simultâneo.
/// </summary>
public static class Pm3CommandRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lock> PortLocks = new(StringComparer.OrdinalIgnoreCase);

    public static string Run(string executablePath, string comPort, string command, TimeSpan? timeout = null)
    {
        var portLock = PortLocks.GetOrAdd(comPort, static _ => new Lock());

        lock (portLock)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                ArgumentList = { "-p", comPort, "-c", command },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Não foi possível iniciar {executablePath}.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit((int)(timeout ?? DefaultTimeout).TotalMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // Já tinha terminado entre o WaitForExit e o Kill.
                }

                throw new TimeoutException($"O comando Proxmark3 '{command}' não respondeu a tempo.");
            }

            return stdoutTask.GetAwaiter().GetResult() + stderrTask.GetAwaiter().GetResult();
        }
    }
}
