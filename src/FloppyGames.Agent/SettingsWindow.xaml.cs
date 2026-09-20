using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Navigation;
using FloppyGames.Core.Logging;
using FloppyGames.Core.Settings;
using FloppyGames.Core.Startup;

namespace FloppyGames.Agent;

public partial class SettingsWindow : Window
{
    private readonly AutostartManager _autostartManager;
    private readonly AgentSettingsStore _settingsStore;
    private readonly bool _initializing;

    public SettingsWindow()
    {
        InitializeComponent();

        _autostartManager = new AutostartManager(
            new WindowsAutostartRegistry(), Environment.ProcessPath ?? "FloppyGames.Agent.exe");
        _settingsStore = new AgentSettingsStore();

        LogsPathText.Text = LoggingBootstrapper.LogDirectory;
        SteamWebApiKeyBox.Text = _settingsStore.Load().SteamWebApiKey ?? string.Empty;

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

    private void OnSaveApiKeyClicked(object sender, RoutedEventArgs e)
    {
        var apiKey = SteamWebApiKeyBox.Text.Trim();
        _settingsStore.Save(new AgentSettings { SteamWebApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey });
        StatusText.Text = string.IsNullOrWhiteSpace(apiKey)
            ? "Chave removida — conquistas deixam de aparecer no ecrã de arranque."
            : "Chave guardada.";
    }

    private void OnApiKeyLinkClicked(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
