using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using FloppyGames.Core.Launch;
using FloppyGames.Core.Localization;
using FloppyGames.Core.Logging;

namespace FloppyGames.Agent;

/// <summary>
/// Ícone de bandeja do sistema: reflete o estado atual (inativo / a lançar / jogo em execução)
/// a partir dos eventos do <see cref="GameSessionManager"/>, e dá acesso rápido a logs,
/// Label Studio, definições e saída.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly MainWindow _mainWindow;
    private readonly Dictionary<TrayIconState, Icon> _icons;
    private readonly Lock _stateLock = new();
    private int _activeLaunches;
    private int _activeSessions;

    public TrayIconController(MainWindow mainWindow, GameSessionManager sessionManager)
    {
        _mainWindow = mainWindow;
        _icons = Enum.GetValues<TrayIconState>().ToDictionary(state => state, TrayIconFactory.Create);

        var menu = new ContextMenuStrip();
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

        sessionManager.LaunchStarting += (_, _) => OnLaunchStarting();
        sessionManager.GameLaunched += (_, _) => OnLaunchSettled(succeeded: true);
        sessionManager.GameLaunchFailed += (_, _) => OnLaunchSettled(succeeded: false);
        sessionManager.GameStopped += (_, _) => OnGameStopped();
    }

    public event EventHandler? ExitRequested;

    private void ShowMainWindow() => _mainWindow.Dispatcher.Invoke(() =>
    {
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    });

    private void ShowSettings() => _mainWindow.Dispatcher.Invoke(() =>
    {
        var settings = new SettingsWindow { Owner = _mainWindow.IsVisible ? _mainWindow : null };
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
