using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using FloppyGames.Core.Configuration;
using FloppyGames.Core.Localization;
using FloppyGames.Core.Logging;
using FloppyGames.Core.Media;
using FloppyGames.Core.Platforms;
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

    private List<DiscoveredGame> _allGames = [];
    private DiscoveredGame? _selectedGame;
    private byte[]? _coverBytes;
    private string _coverFileName = "cover.jpg";
    private CancellationTokenSource? _coverFetchCts;

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

        RefreshDrives();

        // Definido em código (não no XAML) para só disparar OnPlatformChanged depois dos scanners
        // acima estarem prontos — se viesse do XAML, o SelectionChanged correria durante o
        // InitializeComponent(), antes destes campos serem atribuídos.
        PlatformCombo.SelectedIndex = 0;
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
        TargetDriveLabel.Text = Strings.LS_TargetDrive;
        RefreshDrivesButton.Content = Strings.LS_RefreshButton;
        WriteButton.Content = Strings.LS_WriteButton;
        PrintLabelButton.Content = Strings.LS_PrintButton;
        ChooseLocalCoverButton.Content = Strings.LS_ChooseLocalCoverButton;
    }

    private GamePlatform SelectedPlatform() => PlatformCombo.SelectedIndex switch
    {
        1 => GamePlatform.Epic,
        2 => GamePlatform.Gog,
        _ => GamePlatform.Steam,
    };

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

        _coverFetchCts?.Cancel();
        CoverImage.Source = null;
        _coverBytes = null;
        _coverFileName = "cover.jpg";

        // Só a Steam tem um CDN de capas público e sem autenticação — Epic/GOG ficam com a
        // escolha manual de imagem local. Sem isto ficar explícito, a caixa vazia parece avariada.
        if (game.Platform == GamePlatform.Steam && game.SteamAppId is { } appId)
        {
            ShowNoCoverMessage(Strings.LS_FetchingSteamCover);
            _ = LoadCoverAsync(appId);
        }
        else
        {
            ShowNoCoverMessage(Strings.LS_NoAutoCoverForPlatform(PlatformDisplayName(game.Platform)));
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

    private async Task LoadCoverAsync(int appId)
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
                ShowNoCoverMessage(Strings.LS_NoCoverFoundSteam);
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

    private void OnWriteClicked(object sender, RoutedEventArgs e)
    {
        if (_selectedGame is not { } selectedGame)
        {
            return;
        }

        if (DriveCombo.SelectedItem is not string driveRoot)
        {
            WriteStatusText.Text = Strings.LS_ChooseTargetDrive;
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
