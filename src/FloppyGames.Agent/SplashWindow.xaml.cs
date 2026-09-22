using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FloppyGames.Core.Configuration;
using FloppyGames.Core.Localization;
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

    public void SetGame(string title, string? description, string? coverPath, MediaKind mediaKind, GamePlatform platform)
    {
        TitleText.Text = title;
        StatusText.Text = Strings.Splash_Preparing;

        DescriptionText.Text = description ?? string.Empty;
        DescriptionText.Visibility = string.IsNullOrWhiteSpace(description) ? Visibility.Collapsed : Visibility.Visible;

        FloppyIcon.Visibility = mediaKind == MediaKind.Floppy ? Visibility.Visible : Visibility.Collapsed;
        UsbIcon.Visibility = mediaKind == MediaKind.Usb ? Visibility.Visible : Visibility.Collapsed;
        NfcIcon.Visibility = mediaKind == MediaKind.Nfc ? Visibility.Visible : Visibility.Collapsed;
        MediaKindLabel.Text = SpaceOutLetters(mediaKind switch
        {
            MediaKind.Floppy => Strings.Splash_FloppyDetected,
            MediaKind.Nfc => Strings.Splash_NfcDetected,
            _ => Strings.Splash_UsbDetected,
        });

        CardIdText.Visibility = Visibility.Collapsed;

        SizeText.Text = FormatStatLine(Strings.Splash_StatSupport, Strings.Splash_Checking);
        InstalledText.Text = FormatStatLine(platform.ToString().ToUpperInvariant(), Strings.Splash_Checking);
        BuildText.Text = FormatStatLine(Strings.Splash_StatBuild, Strings.Splash_Checking);
        UpdatedText.Text = FormatStatLine(Strings.Splash_StatUpdated, Strings.Splash_Checking);
        PlaytimeText.Text = FormatStatLine(Strings.Splash_StatPlaytime, Strings.Splash_Checking);
        LastSessionText.Text = FormatStatLine(Strings.Splash_StatLastSession, Strings.Splash_Checking);
        BuildText.Visibility = Visibility.Visible;
        UpdatedText.Visibility = Visibility.Visible;
        PlaytimeText.Visibility = Visibility.Visible;
        LastSessionText.Visibility = Visibility.Visible;

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

    /// <summary>Mostra o UID do cartão NFC que despoletou o lançamento — sem efeito para disquete/USB.</summary>
    public void SetCardId(string? cardUid) => SetOptionalStatLine(CardIdText, Strings.Splash_StatCardId, cardUid);

    /// <summary>
    /// Liga/desliga o varrimento CRT. Quando ligado, desliza continuamente devagar (sem picos
    /// nem estroboscopia) — deliberadamente lento por acessibilidade, nunca um "flicker" rápido.
    /// </summary>
    public void SetCrtEffectEnabled(bool enabled)
    {
        ScanlineOverlay.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;

        if (!enabled)
        {
            ScanlineTransform.BeginAnimation(TranslateTransform.YProperty, null);
            return;
        }

        var scroll = new DoubleAnimation
        {
            From = 0,
            To = 4,
            Duration = TimeSpan.FromSeconds(1.2),
            RepeatBehavior = RepeatBehavior.Forever,
        };
        ScanlineTransform.BeginAnimation(TranslateTransform.YProperty, scroll);
    }

    /// <summary>
    /// Preenche tamanho/estado de instalação e, quando disponível, build/tempo de jogo/conquistas
    /// (exclusivos da Steam) assim que ficam disponíveis (calculados fora da UI thread). Cada linha
    /// esconde-se de forma independente quando o respetivo dado não existe para a plataforma —
    /// a Epic, por exemplo, mostra tamanho instalado mas não tempo de jogo.
    /// </summary>
    public void SetSummary(GameLaunchSummary summary)
    {
        SizeText.Text = FormatStatLine(Strings.Splash_StatSupport, FormatBytes(summary.MediaSizeBytes));

        var platformLabel = summary.Platform.ToString().ToUpperInvariant();
        var installedValue = summary.IsInstalled
            ? summary.InstalledSizeBytes is { } sizeBytes ? $"{Strings.Splash_Installed} ({FormatBytes(sizeBytes)})" : Strings.Splash_Installed
            : Strings.Splash_NotInstalled;
        InstalledText.Text = FormatStatLine(platformLabel, installedValue);

        SetOptionalStatLine(BuildText, Strings.Splash_StatBuild, summary.BuildId);
        SetOptionalStatLine(UpdatedText, Strings.Splash_StatUpdated, summary.LastUpdatedUtc is null ? null : FormatDate(summary.LastUpdatedUtc));
        SetOptionalStatLine(PlaytimeText, Strings.Splash_StatPlaytime, summary.PlaytimeMinutes is null ? null : FormatPlaytime(summary.PlaytimeMinutes));
        SetOptionalStatLine(LastSessionText, Strings.Splash_StatLastSession, summary.LastPlayedUtc is null ? null : FormatDate(summary.LastPlayedUtc));

        if (summary.Achievements is { } achievements)
        {
            AchievementsText.Text = FormatStatLine(Strings.Splash_StatAchievements, $"{achievements.Unlocked}/{achievements.Total}");
            AchievementsText.Visibility = Visibility.Visible;
        }
        else
        {
            AchievementsText.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Versão simplificada de <see cref="SetSummary"/> para lançamentos via cartão NFC: não há
    /// biblioteca de plataforma a consultar sem duplicar os scanners de Steam/Epic/GOG só para
    /// isto — mostra apenas o espaço ocupado no próprio cartão, e esconde as linhas que só fazem
    /// sentido com uma biblioteca instalada localmente.
    /// </summary>
    public void SetNfcCardSummary(long cardBytesUsed)
    {
        SizeText.Text = FormatStatLine(Strings.Splash_StatSupport, FormatBytes(cardBytesUsed));
        InstalledText.Visibility = Visibility.Collapsed;
        BuildText.Visibility = Visibility.Collapsed;
        UpdatedText.Visibility = Visibility.Collapsed;
        PlaytimeText.Visibility = Visibility.Collapsed;
        LastSessionText.Visibility = Visibility.Collapsed;
        AchievementsText.Visibility = Visibility.Collapsed;
    }

    private static void SetOptionalStatLine(TextBlock textBlock, string label, string? value)
    {
        if (value is null)
        {
            textBlock.Visibility = Visibility.Collapsed;
            return;
        }

        textBlock.Text = FormatStatLine(label, value);
        textBlock.Visibility = Visibility.Visible;
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

    // 18 caracteres cobre a label mais longa entre os 5 idiomas ("DERNIÈRE SESSION", FR, 17 chars)
    // com pelo menos um espaço de intervalo — labels mais compridas simplesmente não ficam alinhadas.
    private static string FormatStatLine(string label, string value) => string.Format("{0,-18}{1}", label, value);

    private static string FormatDate(DateTime? utc) => utc is null
        ? Strings.Splash_UnknownDate
        : utc.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    private static string FormatPlaytime(long? minutes)
    {
        if (minutes is null)
        {
            return Strings.Splash_PlaytimeUnavailable;
        }

        var hours = minutes.Value / 60;
        var mins = minutes.Value % 60;
        return hours > 0 ? $"{hours}h {mins}min" : $"{mins}min";
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
