using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using FloppyGames.Core.Configuration;
using FloppyGames.Core.Logging;
using FloppyGames.Core.Media;
using FloppyGames.Core.Steam;
using Microsoft.Win32;
using Serilog;

namespace FloppyGames.LabelStudio;

public partial class MainWindow : Window
{
    private readonly ILogger _logger;
    private readonly SteamLibraryScanner _scanner;
    private readonly GameExecutableFinder _executableFinder;
    private readonly ICoverArtProvider _coverArtProvider;
    private readonly FloppyMediaWriter _mediaWriter;

    private List<InstalledSteamGame> _allGames = [];
    private byte[]? _coverBytes;
    private string _coverFileName = "cover.jpg";
    private CancellationTokenSource? _coverFetchCts;

    public MainWindow()
    {
        InitializeComponent();

        _logger = LoggingBootstrapper.CreateLogger("LabelStudio");
        _logger.Information("FloppyGames Label Studio iniciado.");

        var fileSystem = new FileSystemSteamFileSystem();
        _scanner = new SteamLibraryScanner(fileSystem, new RegistrySteamPathProvider());
        _executableFinder = new GameExecutableFinder(fileSystem);
        _coverArtProvider = new SteamCdnCoverArtProvider();
        _mediaWriter = new FloppyMediaWriter(new FileSystemDriveInspector());

        RefreshDrives();
        _ = LoadGamesAsync();
    }

    private async Task LoadGamesAsync()
    {
        GamesStatusText.Text = "A ler a biblioteca Steam...";

        try
        {
            var games = await Task.Run(() => _scanner.ScanInstalledGames());
            _allGames = [.. games];

            ApplyFilter(SearchBox.Text);
            GamesStatusText.Text = _allGames.Count == 0
                ? "Nenhum jogo encontrado — confirma que a Steam está instalada e tens jogos instalados."
                : $"{_allGames.Count} jogo(s) encontrado(s).";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Falha ao ler a biblioteca Steam.");
            GamesStatusText.Text = "Falha ao ler a biblioteca Steam — ver logs.";
        }
    }

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
        if (GamesList.SelectedItem is not InstalledSteamGame game)
        {
            DetailsPanel.IsEnabled = false;
            return;
        }

        DetailsPanel.IsEnabled = true;
        TitleBox.Text = game.Name;
        AppIdText.Text = game.AppId.ToString();
        ProcessBox.Text = _executableFinder.FindSuggestedExecutable(game.InstallPath) ?? string.Empty;
        DescriptionBox.Text = string.Empty;
        WriteStatusText.Text = string.Empty;

        _ = LoadCoverAsync(game.AppId);
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
            if (cts.IsCancellationRequested || bytes is null)
            {
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
        if (GamesList.SelectedItem is not InstalledSteamGame)
        {
            return;
        }

        if (DriveCombo.SelectedItem is not string driveRoot)
        {
            WriteStatusText.Text = "Escolhe uma unidade destino.";
            return;
        }

        if (!int.TryParse(AppIdText.Text, out var appId))
        {
            WriteStatusText.Text = "AppID inválido.";
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
            AppId = appId,
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
                "GAME.INI escrito em {Drive} para {Title} (AppID {AppId}).", driveRoot, config.Title, config.AppId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error(ex, "Falha ao escrever para {Drive}.", driveRoot);
            WriteStatusText.Text = "Falha ao escrever para o suporte — ver logs.";
        }
    }

    private void OnPrintLabelClicked(object sender, RoutedEventArgs e)
    {
        if (GamesList.SelectedItem is not InstalledSteamGame)
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
