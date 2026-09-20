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
        var candidate = agentFolder is null ? null : Path.Combine(agentFolder, ExecutableName);

        if (candidate is not null && File.Exists(candidate))
        {
            Process.Start(new ProcessStartInfo(candidate) { UseShellExecute = true });
            return;
        }

        System.Windows.MessageBox.Show(
            "O FloppyGames Label Studio ainda não está instalado nesta pasta.",
            "FloppyGames",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }
}
