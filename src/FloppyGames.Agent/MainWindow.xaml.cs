using System.Collections.ObjectModel;
using System.Windows;
using FloppyGames.Core.Logging;
using FloppyGames.Core.Media;
using Serilog;

namespace FloppyGames.Agent;

public partial class MainWindow : Window
{
    private readonly ILogger _logger;
    private readonly RemovableGameMediaService _mediaService;

    public ObservableCollection<string> StatusLog { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _logger = LoggingBootstrapper.CreateLogger("Agent");
        _logger.Information("FloppyGames Agent iniciado.");

        var watcher = new WmiRemovableMediaWatcher(_logger);
        var inspector = new FileSystemDriveInspector();
        var scanner = new GameMediaScanner(inspector);
        _mediaService = new RemovableGameMediaService(watcher, scanner, _logger);

        _mediaService.MediaInserted += OnMediaInserted;
        _mediaService.MediaRemoved += OnMediaRemoved;
        _mediaService.InvalidMediaDetected += OnInvalidMediaDetected;

        _mediaService.Start();
        AppendStatus("A vigiar unidades amovíveis...");

        Closed += OnWindowClosed;
    }

    private void OnMediaInserted(object? sender, MediaInsertedEventArgs e) =>
        AppendStatus($"Disquete inserida em {e.DriveRoot}: {e.Config.Title} (AppID {e.Config.AppId}, processo {e.Config.Process}).");

    private void OnMediaRemoved(object? sender, MediaRemovedEventArgs e) =>
        AppendStatus($"Disquete removida de {e.DriveRoot}: {e.Config.Title}.");

    private void OnInvalidMediaDetected(object? sender, InvalidMediaEventArgs e) =>
        AppendStatus($"GAME.INI inválido em {e.DriveRoot}: {string.Join(" | ", e.Errors)}");

    private void AppendStatus(string message) =>
        Dispatcher.Invoke(() => StatusLog.Insert(0, $"{DateTime.Now:HH:mm:ss} — {message}"));

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _logger.Information("FloppyGames Agent a terminar.");
        _mediaService.Dispose();
    }
}
