namespace FloppyGames.Agent;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private TrayIconController? _trayIcon;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        _mainWindow = new MainWindow();
        _trayIcon = new TrayIconController(_mainWindow, _mainWindow.SessionManager);
        _trayIcon.ExitRequested += OnExitRequested;

        _mainWindow.Show();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        _trayIcon?.Dispose();
        _mainWindow?.Shutdown();
        Shutdown();
    }
}
