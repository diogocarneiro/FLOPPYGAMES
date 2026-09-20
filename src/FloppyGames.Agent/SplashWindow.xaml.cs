using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FloppyGames.Core.Media;

namespace FloppyGames.Agent;

/// <summary>
/// Janela de "a carregar" mostrada entre o reconhecimento do suporte e a confirmação
/// (ou falha) do arranque do jogo. Fecha-se sozinha pouco depois de chegar a um estado final.
/// </summary>
public partial class SplashWindow : Window
{
    private readonly DispatcherTimer _autoCloseTimer = new();
    private readonly DispatcherTimer _progressTimer = new();
    private DateTime _progressStartUtc;
    private TimeSpan _progressEstimatedDuration = TimeSpan.FromSeconds(1);

    public SplashWindow()
    {
        InitializeComponent();
        _autoCloseTimer.Tick += (_, _) =>
        {
            _autoCloseTimer.Stop();
            Close();
        };

        _progressTimer.Interval = TimeSpan.FromMilliseconds(100);
        _progressTimer.Tick += (_, _) => TickProgress();
    }

    public void SetGame(string title, string? description, string? coverPath, MediaKind mediaKind)
    {
        TitleText.Text = title;
        StatusText.Text = "A preparar...";

        DescriptionText.Text = description ?? string.Empty;
        DescriptionText.Visibility = string.IsNullOrWhiteSpace(description) ? Visibility.Collapsed : Visibility.Visible;

        FloppyIcon.Visibility = mediaKind == MediaKind.Floppy ? Visibility.Visible : Visibility.Collapsed;
        UsbIcon.Visibility = mediaKind == MediaKind.Usb ? Visibility.Visible : Visibility.Collapsed;

        SizeText.Text = "Tamanho: a verificar...";
        Crc32Text.Text = "CRC32: a calcular...";
        InstalledText.Text = "Instalado: a verificar...";

        if (!string.IsNullOrWhiteSpace(coverPath) && File.Exists(coverPath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(coverPath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                CoverImage.Source = bitmap;
                CoverImage.Visibility = Visibility.Visible;
                BackgroundImage.Source = bitmap;
                BackgroundImage.Visibility = Visibility.Visible;
                return;
            }
            catch (NotSupportedException)
            {
                // Ficheiro de capa corrompido ou formato não suportado — segue sem capa.
            }
        }

        CoverImage.Visibility = Visibility.Collapsed;
        BackgroundImage.Visibility = Visibility.Collapsed;
    }

    public void SetStatus(string message) => StatusText.Text = message;

    /// <summary>Preenche tamanho/CRC32/estado de instalação assim que ficam disponíveis (calculados fora da UI thread).</summary>
    public void SetSummary(GameLaunchSummary summary)
    {
        SizeText.Text = $"Suporte: {FormatBytes(summary.MediaSizeBytes)}";
        Crc32Text.Text = $"CRC32: {summary.MediaCrc32:X8}";
        InstalledText.Text = summary.IsInstalledOnSteam
            ? $"Instalado: Sim ({FormatBytes(summary.InstalledSizeBytes ?? 0)})"
            : "Instalado: Não";
    }

    /// <summary>
    /// Arranca uma barra de progresso determinística com base na duração máxima real configurada
    /// (atraso de lançamento + timeout de deteção do processo) — nunca chega sozinha aos 100%,
    /// isso só acontece quando <see cref="ShowSuccessAndAutoClose"/> confirma o jogo em execução.
    /// </summary>
    public void StartProgress(TimeSpan estimatedDuration)
    {
        ProgressIndicator.IsIndeterminate = false;
        ProgressIndicator.Value = 0;

        _progressEstimatedDuration = estimatedDuration <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : estimatedDuration;
        _progressStartUtc = DateTime.UtcNow;
        _progressTimer.Start();
    }

    private void TickProgress()
    {
        var elapsed = DateTime.UtcNow - _progressStartUtc;
        var fraction = elapsed.TotalSeconds / _progressEstimatedDuration.TotalSeconds;
        ProgressIndicator.Value = Math.Clamp(fraction * 100, 0, 92);
    }

    public void ShowSuccessAndAutoClose(string message)
    {
        _progressTimer.Stop();
        StatusText.Text = message;
        ProgressIndicator.IsIndeterminate = false;
        ProgressIndicator.Value = ProgressIndicator.Maximum;
        _autoCloseTimer.Interval = TimeSpan.FromSeconds(1.5);
        _autoCloseTimer.Start();
    }

    public void ShowErrorAndAutoClose(string message)
    {
        _progressTimer.Stop();
        StatusText.Text = message;
        StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
        ProgressIndicator.IsIndeterminate = false;
        _autoCloseTimer.Interval = TimeSpan.FromSeconds(5);
        _autoCloseTimer.Start();
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.#} {units[unitIndex]}";
    }
}
