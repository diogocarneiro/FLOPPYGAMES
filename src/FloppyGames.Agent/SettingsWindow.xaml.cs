using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Navigation;
using FloppyGames.Core;
using FloppyGames.Core.Localization;
using FloppyGames.Core.Logging;
using FloppyGames.Core.Settings;
using FloppyGames.Core.Startup;
using FloppyGames.Core.Updates;

namespace FloppyGames.Agent;

public partial class SettingsWindow : Window
{
    private readonly AutostartManager _autostartManager;
    private readonly AgentSettingsStore _settingsStore;
    private readonly TrayIconController _trayIconController;
    private readonly GitHubReleaseUpdateChecker _updateChecker = new();
    private readonly bool _initializing;
    private AvailableUpdate? _pendingUpdate;

    public SettingsWindow(TrayIconController trayIconController)
    {
        InitializeComponent();
        _trayIconController = trayIconController;

        AutostartCheckBox.Content = Strings.Settings_Autostart;
        FloppySoundCheckBox.Content = Strings.Settings_FloppySound;
        CrtEffectCheckBox.Content = Strings.Settings_CrtEffect;
        LanguageLabelText.Text = Strings.Settings_LanguageLabel;
        LanguageRestartNoteText.Text = Strings.Settings_LanguageRestartNote;
        PlatformsLabelText.Text = Strings.Settings_PlatformsLabel;
        PlatformsDescriptionText.Text = Strings.Settings_PlatformsDescription;
        NfcEnabledCheckBox.Content = Strings.Settings_NfcEnabled;
        NfcDescriptionText.Text = Strings.Settings_NfcDescription;
        NfcCardPasswordLabelText.Text = Strings.Settings_NfcCardPasswordLabel;
        NfcCardPasswordDescriptionText.Text = Strings.Settings_NfcCardPasswordDescription;
        SaveNfcCardPasswordButton.Content = Strings.Settings_SaveButton;
        UpdatesLabelText.Text = Strings.Settings_UpdatesLabel;
        UpdatesDescriptionText.Text = Strings.Settings_UpdatesDescription;
        CheckForUpdatesCheckBox.Content = Strings.Settings_CheckForUpdatesCheckbox;
        CheckForUpdatesNowButton.Content = Strings.Settings_CheckForUpdatesNowButton;
        InstallUpdateButton.Content = Strings.Settings_InstallUpdateButton;
        AboutLabelText.Text = Strings.Settings_AboutLabel;
        AboutDescriptionText.Text = Strings.Settings_AboutDescription;
        AboutCreatedByText.Text = Strings.Settings_AboutCreatedBy;
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
        NfcCardPasswordBox.Password = settings.NfcCardPassword ?? string.Empty;

        LanguageCombo.ItemsSource = SupportedLanguages.All;
        LanguageCombo.DisplayMemberPath = "NativeName";

        _initializing = true;
        AutostartCheckBox.IsChecked = _autostartManager.IsEnabled;
        FloppySoundCheckBox.IsChecked = settings.PlayFloppySound;
        CrtEffectCheckBox.IsChecked = settings.CrtEffectEnabled;
        LanguageCombo.SelectedItem = SupportedLanguages.All.FirstOrDefault(l => l.Code == settings.Language)
            ?? SupportedLanguages.All[0];
        SteamEnabledCheckBox.IsChecked = settings.SteamEnabled;
        EpicEnabledCheckBox.IsChecked = settings.EpicEnabled;
        GogEnabledCheckBox.IsChecked = settings.GogEnabled;
        NfcEnabledCheckBox.IsChecked = settings.NfcEnabled;
        CheckForUpdatesCheckBox.IsChecked = settings.CheckForUpdatesEnabled;
        _initializing = false;

        // Se o TrayIconController já encontrou uma atualização ao arrancar, mostra-a de imediato
        // em vez de obrigar a clicar em "Verificar agora" outra vez.
        if (_trayIconController.PendingUpdate is { } preloaded)
        {
            ShowUpdateAvailable(preloaded);
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e) => WindowPlacement.FitToWorkArea(this);

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

    private void OnSaveNfcCardPasswordClicked(object sender, RoutedEventArgs e)
    {
        var password = NfcCardPasswordBox.Password.Trim();
        var current = _settingsStore.Load();
        _settingsStore.Save(current with { NfcCardPassword = string.IsNullOrWhiteSpace(password) ? null : password });
        StatusText.Text = string.IsNullOrWhiteSpace(password)
            ? Strings.Settings_NfcCardPasswordRemoved
            : Strings.Settings_NfcCardPasswordSaved;
    }

    private void OnCheckForUpdatesToggled(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        var current = _settingsStore.Load();
        _settingsStore.Save(current with { CheckForUpdatesEnabled = CheckForUpdatesCheckBox.IsChecked == true });
        StatusText.Text = CheckForUpdatesCheckBox.IsChecked == true
            ? Strings.Settings_CheckForUpdatesEnabledOn
            : Strings.Settings_CheckForUpdatesEnabledOff;
    }

    private async void OnCheckForUpdatesNowClicked(object sender, RoutedEventArgs e)
    {
        CheckForUpdatesNowButton.IsEnabled = false;
        InstallUpdateButton.Visibility = Visibility.Collapsed;
        _pendingUpdate = null;
        UpdateStatusText.Text = Strings.Settings_UpdateStatusChecking;

        try
        {
            var result = await _updateChecker.CheckAsync(AppInfo.Version, CancellationToken.None);
            switch (result.Status)
            {
                case UpdateCheckStatus.UpdateAvailable:
                    ShowUpdateAvailable(result.Update!);
                    break;
                case UpdateCheckStatus.UpToDate:
                    UpdateStatusText.Text = Strings.Settings_UpdateStatusUpToDate(AppInfo.Version);
                    break;
                default:
                    UpdateStatusText.Text = Strings.Settings_UpdateStatusFailed;
                    break;
            }
        }
        finally
        {
            CheckForUpdatesNowButton.IsEnabled = true;
        }
    }

    private void ShowUpdateAvailable(AvailableUpdate update)
    {
        _pendingUpdate = update;
        UpdateStatusText.Text = Strings.Settings_UpdateStatusAvailable(update.Version.ToString());
        InstallUpdateButton.Visibility = update.InstallerUrl is not null ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Descarrega o instalador anexado à release e lança-o em modo silencioso (sem assistente,
    /// sem reiniciar o PC — mesmo comportamento da atualização automática ao arrancar) — depois
    /// pede ao Agent para se fechar de forma limpa, porque o instalador precisa de substituir o
    /// próprio executável em execução; o Agent reabre sozinho no fim, minimizado à bandeja.
    /// </summary>
    private async void OnInstallUpdateClicked(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate is null)
        {
            return;
        }

        InstallUpdateButton.IsEnabled = false;
        CheckForUpdatesNowButton.IsEnabled = false;

        try
        {
            var progress = new Progress<double>(p => UpdateStatusText.Text = Strings.Settings_UpdateDownloading((int)(p * 100)));
            var installerPath = await _updateChecker.DownloadInstallerAsync(_pendingUpdate, progress, CancellationToken.None);

            Process.Start(new ProcessStartInfo(installerPath, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = true });
            UpdateStatusText.Text = Strings.Settings_UpdateInstallStarting;
            _trayIconController.RequestExit();
            Close();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or Win32Exception)
        {
            UpdateStatusText.Text = Strings.Settings_UpdateDownloadFailed;
            InstallUpdateButton.IsEnabled = true;
            CheckForUpdatesNowButton.IsEnabled = true;
        }
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

    private void OnSteamEnabledToggled(object sender, RoutedEventArgs e) =>
        OnPlatformToggled("Steam", SteamEnabledCheckBox.IsChecked == true, (settings, enabled) => settings with { SteamEnabled = enabled });

    private void OnEpicEnabledToggled(object sender, RoutedEventArgs e) =>
        OnPlatformToggled("Epic Games", EpicEnabledCheckBox.IsChecked == true, (settings, enabled) => settings with { EpicEnabled = enabled });

    private void OnGogEnabledToggled(object sender, RoutedEventArgs e) =>
        OnPlatformToggled("GOG", GogEnabledCheckBox.IsChecked == true, (settings, enabled) => settings with { GogEnabled = enabled });

    private void OnNfcEnabledToggled(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        var current = _settingsStore.Load();
        _settingsStore.Save(current with { NfcEnabled = NfcEnabledCheckBox.IsChecked == true });
        StatusText.Text = NfcEnabledCheckBox.IsChecked == true
            ? Strings.Settings_NfcEnabledOn
            : Strings.Settings_NfcEnabledOff;
    }

    private void OnPlatformToggled(string platformName, bool enabled, Func<AgentSettings, bool, AgentSettings> apply)
    {
        if (_initializing)
        {
            return;
        }

        var current = _settingsStore.Load();
        _settingsStore.Save(apply(current, enabled));
        StatusText.Text = enabled
            ? Strings.Settings_PlatformEnabled(platformName)
            : Strings.Settings_PlatformDisabled(platformName);
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
