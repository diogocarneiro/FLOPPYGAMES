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
    private double _progressFraction;

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

        var isFloppy = mediaKind == MediaKind.Floppy;
        FloppyIcon.Visibility = isFloppy ? Visibility.Visible : Visibility.Collapsed;
        UsbIcon.Visibility = isFloppy ? Visibility.Collapsed : Visibility.Visible;
        MediaKindLabel.Text = SpaceOutLetters(isFloppy ? "DISQUETE DETETADA" : "PEN USB DETETADA");

        SizeText.Text = "TAMANHO   a verificar...";
        Crc32Text.Text = "CRC32     a calcular...";
        InstalledText.Text = "STEAM     a verificar...";

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
                CoverCard.Visibility = Visibility.Visible;
                BackgroundImage.Source = bitmap;
                BackgroundImage.Visibility = Visibility.Visible;
                return;
            }
            catch (NotSupportedException)
            {
                // Ficheiro de capa corrompido ou formato não suportado — segue sem capa.
            }
        }

        CoverCard.Visibility = Visibility.Collapsed;
        BackgroundImage.Visibility = Visibility.Collapsed;
    }

    public void SetStatus(string message) => StatusText.Text = message;

    /// <summary>Preenche tamanho/CRC32/estado de instalação assim que ficam disponíveis (calculados fora da UI thread).</summary>
    public void SetSummary(GameLaunchSummary summary)
    {
        SizeText.Text = $"TAMANHO   {FormatBytes(summary.MediaSizeBytes)}";
        Crc32Text.Text = $"CRC32     {summary.MediaCrc32:X8}";
        InstalledText.Text = summary.IsInstalledOnSteam
            ? $"STEAM     instalado ({FormatBytes(summary.InstalledSizeBytes ?? 0)})"
            : "STEAM     não instalado";
    }

    /// <summary>
    /// Arranca uma barra de progresso determinística com base na duração máxima real configurada
    /// (atraso de lançamento + timeout de deteção do processo) — nunca chega sozinha aos 100%,
    /// isso só acontece quando <see cref="ShowSuccessAndAutoClose"/> confirma o jogo em execução.
    /// </summary>
    public void StartProgress(TimeSpan estimatedDuration)
    {
        _progressEstimatedDuration = estimatedDuration <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : estimatedDuration;
        _progressStartUtc = DateTime.UtcNow;
        SetProgressFraction(0);
        _progressTimer.Start();
    }

    private void TickProgress()
    {
        var elapsed = DateTime.UtcNow - _progressStartUtc;
        var fraction = elapsed.TotalSeconds / _progressEstimatedDuration.TotalSeconds;
        SetProgressFraction(Math.Clamp(fraction, 0, 0.92));
    }

    private void SetProgressFraction(double fraction)
    {
        _progressFraction = fraction;
        var trackWidth = ProgressTrack.ActualWidth;
        if (trackWidth > 0)
        {
            ProgressFill.Width = trackWidth * fraction;
        }

        ProgressPercentText.Text = $"{fraction * 100:0}%";
    }

    public void ShowSuccessAndAutoClose(string message)
    {
        _progressTimer.Stop();
        StatusText.Text = message;
        SetProgressFraction(1);
        _autoCloseTimer.Interval = TimeSpan.FromSeconds(1.5);
        _autoCloseTimer.Start();
    }

    public void ShowErrorAndAutoClose(string message)
    {
        _progressTimer.Stop();
        StatusText.Text = message;
        StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
        ProgressFill.Background = System.Windows.Media.Brushes.OrangeRed;
        _autoCloseTimer.Interval = TimeSpan.FromSeconds(5);
        _autoCloseTimer.Start();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        // A primeira medição do layout só fica pronta depois do primeiro render — reaplica a
        // fração atual para o preenchimento em pixels não ficar preso a 0 caso StartProgress
        // tenha corrido antes da janela ter sequer uma ActualWidth.
        SetProgressFraction(_progressFraction);
    }

    private static string SpaceOutLetters(string text) => string.Join(' ', text.ToCharArray());

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
