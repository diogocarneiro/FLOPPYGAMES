using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using FloppyGames.Core.Catalog;
using FloppyGames.Core.Configuration;
using FloppyGames.Core.Localization;
using FloppyGames.Core.Logging;
using FloppyGames.Core.Media;
using FloppyGames.Core.Nfc;
using FloppyGames.Core.Platforms;
using FloppyGames.Core.Settings;
using FloppyGames.Core.Steam;
using Microsoft.Win32;
using Serilog;

namespace FloppyGames.LabelStudio;

public partial class MainWindow : Window
{
    private readonly ILogger _logger;
    private readonly SteamLibraryScanner _steamScanner;
    private readonly EpicGameLibraryScanner _epicScanner;
    private readonly GogGameLibraryScanner _gogScanner;
    private readonly GameExecutableFinder _executableFinder;
    private readonly ICoverArtProvider _coverArtProvider;
    private readonly FloppyMediaWriter _mediaWriter;
    private readonly GameCatalog _catalog;
    private readonly INfcReaderDetector _nfcReaderDetector;
    private readonly IDisposable? _nfcBackendDisposable;
    private readonly NfcCardConfigWriter _nfcCardWriter;
    private readonly PollingNfcCardWatcher _nfcCardWatcher;

    private List<DiscoveredGame> _allGames = [];
    private DiscoveredGame? _selectedGame;
    private byte[]? _coverBytes;
    private string _coverFileName = "cover.jpg";
    private CancellationTokenSource? _coverFetchCts;
    private NfcCardPresence? _presentNfcCard;
    private (NfcCardPresence Card, GameConfig Config)? _pendingNfcFormatConfig;

    public MainWindow()
    {
        InitializeComponent();
        ApplyStaticText();

        _logger = LoggingBootstrapper.CreateLogger("LabelStudio");
        _logger.Information("FloppyGames Label Studio iniciado.");

        var fileSystem = new FileSystemSteamFileSystem();
        _steamScanner = new SteamLibraryScanner(fileSystem, new RegistrySteamPathProvider());
        _epicScanner = new EpicGameLibraryScanner(fileSystem);
        _gogScanner = new GogGameLibraryScanner();
        _executableFinder = new GameExecutableFinder(fileSystem);
        _coverArtProvider = new SteamCdnCoverArtProvider();
        _mediaWriter = new FloppyMediaWriter(new FileSystemDriveInspector());
        _catalog = new GameCatalog();

        var nfcBackend = NfcBackendFactory.Create();
        _nfcReaderDetector = nfcBackend.ReaderDetector;
        _nfcBackendDisposable = nfcBackend.Disposable;
        _nfcCardWriter = new NfcCardConfigWriter(nfcBackend.CardGateway);
        _nfcCardWatcher = new PollingNfcCardWatcher(nfcBackend.ReaderDetector, nfcBackend.CardPresenceProbe, _logger);
        _nfcCardWatcher.CardArrived += OnNfcCardArrived;
        _nfcCardWatcher.CardRemoved += OnNfcCardRemoved;

        Closed += OnMainWindowClosed;

        // Definido em código (não no XAML) pela mesma razão do PlatformCombo.SelectedIndex abaixo:
        // um IsChecked="True" no XAML dispara o Checked durante o InitializeComponent(), antes dos
        // campos _nfc* acima estarem atribuídos, e o handler batia num NullReferenceException.
        TargetTypeDriveRadio.IsChecked = true;

        RefreshDrives();

        // Definido em código (não no XAML) para só disparar OnPlatformChanged depois dos scanners
        // acima estarem prontos — se viesse do XAML, o SelectionChanged correria durante o
        // InitializeComponent(), antes destes campos serem atribuídos.
        PlatformCombo.ItemsSource = EnabledPlatformOptions();
        PlatformCombo.SelectedIndex = 0;
    }

    /// <summary>
    /// Só as plataformas ligadas nas Definições do Agent (settings.json partilhado) aparecem aqui
    /// — Steam ligada por omissão, Epic e GOG desligadas. Nunca fica vazio: se por acaso todas
    /// ficarem desligadas, mostra as 3 na mesma em vez de um seletor inutilizável.
    /// </summary>
    private static List<PlatformOption> EnabledPlatformOptions()
    {
        var settings = new AgentSettingsStore().Load();
        var options = new List<PlatformOption>();

        if (settings.SteamEnabled)
        {
            options.Add(new PlatformOption(GamePlatform.Steam, "Steam"));
        }

        if (settings.EpicEnabled)
        {
            options.Add(new PlatformOption(GamePlatform.Epic, "Epic Games"));
        }

        if (settings.GogEnabled)
        {
            options.Add(new PlatformOption(GamePlatform.Gog, "GOG"));
        }

        if (options.Count == 0)
        {
            options.Add(new PlatformOption(GamePlatform.Steam, "Steam"));
            options.Add(new PlatformOption(GamePlatform.Epic, "Epic Games"));
            options.Add(new PlatformOption(GamePlatform.Gog, "GOG"));
        }

        return options;
    }

    private void ApplyStaticText()
    {
        Step1Header.Text = Strings.LS_Step1;
        PlatformLabel.Text = Strings.LS_Platform;
        InstalledGamesLabel.Text = Strings.LS_InstalledGames;
        Step2Header.Text = Strings.LS_Step2;
        TitleFieldLabel.Text = Strings.LS_TitleField;
        ProcessFieldLabel.Text = Strings.LS_ProcessField;
        DescriptionFieldLabel.Text = Strings.LS_DescriptionField;
        AdvancedOptionsExpander.Header = Strings.LS_AdvancedOptions;
        WatchTimeoutFieldLabel.Text = Strings.LS_WatchTimeoutField;
        LaunchDelayFieldLabel.Text = Strings.LS_LaunchDelayField;
        GracefulShutdownBox.Content = Strings.LS_GracefulShutdownField;
        Step3Header.Text = Strings.LS_Step3;
        Step4Header.Text = Strings.LS_Step4;
        TargetTypeDriveRadio.Content = Strings.LS_TargetTypeDrive;
        TargetTypeNfcRadio.Content = Strings.LS_TargetTypeNfc;
        TargetDriveLabel.Text = Strings.LS_TargetDrive;
        RefreshDrivesButton.Content = Strings.LS_RefreshButton;
        RefreshNfcReaderButton.Content = Strings.LS_Nfc_RefreshReaderButton;
        WriteButton.Content = Strings.LS_WriteButton;
        FormatCardButton.Content = Strings.LS_Nfc_FormatButton;
        PrintLabelButton.Content = Strings.LS_PrintButton;
        ChooseLocalCoverButton.Content = Strings.LS_ChooseLocalCoverButton;
    }

    private GamePlatform SelectedPlatform() =>
        (PlatformCombo.SelectedItem as PlatformOption)?.Platform ?? GamePlatform.Steam;

    private static string PlatformDisplayName(GamePlatform platform) => platform switch
    {
        GamePlatform.Steam => "Steam",
        GamePlatform.Epic => "Epic Games",
        GamePlatform.Gog => "GOG",
        _ => platform.ToString(),
    };

    private void OnPlatformChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        _ = LoadGamesAsync();

    private async Task LoadGamesAsync()
    {
        var platform = SelectedPlatform();
        GamesStatusText.Text = Strings.LS_LoadingLibrary(PlatformDisplayName(platform));

        try
        {
            var games = await Task.Run(() => ScanPlatform(platform));
            _allGames = [.. games];

            ApplyFilter(SearchBox.Text);
            GamesStatusText.Text = _allGames.Count switch
            {
                0 => Strings.LS_NoGamesFound(PlatformDisplayName(platform)),
                1 => Strings.LS_GamesFoundOne,
                _ => Strings.LS_GamesFoundMany(_allGames.Count),
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Falha ao ler a biblioteca {Platform}.", platform);
            GamesStatusText.Text = Strings.LS_LoadLibraryFailed;
        }
    }

    private List<DiscoveredGame> ScanPlatform(GamePlatform platform) => platform switch
    {
        GamePlatform.Steam => _steamScanner.ScanInstalledGames()
            .Select(g => new DiscoveredGame(g.Name, GamePlatform.Steam, g.InstallPath, g.SizeOnDiskBytes, SteamAppId: g.AppId))
            .ToList(),
        GamePlatform.Epic => [.. _epicScanner.ScanInstalledGames()],
        GamePlatform.Gog => [.. _gogScanner.ScanInstalledGames()],
        _ => [],
    };

    private void ApplyFilter(string? query)
    {
        GamesList.ItemsSource = string.IsNullOrWhiteSpace(query)
            ? _allGames
            : _allGames.Where(g => g.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void OnSearchTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        ApplyFilter(SearchBox.Text);

    private void OnGameSelected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (GamesList.SelectedItem is not DiscoveredGame game)
        {
            DetailsPanel.IsEnabled = false;
            _selectedGame = null;
            return;
        }

        _selectedGame = game;
        DetailsPanel.IsEnabled = true;
        TitleBox.Text = game.Name;
        IdentifierLabel.Text = IdentifierLabelFor(game.Platform);
        IdentifierText.Text = FormatIdentifier(game);
        ProcessBox.Text = _executableFinder.FindSuggestedExecutable(game.InstallPath) ?? string.Empty;
        DescriptionBox.Text = string.Empty;
        WriteStatusText.Text = string.Empty;
        FormatCardButton.Visibility = Visibility.Collapsed;
        _pendingNfcFormatConfig = null;

        _coverFetchCts?.Cancel();
        CoverImage.Source = null;
        _coverBytes = null;
        _coverFileName = "cover.jpg";

        // O catálogo local (catalog/catalog.json) tem processo verificado + descrição já
        // traduzida para jogos já catalogados — poupa o utilizador de preencher isto outra vez.
        var catalogEntry = _catalog.TryFind(game);
        if (catalogEntry is not null)
        {
            ProcessBox.Text = catalogEntry.Process;
            DescriptionBox.Text = ResolveCatalogDescription(catalogEntry);
            WriteStatusText.Text = Strings.LS_FilledFromCatalog;
        }

        // Só a Steam tem um CDN de capas público e sem autenticação — Epic/GOG ficam com a capa
        // do catálogo (se existir) ou a escolha manual de imagem local. Sem isto ficar explícito,
        // a caixa vazia parece avariada.
        if (game.Platform == GamePlatform.Steam && game.SteamAppId is { } appId)
        {
            ShowNoCoverMessage(Strings.LS_FetchingSteamCover);
            _ = LoadCoverAsync(appId, catalogEntry);
        }
        else if (catalogEntry is null || !TryLoadCatalogCover(catalogEntry))
        {
            ShowNoCoverMessage(Strings.LS_NoAutoCoverForPlatform(PlatformDisplayName(game.Platform)));
        }
    }

    private static string ResolveCatalogDescription(CatalogEntry entry)
    {
        var languageCode = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return entry.Description.TryGetValue(languageCode, out var localized)
            ? localized
            : entry.Description.Values.FirstOrDefault() ?? string.Empty;
    }

    private bool TryLoadCatalogCover(CatalogEntry entry)
    {
        var coverPath = _catalog.ResolveCoverPath(entry);
        if (coverPath is null || !File.Exists(coverPath))
        {
            return false;
        }

        try
        {
            _coverBytes = File.ReadAllBytes(coverPath);
            _coverFileName = "cover" + Path.GetExtension(coverPath);
            SetCoverImage(_coverBytes);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(ex, "Falha ao ler a capa do catálogo em {Path}.", coverPath);
            return false;
        }
    }

    private static string IdentifierLabelFor(GamePlatform platform) => platform switch
    {
        GamePlatform.Steam => Strings.LS_IdentifierLabelSteam,
        GamePlatform.Epic => Strings.LS_IdentifierLabelEpic,
        GamePlatform.Gog => Strings.LS_IdentifierLabelGog,
        _ => Strings.LS_IdentifierLabel,
    };

    private static string FormatIdentifier(DiscoveredGame game) => game.Platform switch
    {
        GamePlatform.Steam => $"{game.SteamAppId}",
        GamePlatform.Epic => $"{game.EpicNamespace}:{game.EpicItemId}:{game.EpicAppName}",
        GamePlatform.Gog => $"{game.GogGameId}",
        _ => "—",
    };

    private void ShowNoCoverMessage(string message)
    {
        NoCoverText.Text = message;
        NoCoverText.Visibility = Visibility.Visible;
    }

    private async Task LoadCoverAsync(int appId, CatalogEntry? catalogEntry)
    {
        _coverFetchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _coverFetchCts = cts;

        CoverImage.Source = null;
        _coverBytes = null;
        _coverFileName = "cover.jpg";

        try
        {
            var bytes = await _coverArtProvider.TryDownloadCoverAsync(appId, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            if (bytes is null)
            {
                if (catalogEntry is null || !TryLoadCatalogCover(catalogEntry))
                {
                    ShowNoCoverMessage(Strings.LS_NoCoverFoundSteam);
                }

                return;
            }

            _coverBytes = bytes;
            SetCoverImage(bytes);
        }
        catch (OperationCanceledException)
        {
            // A seleção mudou entretanto — ignorar o pedido cancelado.
        }
    }

    private void SetCoverImage(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        CoverImage.Source = bitmap;
        NoCoverText.Visibility = Visibility.Collapsed;
    }

    private void OnChooseLocalCoverClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = $"{Strings.LS_ImagesFilterWord} (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
            Title = Strings.LS_ChooseCoverDialogTitle,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _coverFetchCts?.Cancel();
            _coverBytes = File.ReadAllBytes(dialog.FileName);
            _coverFileName = "cover" + Path.GetExtension(dialog.FileName);
            SetCoverImage(_coverBytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(ex, "Falha ao ler a imagem local escolhida.");
            MessageBox.Show(Strings.LS_ImageReadFailed, "FloppyGames", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RefreshDrives()
    {
        var previouslySelected = DriveCombo.SelectedItem as string;
        var drives = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Removable)
            .Select(d => d.Name)
            .ToList();

        DriveCombo.ItemsSource = drives;

        if (previouslySelected is not null && drives.Contains(previouslySelected))
        {
            DriveCombo.SelectedItem = previouslySelected;
        }
        else if (drives.Count > 0)
        {
            DriveCombo.SelectedIndex = 0;
        }
    }

    private void OnRefreshDrivesClicked(object sender, RoutedEventArgs e) => RefreshDrives();

    /// <summary>
    /// Alterna entre gravar numa disquete/pen (letra de unidade) e gravar num cartão NFC. A
    /// sondagem do leitor NFC só corre enquanto este modo estiver selecionado — evita chamadas
    /// PC/SC desnecessárias quando ninguém pediu para usar um cartão.
    /// </summary>
    private void OnTargetTypeChanged(object sender, RoutedEventArgs e)
    {
        if (TargetTypeNfcRadio.IsChecked == true)
        {
            DriveTargetPanel.Visibility = Visibility.Collapsed;
            NfcTargetPanel.Visibility = Visibility.Visible;
            RefreshNfcReaderStatus();
            _nfcCardWatcher.Start();
        }
        else
        {
            NfcTargetPanel.Visibility = Visibility.Collapsed;
            DriveTargetPanel.Visibility = Visibility.Visible;
            _nfcCardWatcher.Stop();
        }
    }

    private void OnRefreshNfcReaderClicked(object sender, RoutedEventArgs e) => RefreshNfcReaderStatus();

    private void RefreshNfcReaderStatus()
    {
        var readers = _nfcReaderDetector.ListConnectedReaders();
        NfcReaderStatusText.Text = readers.Count > 0
            ? Strings.LS_Nfc_ReaderDetected(readers[0])
            : Strings.LS_Nfc_NoReaderDetected;

        NfcCardStatusText.Text = _presentNfcCard is { } present
            ? Strings.LS_Nfc_CardDetected(present.Uid)
            : Strings.LS_Nfc_WaitingForCard;
    }

    private void OnNfcCardArrived(object? sender, NfcCardPresence e) =>
        Dispatcher.Invoke(() =>
        {
            _presentNfcCard = e;
            NfcCardStatusText.Text = Strings.LS_Nfc_CardDetected(e.Uid);
        });

    private void OnNfcCardRemoved(object? sender, NfcCardPresence e) =>
        Dispatcher.Invoke(() =>
        {
            if (_presentNfcCard is { } current && string.Equals(current.Uid, e.Uid, StringComparison.OrdinalIgnoreCase))
            {
                _presentNfcCard = null;
            }

            NfcCardStatusText.Text = Strings.LS_Nfc_WaitingForCard;
        });

    private void OnMainWindowClosed(object? sender, EventArgs e)
    {
        _nfcCardWatcher.Dispose();
        _nfcBackendDisposable?.Dispose();
    }

    private void OnWriteClicked(object sender, RoutedEventArgs e)
    {
        if (_selectedGame is not { } selectedGame)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(TitleBox.Text) || string.IsNullOrWhiteSpace(ProcessBox.Text))
        {
            WriteStatusText.Text = Strings.LS_TitleProcessRequired;
            return;
        }

        if (!int.TryParse(WatchTimeoutBox.Text, out var watchTimeout) || watchTimeout <= 0)
        {
            WriteStatusText.Text = Strings.LS_WatchTimeoutInvalid;
            return;
        }

        if (!int.TryParse(LaunchDelayBox.Text, out var launchDelay) || launchDelay < 0)
        {
            WriteStatusText.Text = Strings.LS_LaunchDelayInvalid;
            return;
        }

        var config = new GameConfig
        {
            Title = TitleBox.Text.Trim(),
            Platform = selectedGame.Platform,
            AppId = selectedGame.SteamAppId,
            EpicNamespace = selectedGame.EpicNamespace,
            EpicItemId = selectedGame.EpicItemId,
            EpicAppName = selectedGame.EpicAppName,
            GogGameId = selectedGame.GogGameId,
            Process = ProcessBox.Text.Trim(),
            Cover = _coverBytes is not null ? _coverFileName : null,
            Description = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim(),
            WatchTimeoutSeconds = watchTimeout,
            LaunchDelaySeconds = launchDelay,
            GracefulShutdown = GracefulShutdownBox.IsChecked == true,
        };

        if (TargetTypeNfcRadio.IsChecked == true)
        {
            WriteToNfcCard(config);
        }
        else
        {
            WriteToDrive(config);
        }
    }

    private void WriteToDrive(GameConfig config)
    {
        if (DriveCombo.SelectedItem is not string driveRoot)
        {
            WriteStatusText.Text = Strings.LS_ChooseTargetDrive;
            return;
        }

        var check = _mediaWriter.Check(driveRoot, config, _coverBytes);

        if (check.Status == MediaWriteCheckStatus.Blocked)
        {
            WriteStatusText.Text = check.Message;
            return;
        }

        if (check.Status == MediaWriteCheckStatus.NeedsConfirmation)
        {
            var confirmed = MessageBox.Show(check.Message, "FloppyGames", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirmed != MessageBoxResult.Yes)
            {
                WriteStatusText.Text = Strings.LS_WriteCancelled;
                return;
            }
        }

        try
        {
            _mediaWriter.Write(driveRoot, config, _coverBytes, config.Cover);
            WriteStatusText.Text = Strings.LS_WriteSuccess(config.Title, driveRoot);
            _logger.Information(
                "GAME.INI escrito em {Drive} para {Title} ({Platform}).", driveRoot, config.Title, config.Platform);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error(ex, "Falha ao escrever para {Drive}.", driveRoot);
            WriteStatusText.Text = Strings.LS_WriteFailed;
        }
    }

    private void WriteToNfcCard(GameConfig config)
    {
        FormatCardButton.Visibility = Visibility.Collapsed;

        if (_presentNfcCard is not { } card)
        {
            WriteStatusText.Text = Strings.LS_Nfc_NoCardPresent;
            return;
        }

        if (_coverBytes is not null)
        {
            WriteStatusText.Text = Strings.LS_Nfc_CoverNotWritten;
        }

        var check = _nfcCardWriter.Check(card.ReaderName, card.CardType, config);

        if (check.Status == NfcCardWriteCheckStatus.AuthenticationFailed)
        {
            WriteStatusText.Text = check.Message;
            // Chave de fábrica não autentica — pode ser um clone "magic" que ainda responde ao
            // backdoor Gen1a/Gen2, sem precisar de saber a chave atual. Só o Proxmark3 suporta
            // isto (ver IMifareCardGateway.TryMagicWriteBlock); o botão fica sempre visível nesse
            // caso, a própria escrita é que reporta se o cartão não for um clone.
            _pendingNfcFormatConfig = (card, config);
            FormatCardButton.Visibility = Visibility.Visible;
            return;
        }

        if (check.Status == NfcCardWriteCheckStatus.Blocked)
        {
            WriteStatusText.Text = check.Message;
            return;
        }

        if (check.Status == NfcCardWriteCheckStatus.NeedsConfirmation)
        {
            var confirmed = MessageBox.Show(check.Message, "FloppyGames", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirmed != MessageBoxResult.Yes)
            {
                WriteStatusText.Text = Strings.LS_WriteCancelled;
                return;
            }
        }

        try
        {
            _nfcCardWriter.Write(card.ReaderName, card.CardType, config);
            WriteStatusText.Text = Strings.LS_Nfc_WriteSuccess(config.Title, card.Uid);
            _logger.Information(
                "GAME.INI gravado no cartão NFC UID {Uid} para {Title} ({Platform}).", card.Uid, config.Title, config.Platform);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Falha ao gravar no cartão NFC UID {Uid}.", card.Uid);
            WriteStatusText.Text = Strings.LS_Nfc_WriteFailed;
        }
    }

    private void OnFormatCardClicked(object sender, RoutedEventArgs e)
    {
        if (_pendingNfcFormatConfig is not { } pending)
        {
            return;
        }

        var (card, config) = pending;

        var confirmed = MessageBox.Show(Strings.LS_Nfc_FormatConfirm, "FloppyGames", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirmed != MessageBoxResult.Yes)
        {
            return;
        }

        FormatCardButton.Visibility = Visibility.Collapsed;

        try
        {
            if (_nfcCardWriter.WriteMagic(card.ReaderName, card.CardType, config))
            {
                WriteStatusText.Text = Strings.LS_Nfc_WriteSuccess(config.Title, card.Uid);
                _logger.Information(
                    "Cartão NFC UID {Uid} formatado e gravado via backdoor mágico para {Title} ({Platform}).",
                    card.Uid, config.Title, config.Platform);
            }
            else
            {
                WriteStatusText.Text = Strings.LS_Nfc_FormatFailed;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Falha ao formatar o cartão NFC UID {Uid}.", card.Uid);
            WriteStatusText.Text = Strings.LS_Nfc_FormatFailed;
        }
    }

    private void OnPrintLabelClicked(object sender, RoutedEventArgs e)
    {
        if (_selectedGame is null)
        {
            return;
        }

        var printWindow = new LabelPrintWindow(TitleBox.Text, CoverImage.Source as BitmapSource)
        {
            Owner = this,
        };
        printWindow.Show();
    }
}
