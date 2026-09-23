using FloppyGames.Core.Localization;
using FloppyGames.Core.Settings;

namespace FloppyGames.Agent;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private TrayIconController? _trayIcon;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        LocalizationManager.Apply(new AgentSettingsStore().Load().Language);

        _mainWindow = new MainWindow();
        _trayIcon = new TrayIconController(_mainWindow, _mainWindow.SessionManager);
        _trayIcon.ExitRequested += OnExitRequested;

        // Arranca minimizado à bandeja — só a janela principal ao clicar no ícone ou no menu
        // "Abrir FloppyGames" (ShutdownMode="OnExplicitShutdown" no App.xaml permite isto: o
        // Agent continua vivo com zero janelas visíveis).
        _ = _trayIcon.CheckForUpdatesOnStartupAsync();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        _trayIcon?.Dispose();
        _mainWindow?.Shutdown();
        Shutdown();
    }
}
