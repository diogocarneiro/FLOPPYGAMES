using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Forms;
using FloppyGames.Core;
using FloppyGames.Core.Launch;
using FloppyGames.Core.Localization;
using FloppyGames.Core.Logging;
using FloppyGames.Core.Settings;
using FloppyGames.Core.Updates;

namespace FloppyGames.Agent;

/// <summary>
/// Ícone de bandeja do sistema: reflete o estado atual (inativo / a lançar / jogo em execução)
/// a partir dos eventos do <see cref="GameSessionManager"/>, e dá acesso rápido a logs,
/// Label Studio, definições e saída. Também deteta atualizações disponíveis ao arrancar (ver
/// <see cref="CheckForUpdatesOnStartupAsync"/>) e mostra-as aqui — a instalação em si fica na
/// janela de Definições, para haver só um sítio com essa lógica.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly MainWindow _mainWindow;
    private readonly Dictionary<TrayIconState, Icon> _icons;
    private readonly Lock _stateLock = new();
    private readonly GitHubReleaseUpdateChecker _updateChecker = new();
    private readonly ToolStripMenuItem _updateMenuItem;
    private int _activeLaunches;
    private int _activeSessions;

    public TrayIconController(MainWindow mainWindow, GameSessionManager sessionManager)
    {
        _mainWindow = mainWindow;
        _icons = Enum.GetValues<TrayIconState>().ToDictionary(state => state, TrayIconFactory.Create);

        // Só aparece depois de CheckForUpdatesOnStartupAsync encontrar uma versão nova — nunca
        // ocupa espaço no menu quando já se está atualizado ou a verificação está desligada.
        _updateMenuItem = new ToolStripMenuItem(string.Empty, null, (_, _) => ShowSettings()) { Visible = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_updateMenuItem);
        menu.Items.Add(Strings.Tray_OpenFloppyGames, null, (_, _) => ShowMainWindow());
        menu.Items.Add(Strings.Tray_OpenLogsFolder, null, (_, _) => OpenLogsFolder());
        menu.Items.Add(Strings.Tray_OpenLabelStudio, null, (_, _) => LabelStudioLauncher.TryLaunch());
        menu.Items.Add(Strings.Tray_Settings, null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.Tray_Exit, null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new NotifyIcon
        {
            Icon = _icons[TrayIconState.Idle],
            Text = Strings.Tray_Idle,
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
        _notifyIcon.BalloonTipClicked += (_, _) => ShowSettings();

        sessionManager.LaunchStarting += (_, _) => OnLaunchStarting();
        sessionManager.GameLaunched += (_, _) => OnLaunchSettled(succeeded: true);
        sessionManager.GameLaunchFailed += (_, _) => OnLaunchSettled(succeeded: false);
        sessionManager.GameStopped += (_, _) => OnGameStopped();
    }

    public event EventHandler? ExitRequested;

    /// <summary>A atualização encontrada por <see cref="CheckForUpdatesOnStartupAsync"/>, se alguma — lida pela janela de Definições para não repetir a verificação.</summary>
    public AvailableUpdate? PendingUpdate { get; private set; }

    /// <summary>Permite à janela de Definições pedir a saída limpa do Agent depois de lançar o instalador de uma atualização.</summary>
    public void RequestExit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Verifica a existência de uma versão nova ao arrancar, se a opção estiver ativa nas
    /// Definições, e se encontrar uma instala-a sozinha (silenciosa, sem assistente) — nunca
    /// bloqueia o arranque do Agent (é chamada em segundo plano, sem esperar por ela) nem
    /// incomoda se falhar (sem ligação, GitHub em baixo, release sem instalador anexado): fica
    /// só com o item do menu como alternativa manual.
    /// </summary>
    public async Task CheckForUpdatesOnStartupAsync()
    {
        if (!new AgentSettingsStore().Load().CheckForUpdatesEnabled)
        {
            return;
        }

        // Dá tempo ao resto do arranque (vigilância de mídia, pilha NFC) sem competir por rede/CPU.
        await Task.Delay(TimeSpan.FromSeconds(5));

        var result = await _updateChecker.CheckAsync(AppInfo.Version, CancellationToken.None);
        if (result.Status != UpdateCheckStatus.UpdateAvailable)
        {
            return;
        }

        var update = result.Update!;
        PendingUpdate = update;
        var version = update.Version.ToString();

        _updateMenuItem.Text = Strings.Tray_UpdateAvailable(version);
        _updateMenuItem.Visible = true;

        if (update.InstallerUrl is null)
        {
            // A release não tem instalador anexado — não há nada para instalar sozinho; fica só
            // o aviso e o item do menu, que abre as Definições para o utilizador tratar disso.
            _notifyIcon.ShowBalloonTip(10_000, Strings.Tray_UpdateBalloonTitle, Strings.Tray_UpdateAvailable(version), ToolTipIcon.Info);
            return;
        }

        _notifyIcon.ShowBalloonTip(10_000, Strings.Tray_UpdateBalloonTitle, Strings.Tray_UpdateBalloonText(version), ToolTipIcon.Info);

        try
        {
            var installerPath = await _updateChecker.DownloadInstallerAsync(update, progress: null, CancellationToken.None);
            Process.Start(new ProcessStartInfo(installerPath, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = true });
            PendingUpdate = null;
            _updateMenuItem.Visible = false;
            RequestExit();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or System.ComponentModel.Win32Exception)
        {
            // Fica com o item do menu visível (já preenchido acima) como alternativa manual.
        }
    }

    private void ShowMainWindow() => _mainWindow.Dispatcher.Invoke(() =>
    {
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    });

    private void ShowSettings() => _mainWindow.Dispatcher.Invoke(() =>
    {
        var settings = new SettingsWindow(this) { Owner = _mainWindow.IsVisible ? _mainWindow : null };
        settings.ShowDialog();
    });

    private static void OpenLogsFolder()
    {
        Directory.CreateDirectory(LoggingBootstrapper.LogDirectory);
        Process.Start(new ProcessStartInfo(LoggingBootstrapper.LogDirectory) { UseShellExecute = true });
    }

    private void OnLaunchStarting()
    {
        lock (_stateLock)
        {
            _activeLaunches++;
        }

        Refresh();
    }

    private void OnLaunchSettled(bool succeeded)
    {
        lock (_stateLock)
        {
            _activeLaunches = Math.Max(0, _activeLaunches - 1);
            if (succeeded)
            {
                _activeSessions++;
            }
        }

        Refresh();
    }

    private void OnGameStopped()
    {
        lock (_stateLock)
        {
            _activeSessions = Math.Max(0, _activeSessions - 1);
        }

        Refresh();
    }

    private void Refresh()
    {
        TrayIconState state;
        string text;

        lock (_stateLock)
        {
            state = _activeLaunches > 0
                ? TrayIconState.Loading
                : _activeSessions > 0 ? TrayIconState.Running : TrayIconState.Idle;

            text = state switch
            {
                TrayIconState.Loading => Strings.Tray_Launching,
                TrayIconState.Running when _activeSessions == 1 => Strings.Tray_OneRunning,
                TrayIconState.Running => Strings.Tray_ManyRunning(_activeSessions),
                _ => Strings.Tray_Idle,
            };
        }

        // NotifyIcon.Text está limitado a 63 caracteres pelo Win32; truncar por segurança.
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
        _notifyIcon.Icon = _icons[state];
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        foreach (var icon in _icons.Values)
        {
            icon.Dispose();
        }
    }
}
