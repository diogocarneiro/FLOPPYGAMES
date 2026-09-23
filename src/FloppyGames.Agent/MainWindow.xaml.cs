using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using FloppyGames.Core;
using FloppyGames.Core.Configuration;
using FloppyGames.Core.Launch;
using FloppyGames.Core.Localization;
using FloppyGames.Core.Logging;
using FloppyGames.Core.Media;
using FloppyGames.Core.Nfc;
using FloppyGames.Core.Platforms;
using FloppyGames.Core.Settings;
using FloppyGames.Core.Steam;
using Serilog;

namespace FloppyGames.Agent;

public partial class MainWindow : Window
{
    private readonly ILogger _logger;
    private readonly RemovableGameMediaService _mediaService;
    private readonly GameSessionManager _sessionManager;
    private readonly GameLaunchSummaryBuilder _summaryBuilder;
    private readonly AgentSettingsStore _settingsStore;
    private readonly GameCoverArtProvider _coverArtProvider;
    private readonly Dictionary<string, SplashWindow> _splashWindows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SplashWindow> _nfcSplashWindows = new(StringComparer.OrdinalIgnoreCase);
    private IDisposable? _nfcBackendDisposable;
    private NfcGameMediaService? _nfcMediaService;
    private NfcCardSessionManager? _nfcSessionManager;
    private bool _realShutdownRequested;

    public GameSessionManager SessionManager => _sessionManager;

    public ObservableCollection<string> StatusLog { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        SubtitleText.Text = Strings.MainWindow_Subtitle;
        FooterText.Text = AppInfo.FooterText;

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
        var epicLibraryScanner = new EpicGameLibraryScanner(steamFileSystem);
        var gogLibraryScanner = new GogGameLibraryScanner();
        _coverArtProvider = new GameCoverArtProvider(epicLibraryScanner, gogLibraryScanner);
        _settingsStore = new AgentSettingsStore();
        _summaryBuilder = new GameLaunchSummaryBuilder(
            new SteamLibraryScanner(steamFileSystem, steamPathProvider),
            new SteamPlaytimeReader(steamFileSystem, steamPathProvider),
            new SteamWebApiAchievementsProvider(),
            _settingsStore,
            epicLibraryScanner,
            gogLibraryScanner);

        var launcher = new CompositeGameLauncher(new Dictionary<GamePlatform, IGameLauncher>
        {
            [GamePlatform.Steam] = new SteamProtocolLauncher(),
            [GamePlatform.Epic] = new EpicProtocolLauncher(),
            [GamePlatform.Gog] = new GogExeLauncher(gogLibraryScanner),
        });

        _sessionManager = new GameSessionManager(_mediaService, launcher, new Win32ProcessGateway(), _logger);
        _sessionManager.LaunchStarting += OnLaunchStarting;
        _sessionManager.GameLaunched += OnGameLaunched;
        _sessionManager.GameLaunchFailed += OnGameLaunchFailed;
        _sessionManager.GameStopped += OnGameStopped;

        _mediaService.Start();
        AppendStatus(Strings.MainWindow_Watching);

        if (_settingsStore.Load().NfcEnabled)
        {
            StartNfcStack(launcher);
        }

        Closed += OnWindowClosed;
    }

    /// <summary>
    /// Compõe a pilha NFC (leitor → deteção de cartão → leitura → sessão de jogo) e liga-a aos
    /// mesmos handlers de splash/estado da pipeline de disquete/USB. Só é chamado quando
    /// <see cref="AgentSettings.NfcEnabled"/> está ativo — máquinas sem esse opt-in nunca tocam
    /// no subsistema PC/SC/Proxmark3. Combina leitores PC/SC genéricos com um Proxmark3 (se
    /// <c>tools/proxmark3/proxmark3.exe</c> estiver presente — ver <see cref="NfcBackendFactory"/>).
    /// Nunca deixa uma falha de arranque de qualquer um dos dois (serviço "Cartão Inteligente"
    /// desligado, sem leitor instalado, etc.) derrubar o resto do Agent: fica só um aviso no log e
    /// a deteção NFC simplesmente não ativa nesta sessão.
    /// </summary>
    private void StartNfcStack(CompositeGameLauncher launcher)
    {
        try
        {
            var backend = NfcBackendFactory.Create();
            _nfcBackendDisposable = backend.Disposable;

            var nfcWatcher = new PollingNfcCardWatcher(backend.ReaderDetector, backend.CardPresenceProbe, _logger);
            var nfcReader = new NfcCardConfigReader(backend.CardGateway);
            var nfcCardPassword = _settingsStore.Load().NfcCardPassword;
            IReadOnlyList<byte[]> extraKeys = string.IsNullOrWhiteSpace(nfcCardPassword)
                ? Array.Empty<byte[]>()
                : [NfcCardPasswordKey.Derive(nfcCardPassword)];
            _nfcMediaService = new NfcGameMediaService(nfcWatcher, nfcReader, _logger, extraKeys);
            _nfcMediaService.CardInserted += OnCardInserted;
            _nfcMediaService.CardRemoved += OnCardRemoved;
            _nfcMediaService.InvalidCardDetected += OnInvalidCardDetected;

            _nfcSessionManager = new NfcCardSessionManager(_nfcMediaService, launcher, new Win32ProcessGateway(), _logger);
            _nfcSessionManager.LaunchStarting += OnNfcLaunchStarting;
            _nfcSessionManager.GameLaunched += OnNfcGameLaunched;
            _nfcSessionManager.GameLaunchFailed += OnNfcGameLaunchFailed;
            _nfcSessionManager.GameStopped += OnNfcGameStopped;

            _nfcMediaService.Start();
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Falha ao iniciar a deteção NFC — a continuar sem ela nesta sessão.");
        }
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
        AppendStatus(Strings.MainWindow_MediaInserted(e.DriveRoot, e.Config.Title, e.Config.Platform.ToString(), e.Config.Process));

    private void OnMediaRemoved(object? sender, MediaRemovedEventArgs e) =>
        AppendStatus(Strings.MainWindow_MediaRemoved(e.DriveRoot, e.Config.Title));

    private void OnInvalidMediaDetected(object? sender, InvalidMediaEventArgs e) =>
        AppendStatus(Strings.MainWindow_InvalidGameIni(e.DriveRoot, string.Join(" | ", e.Errors)));

    private void OnLaunchStarting(object? sender, GameLaunchStartingEventArgs e) =>
        Dispatcher.Invoke(() =>
        {
            var splash = new SplashWindow();
            var coverPath = e.Config.Cover is null ? null : Path.Combine(e.DriveRoot, e.Config.Cover);
            var mediaKind = MediaKindClassifier.Classify(e.DriveRoot);
            var settings = _settingsStore.Load();
            splash.SetGame(e.Config.Title, e.Config.Description, coverPath, mediaKind, e.Config.Platform);
            splash.SetCrtEffectEnabled(settings.CrtEffectEnabled);

            if (mediaKind == MediaKind.Floppy && settings.PlayFloppySound)
            {
                FloppyMotorSoundPlayer.PlayIfAvailable();
            }

            splash.SetStatus(LaunchingStatusText(e.Config.Platform));
            splash.StartProgress(TimeSpan.FromSeconds(e.Config.LaunchDelaySeconds + e.Config.WatchTimeoutSeconds));
            splash.Show();

            if (_splashWindows.Remove(e.DriveRoot, out var previous))
            {
                previous.Close();
            }

            _splashWindows[e.DriveRoot] = splash;
            AppendStatus(Strings.MainWindow_Launching(e.Config.Title));

            _ = UpdateSplashSummaryAsync(e.DriveRoot, e.Config, splash);
        });

    /// <summary>
    /// Tamanho/estado de instalação demoram a apurar (I/O num suporte que pode ser lento),
    /// por isso correm em segundo plano e só atualizam a splash se ela ainda for a atual para esta unidade.
    /// </summary>
    private async Task UpdateSplashSummaryAsync(string driveRoot, GameConfig config, SplashWindow splash)
    {
        var summary = await _summaryBuilder.BuildAsync(driveRoot, config, CancellationToken.None);

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
                splash.ShowSuccessAndAutoClose(Strings.Splash_GameRunning(e.Config.Title));
            }

            AppendStatus(Strings.MainWindow_LaunchConfirmed(e.Config.Title));
        });

    private void OnGameLaunchFailed(object? sender, GameLaunchFailedEventArgs e) =>
        Dispatcher.Invoke(() =>
        {
            var reason = LocalizeFailureReason(e.Reason);

            if (_splashWindows.Remove(e.DriveRoot, out var splash))
            {
                splash.ShowErrorAndAutoClose(reason);
            }

            AppendStatus(Strings.MainWindow_LaunchFailed(e.Config.Title, reason));
        });

    private void OnGameStopped(object? sender, GameStoppedEventArgs e) =>
        AppendStatus(Strings.MainWindow_GameStopped(e.Config.Title));

    private void OnCardInserted(object? sender, CardInsertedEventArgs e) =>
        AppendStatus(Strings.MainWindow_CardInserted(e.Uid, e.Config.Title, e.Config.Platform.ToString(), e.Config.Process));

    private void OnCardRemoved(object? sender, CardRemovedEventArgs e) =>
        AppendStatus(Strings.MainWindow_CardRemoved(e.Uid, e.Config.Title));

    private void OnInvalidCardDetected(object? sender, InvalidCardEventArgs e) =>
        AppendStatus(Strings.MainWindow_InvalidCard(e.Uid, string.Join(" | ", e.Errors)));

    private void OnNfcLaunchStarting(object? sender, NfcCardLaunchStartingEventArgs e) =>
        Dispatcher.Invoke(() =>
        {
            var splash = new SplashWindow();
            var settings = _settingsStore.Load();
            splash.SetGame(e.Config.Title, e.Config.Description, coverPath: null, MediaKind.Nfc, e.Config.Platform);
            splash.SetCardId(e.Uid);
            splash.SetCrtEffectEnabled(settings.CrtEffectEnabled);
            splash.SetNfcCardSummary(Encoding.UTF8.GetByteCount(GameIniWriter.Write(e.Config)));

            splash.SetStatus(LaunchingStatusText(e.Config.Platform));
            splash.StartProgress(TimeSpan.FromSeconds(e.Config.LaunchDelaySeconds + e.Config.WatchTimeoutSeconds));
            splash.Show();

            if (_nfcSplashWindows.Remove(e.Uid, out var previous))
            {
                previous.Close();
            }

            _nfcSplashWindows[e.Uid] = splash;
            AppendStatus(Strings.MainWindow_Launching(e.Config.Title));

            // Um cartão NFC não tem espaço para uma capa — mas o jogo já está instalado, por
            // isso vai-se buscá-la à origem da plataforma (Steam, Epic ou GOG) em vez de exigir
            // que esteja gravada no suporte.
            _ = UpdateNfcSplashCoverAsync(e.Uid, e.Config, splash);
        });

    /// <summary>Espelha <see cref="UpdateSplashSummaryAsync"/>: corre em segundo plano e só aplica se a splash ainda for a atual para este cartão.</summary>
    private async Task UpdateNfcSplashCoverAsync(string uid, GameConfig config, SplashWindow splash)
    {
        var coverBytes = await _coverArtProvider.TryDownloadCoverAsync(config, CancellationToken.None);
        if (coverBytes is null)
        {
            return;
        }

        Dispatcher.Invoke(() =>
        {
            if (_nfcSplashWindows.TryGetValue(uid, out var current) && ReferenceEquals(current, splash))
            {
                splash.SetCoverFromBytes(coverBytes);
            }
        });
    }

    private void OnNfcGameLaunched(object? sender, NfcCardLaunchedEventArgs e) =>
        Dispatcher.Invoke(() =>
        {
            if (_nfcSplashWindows.Remove(e.Uid, out var splash))
            {
                splash.ShowSuccessAndAutoClose(Strings.Splash_GameRunning(e.Config.Title));
            }

            AppendStatus(Strings.MainWindow_LaunchConfirmed(e.Config.Title));
        });

    private void OnNfcGameLaunchFailed(object? sender, NfcCardLaunchFailedEventArgs e) =>
        Dispatcher.Invoke(() =>
        {
            var reason = LocalizeFailureReason(e.Reason);

            if (_nfcSplashWindows.Remove(e.Uid, out var splash))
            {
                splash.ShowErrorAndAutoClose(reason);
            }

            AppendStatus(Strings.MainWindow_LaunchFailed(e.Config.Title, reason));
        });

    private void OnNfcGameStopped(object? sender, NfcCardStoppedEventArgs e) =>
        AppendStatus(Strings.MainWindow_GameStopped(e.Config.Title));

    private static string LaunchingStatusText(GamePlatform platform) => platform switch
    {
        GamePlatform.Steam => Strings.Splash_LaunchingSteam,
        GamePlatform.Epic => Strings.Splash_LaunchingEpic,
        GamePlatform.Gog => Strings.Splash_LaunchingGog,
        _ => Strings.Splash_LaunchingGeneric,
    };

    private static string LocalizeFailureReason(GameLaunchFailureReason reason) => reason switch
    {
        GameLaunchFailureReason.Timeout => Strings.LaunchFailed_Timeout,
        GameLaunchFailureReason.MediaRemovedDuringLaunch => Strings.LaunchFailed_MediaRemoved,
        _ => Strings.LaunchFailed_UnexpectedError,
    };

    private void AppendStatus(string message) =>
        Dispatcher.Invoke(() => StatusLog.Insert(0, $"{DateTime.Now:HH:mm:ss} — {message}"));

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _logger.Information("FloppyGames Agent a terminar.");
        _sessionManager.Dispose();
        _mediaService.Dispose();
        _nfcSessionManager?.Dispose();
        _nfcMediaService?.Dispose();
        _nfcBackendDisposable?.Dispose();
    }
}
