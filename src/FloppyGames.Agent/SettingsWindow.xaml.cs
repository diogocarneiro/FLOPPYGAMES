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
        var settings = _settingsStore.Load();
        SteamWebApiKeyBox.Text = settings.SteamWebApiKey ?? string.Empty;

        _initializing = true;
        AutostartCheckBox.IsChecked = _autostartManager.IsEnabled;
        FloppySoundCheckBox.IsChecked = settings.PlayFloppySound;
        CrtEffectCheckBox.IsChecked = settings.CrtEffectEnabled;
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
        var current = _settingsStore.Load();
        _settingsStore.Save(current with { SteamWebApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey });
        StatusText.Text = string.IsNullOrWhiteSpace(apiKey)
            ? "Chave removida — conquistas deixam de aparecer no ecrã de arranque."
            : "Chave guardada.";
    }

    private void OnFloppySoundToggled(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        var current = _settingsStore.Load();
        _settingsStore.Save(current with { PlayFloppySound = FloppySoundCheckBox.IsChecked == true });
        StatusText.Text = FloppySoundCheckBox.IsChecked == true
            ? "Som do motor ativado."
            : "Som do motor desativado.";
    }

    private void OnCrtEffectToggled(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        var current = _settingsStore.Load();
        _settingsStore.Save(current with { CrtEffectEnabled = CrtEffectCheckBox.IsChecked == true });
        StatusText.Text = CrtEffectCheckBox.IsChecked == true
            ? "Efeito CRT ativado."
            : "Efeito CRT desativado.";
    }

    private void OnApiKeyLinkClicked(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
