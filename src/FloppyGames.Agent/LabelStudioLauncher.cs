using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace FloppyGames.Agent;

/// <summary>Tenta abrir o FloppyGames Label Studio, instalado na mesma pasta que o Agent.</summary>
internal static class LabelStudioLauncher
{
    private const string ExecutableName = "FloppyGames.LabelStudio.exe";

    public static void TryLaunch()
    {
        var agentPath = Environment.ProcessPath;
        var agentFolder = agentPath is null ? null : Path.GetDirectoryName(agentPath);

        foreach (var candidate in GetCandidatePaths(agentFolder))
        {
            if (File.Exists(candidate))
            {
                Process.Start(new ProcessStartInfo(candidate) { UseShellExecute = true });
                return;
            }
        }

        System.Windows.MessageBox.Show(
            "O FloppyGames Label Studio ainda não está instalado nesta pasta.",
            "FloppyGames",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    /// <summary>
    /// O instalador coloca o Agent e o Label Studio em pastas irmãs (ex.: "{app}\Agent" e
    /// "{app}\LabelStudio"), não na mesma pasta — por isso a pasta-irmã é a primeira tentativa.
    /// A própria pasta do Agent fica como alternativa para cenários de build/execução manual.
    /// </summary>
    private static IEnumerable<string> GetCandidatePaths(string? agentFolder)
    {
        if (agentFolder is null)
        {
            yield break;
        }

        var appFolder = Path.GetDirectoryName(agentFolder);
        if (appFolder is not null)
        {
            yield return Path.Combine(appFolder, "LabelStudio", ExecutableName);
        }

        yield return Path.Combine(agentFolder, ExecutableName);
    }
}
