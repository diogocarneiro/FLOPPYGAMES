using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using FloppyGames.Core.Configuration;
using FloppyGames.Core.Launch;
using FloppyGames.Core.Logging;
using FloppyGames.Core.Media;
using FloppyGames.Core.Steam;
using Serilog;

namespace FloppyGames.Agent;

public partial class MainWindow : Window
{
    private readonly ILogger _logger;
    private readonly RemovableGameMediaService _mediaService;
    private readonly GameSessionManager _sessionManager;
    private readonly GameLaunchSummaryBuilder _summaryBuilder;
    private readonly Dictionary<string, SplashWindow> _splashWindows = new(StringComparer.OrdinalIgnoreCase);
    private bool _realShutdownRequested;

    public GameSessionManager SessionManager => _sessionManager;

    public ObservableCollection<string> StatusLog { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _logger = LoggingBootstrapper.CreateLogger("Agent");
        _logger.Information("FloppyGames Agent iniciado.");

        // Duas fontes de deteção fundidas: WMI para pens USB (letra de unidade aparece/desaparece)
        // e sondagem dedicada para disquetes (a letra fica atribuída, só o disco lá dentro muda).
        var watcher = new CompositeRemovableMediaWatcher(new IRemovableMediaWatcher[]
        {
            new WmiRemovableMediaWatcher(_logger),
            new PollingFloppyDriveWatcher(PollingFloppyDriveWatcher.DefaultCandidateDriveRoots, new DriveInfoReadinessProbe(), _logger),
        });
        var inspector = new FileSystemDriveInspector();
        var scanner = new GameMediaScanner(inspector);
        _mediaService = new RemovableGameMediaService(watcher, scanner, _logger);

        _mediaService.MediaInserted += OnMediaInserted;
        _mediaService.MediaRemoved += OnMediaRemoved;
        _mediaService.InvalidMediaDetected += OnInvalidMediaDetected;

        var steamFileSystem = new FileSystemSteamFileSystem();
        var steamPathProvider = new RegistrySteamPathProvider();
        _summaryBuilder = new GameLaunchSummaryBuilder(
            new SteamLibraryScanner(steamFileSystem, steamPathProvider),
            new SteamPlaytimeReader(steamFileSystem, steamPathProvider));

        _sessionManager = new GameSessionManager(_mediaService, new SteamProtocolLauncher(), new Win32ProcessGateway(), _logger);
        _sessionManager.LaunchStarting += OnLaunchStarting;
        _sessionManager.GameLaunched += OnGameLaunched;
        _sessionManager.GameLaunchFailed += OnGameLaunchFailed;
        _sessionManager.GameStopped += OnGameStopped;

        _mediaService.Start();
        AppendStatus("A vigiar unidades amovíveis...");

        Closed += OnWindowClosed;
    }

    /// <summary>
    /// Fecha a janela para os separadores de estado, mas o Agent continua a vigiar em segundo
    /// plano — a bandeja do sistema é que dita quando o processo termina de facto.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_realShutdownRequested)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    /// <summary>Encerramento real do Agent, chamado a partir do menu "Sair" da bandeja.</summary>
    public void Shutdown()
    {
        _realShutdownRequested = true;
        Close();
    }

    private void OnMediaInserted(object? sender, MediaInsertedEventArgs e) =>
        AppendStatus($"Disquete inserida em {e.DriveRoot}: {e.Config.Title} (AppID {e.Config.AppId}, processo {e.Config.Process}).");

    private void OnMediaRemoved(object? sender, MediaRemovedEventArgs e) =>
        AppendStatus($"Disquete removida de {e.DriveRoot}: {e.Config.Title}.");

    private void OnInvalidMediaDetected(object? sender, InvalidMediaEventArgs e) =>
        AppendStatus($"GAME.INI inválido em {e.DriveRoot}: {string.Join(" | ", e.Errors)}");

    private void OnLaunchStarting(object? sender, GameLaunchStartingEventArgs e) =>
        Dispatcher.Invoke(() =>
        {
            var splash = new SplashWindow();
            var coverPath = e.Config.Cover is null ? null : Path.Combine(e.DriveRoot, e.Config.Cover);
            var mediaKind = MediaKindClassifier.Classify(e.DriveRoot);
            splash.SetGame(e.Config.Title, e.Config.Description, coverPath, mediaKind);
            splash.SetStatus("A abrir a Steam...");
            splash.StartProgress(TimeSpan.FromSeconds(e.Config.LaunchDelaySeconds + e.Config.WatchTimeoutSeconds));
            splash.Show();

            if (_splashWindows.Remove(e.DriveRoot, out var previous))
            {
                previous.Close();
            }

            _splashWindows[e.DriveRoot] = splash;
            AppendStatus($"A lançar {e.Config.Title}...");

            _ = UpdateSplashSummaryAsync(e.DriveRoot, e.Config, splash);
        });

    /// <summary>
    /// Tamanho/CRC32/estado de instalação demoram a apurar (I/O num suporte que pode ser lento),
    /// por isso correm em segundo plano e só atualizam a splash se ela ainda for a atual para esta unidade.
    /// </summary>
    private async Task UpdateSplashSummaryAsync(string driveRoot, GameConfig config, SplashWindow splash)
    {
        var summary = await Task.Run(() => _summaryBuilder.Build(driveRoot, config));

        Dispatcher.Invoke(() =>
        {
            if (_splashWindows.TryGetValue(driveRoot, out var current) && ReferenceEquals(current, splash))
            {
                splash.SetSummary(summary);
            }
        });
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e) =>
        Dispatcher.Invoke(() =>
        {
            if (_splashWindows.Remove(e.DriveRoot, out var splash))
            {
                splash.ShowSuccessAndAutoClose($"{e.Config.Title} em execução.");
            }

            AppendStatus($"{e.Config.Title} confirmado em execução.");
        });

    private void OnGameLaunchFailed(object? sender, GameLaunchFailedEventArgs e) =>
        Dispatcher.Invoke(() =>
        {
            if (_splashWindows.Remove(e.DriveRoot, out var splash))
            {
                splash.ShowErrorAndAutoClose(e.Reason);
            }

            AppendStatus($"Falha ao lançar {e.Config.Title}: {e.Reason}");
        });

    private void OnGameStopped(object? sender, GameStoppedEventArgs e) =>
        AppendStatus($"{e.Config.Title} terminado.");

    private void AppendStatus(string message) =>
        Dispatcher.Invoke(() => StatusLog.Insert(0, $"{DateTime.Now:HH:mm:ss} — {message}"));

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _logger.Information("FloppyGames Agent a terminar.");
        _sessionManager.Dispose();
        _mediaService.Dispose();
    }
}
