using System.Diagnostics;
using System.IO;
using System.Windows;
using FloppyGames.Core.Logging;
using FloppyGames.Core.Startup;

namespace FloppyGames.Agent;

public partial class SettingsWindow : Window
{
    private readonly AutostartManager _autostartManager;
    private readonly bool _initializing;

    public SettingsWindow()
    {
        InitializeComponent();

        _autostartManager = new AutostartManager(
            new WindowsAutostartRegistry(), Environment.ProcessPath ?? "FloppyGames.Agent.exe");

        LogsPathText.Text = LoggingBootstrapper.LogDirectory;

        _initializing = true;
        AutostartCheckBox.IsChecked = _autostartManager.IsEnabled;
        _initializing = false;
    }

    private void OnAutostartToggled(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        if (AutostartCheckBox.IsChecked == true)
        {
            _autostartManager.Enable();
            StatusText.Text = "Arranque automático ativado.";
        }
        else
        {
            _autostartManager.Disable();
            StatusText.Text = "Arranque automático desativado.";
        }
    }

    private void OnOpenLogsClicked(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(LoggingBootstrapper.LogDirectory);
        Process.Start(new ProcessStartInfo(LoggingBootstrapper.LogDirectory) { UseShellExecute = true });
    }
}
