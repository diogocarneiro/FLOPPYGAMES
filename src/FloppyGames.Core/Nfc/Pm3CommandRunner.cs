using System.Diagnostics;

namespace FloppyGames.Core.Nfc;

/// <summary>
/// Invoca o cliente Proxmark3 como processo externo (<c>proxmark3.exe -p &lt;porta&gt; -c
/// "&lt;comando&gt;"</c>) e devolve o texto combinado de stdout — nunca liga (link) o executável
/// GPL ao FloppyGames, só o corre como uma ferramenta separada (ver <c>tools/proxmark3/NOTICE.md</c>).
/// </summary>
public static class Pm3CommandRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    public static string Run(string executablePath, string comPort, string command, TimeSpan? timeout = null)
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
