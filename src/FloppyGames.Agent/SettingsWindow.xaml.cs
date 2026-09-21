using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Navigation;
using FloppyGames.Core.Localization;
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

        AutostartCheckBox.Content = Strings.Settings_Autostart;
        FloppySoundCheckBox.Content = Strings.Settings_FloppySound;
        CrtEffectCheckBox.Content = Strings.Settings_CrtEffect;
        LanguageLabelText.Text = Strings.Settings_LanguageLabel;
        LanguageRestartNoteText.Text = Strings.Settings_LanguageRestartNote;
        LogsFolderLabelText.Text = Strings.Settings_LogsFolder;
        OpenLogsButton.Content = Strings.Settings_OpenButton;
        SteamApiKeyLabelText.Text = Strings.Settings_SteamApiKeyLabel;
        SteamApiKeyDescriptionText.Text = Strings.Settings_SteamApiKeyDescription;
        SaveApiKeyButton.Content = Strings.Settings_SaveButton;
        ApiKeyLink.Inlines.Add(Strings.Settings_GetApiKeyLink);

        _autostartManager = new AutostartManager(
            new WindowsAutostartRegistry(), Environment.ProcessPath ?? "FloppyGames.Agent.exe");
        _settingsStore = new AgentSettingsStore();

        LogsPathText.Text = LoggingBootstrapper.LogDirectory;
        var settings = _settingsStore.Load();
        SteamWebApiKeyBox.Text = settings.SteamWebApiKey ?? string.Empty;

        LanguageCombo.ItemsSource = SupportedLanguages.All;
        LanguageCombo.DisplayMemberPath = "NativeName";

        _initializing = true;
        AutostartCheckBox.IsChecked = _autostartManager.IsEnabled;
        FloppySoundCheckBox.IsChecked = settings.PlayFloppySound;
        CrtEffectCheckBox.IsChecked = settings.CrtEffectEnabled;
        LanguageCombo.SelectedItem = SupportedLanguages.All.FirstOrDefault(l => l.Code == settings.Language)
            ?? SupportedLanguages.All[0];
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
            StatusText.Text = Strings.Settings_AutostartEnabled;
        }
        else
        {
            _autostartManager.Disable();
            StatusText.Text = Strings.Settings_AutostartDisabled;
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
            ? Strings.Settings_ApiKeyRemoved
            : Strings.Settings_ApiKeySaved;
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
            ? Strings.Settings_FloppySoundEnabled
            : Strings.Settings_FloppySoundDisabled;
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
            ? Strings.Settings_CrtEffectEnabled
            : Strings.Settings_CrtEffectDisabled;
    }

    private void OnLanguageChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_initializing || LanguageCombo.SelectedItem is not LanguageOption selected)
        {
            return;
        }

        var current = _settingsStore.Load();
        _settingsStore.Save(current with { Language = selected.Code });
        StatusText.Text = Strings.Settings_LanguageRestartNote;
    }

    private void OnApiKeyLinkClicked(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
