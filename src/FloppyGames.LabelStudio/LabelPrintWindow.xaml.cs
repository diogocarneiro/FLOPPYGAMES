using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace FloppyGames.LabelStudio;

/// <summary>Desenho simples do label físico (capa + título) com impressão direta ou exportação para PNG.</summary>
public partial class LabelPrintWindow : Window
{
    public LabelPrintWindow(string title, BitmapSource? cover)
    {
        InitializeComponent();
        LabelTitleText.Text = title;
        LabelCoverImage.Source = cover;
    }

    private void OnPrintClicked(object sender, RoutedEventArgs e)
    {
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true)
        {
            return;
        }

        printDialog.PrintVisual(LabelSurface, "FloppyGames — Label");
        StatusText.Text = "Enviado para a impressora.";
    }

    private void OnExportPngClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Imagem PNG (*.png)|*.png",
            FileName = "label.png",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var size = new Size(LabelSurface.ActualWidth, LabelSurface.ActualHeight);
        LabelSurface.Measure(size);
        LabelSurface.Arrange(new Rect(size));

        var renderTarget = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
        renderTarget.Render(LabelSurface);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(renderTarget));

        using var stream = File.Create(dialog.FileName);
        encoder.Save(stream);

        StatusText.Text = $"Exportado para {dialog.FileName}.";
    }
}
