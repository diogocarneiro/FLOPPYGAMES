using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FloppyGames.Agent;

/// <summary>
/// Janela de "a carregar" mostrada entre o reconhecimento do suporte e a confirmação
/// (ou falha) do arranque do jogo. Fecha-se sozinha pouco depois de chegar a um estado final.
/// </summary>
public partial class SplashWindow : Window
{
    private readonly DispatcherTimer _autoCloseTimer = new();

    public SplashWindow()
    {
        InitializeComponent();
        _autoCloseTimer.Tick += (_, _) =>
        {
            _autoCloseTimer.Stop();
            Close();
        };
    }

    public void SetGame(string title, string? coverPath)
    {
        TitleText.Text = title;
        StatusText.Text = "A preparar...";

        if (!string.IsNullOrWhiteSpace(coverPath) && File.Exists(coverPath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(coverPath, UriKind.Absolute);
                bitmap.EndInit();
                CoverImage.Source = bitmap;
                CoverImage.Visibility = Visibility.Visible;
                return;
            }
            catch (NotSupportedException)
            {
                // Ficheiro de capa corrompido ou formato não suportado — segue sem capa.
            }
        }

        CoverImage.Visibility = Visibility.Collapsed;
    }

    public void SetStatus(string message) => StatusText.Text = message;

    public void ShowSuccessAndAutoClose(string message)
    {
        StatusText.Text = message;
        ProgressIndicator.IsIndeterminate = false;
        ProgressIndicator.Value = ProgressIndicator.Maximum;
        _autoCloseTimer.Interval = TimeSpan.FromSeconds(1.5);
        _autoCloseTimer.Start();
    }

    public void ShowErrorAndAutoClose(string message)
    {
        StatusText.Text = message;
        StatusText.Foreground = Brushes.OrangeRed;
        ProgressIndicator.IsIndeterminate = false;
        _autoCloseTimer.Interval = TimeSpan.FromSeconds(5);
        _autoCloseTimer.Start();
    }
}
