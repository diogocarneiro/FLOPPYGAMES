using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using FloppyGames.Core.Configuration;
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
        GamesStatusText.Text = $"A ler a biblioteca {PlatformDisplayName(platform)}...";

        try
        {
            var games = await Task.Run(() => ScanPlatform(platform));
            _allGames = [.. games];

            ApplyFilter(SearchBox.Text);
            GamesStatusText.Text = _allGames.Count == 0
                ? $"Nenhum jogo encontrado — confirma que o {PlatformDisplayName(platform)} está instalado e tens jogos instalados."
                : $"{_allGames.Count} jogo(s) encontrado(s).";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Falha ao ler a biblioteca {Platform}.", platform);
            GamesStatusText.Text = "Falha ao ler a biblioteca — ver logs.";
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
            ShowNoCoverMessage("A obter capa da Steam...");
            _ = LoadCoverAsync(appId);
        }
        else
        {
            ShowNoCoverMessage($"Sem capa automática para {PlatformDisplayName(game.Platform)} — escolhe uma imagem local abaixo.");
        }
    }

    private static string IdentifierLabelFor(GamePlatform platform) => platform switch
    {
        GamePlatform.Steam => "AppID (Steam)",
        GamePlatform.Epic => "Identificador (Epic: namespace:item:app)",
        GamePlatform.Gog => "ID (GOG)",
        _ => "Identificador",
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
                ShowNoCoverMessage("Sem capa encontrada na Steam — escolhe uma imagem local abaixo.");
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
            Filter = "Imagens (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
            Title = "Escolher capa",
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
            MessageBox.Show("Não foi possível ler essa imagem.", "FloppyGames", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            WriteStatusText.Text = "Escolhe uma unidade destino.";
            return;
        }

        if (string.IsNullOrWhiteSpace(TitleBox.Text) || string.IsNullOrWhiteSpace(ProcessBox.Text))
        {
            WriteStatusText.Text = "Título e Processo são obrigatórios.";
            return;
        }

        if (!int.TryParse(WatchTimeoutBox.Text, out var watchTimeout) || watchTimeout <= 0)
        {
            WriteStatusText.Text = "Timeout de arranque tem de ser um número inteiro positivo.";
            return;
        }

        if (!int.TryParse(LaunchDelayBox.Text, out var launchDelay) || launchDelay < 0)
        {
            WriteStatusText.Text = "Atraso de lançamento tem de ser um número inteiro não negativo.";
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
                WriteStatusText.Text = "Escrita cancelada.";
                return;
            }
        }

        try
        {
            _mediaWriter.Write(driveRoot, config, _coverBytes, config.Cover);
            WriteStatusText.Text = $"Disquete pronta: \"{config.Title}\" escrito em {driveRoot}.";
            _logger.Information(
                "GAME.INI escrito em {Drive} para {Title} ({Platform}).", driveRoot, config.Title, config.Platform);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error(ex, "Falha ao escrever para {Drive}.", driveRoot);
            WriteStatusText.Text = "Falha ao escrever para o suporte — ver logs.";
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
