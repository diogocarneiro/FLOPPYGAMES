using System.Windows;

namespace FloppyGames.Agent;

internal static class WindowPlacement
{
    /// <summary>
    /// Reduz a janela ao que cabe na área útil do ecrã (sem a barra de tarefas) e volta a centrá-la
    /// — o tamanho definido no XAML é o ideal, mas num portátil de 1366x768 as janelas maiores
    /// ficariam cortadas (a de Definições nem sequer se pode redimensionar).
    /// </summary>
    public static void FitToWorkArea(Window window)
    {
        var workArea = SystemParameters.WorkArea;
        window.Width = Math.Min(window.Width, workArea.Width);
        window.Height = Math.Min(window.Height, workArea.Height);
        window.Left = workArea.Left + ((workArea.Width - window.Width) / 2);
        window.Top = workArea.Top + ((workArea.Height - window.Height) / 2);
    }
}
