using System.Drawing;
using System.Drawing.Drawing2D;

namespace FloppyGames.Agent;

public enum TrayIconState
{
    Idle,
    Loading,
    Running,
}

/// <summary>
/// Desenha os ícones de bandeja em código (um pequeno "disquete" colorido por estado),
/// para o Agent não depender de ficheiros .ico externos.
/// </summary>
public static class TrayIconFactory
{
    public static Icon Create(TrayIconState state)
    {
        var accent = state switch
        {
            TrayIconState.Loading => Color.FromArgb(255, 240, 173, 78),
            TrayIconState.Running => Color.FromArgb(255, 76, 175, 80),
            _ => Color.FromArgb(255, 150, 150, 150),
        };

        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var body = new SolidBrush(accent);
            using var outline = new Pen(Color.FromArgb(255, 30, 30, 30), 1.5f);
            using var label = new SolidBrush(Color.White);

            // Silhueta simplificada de uma disquete 3.5" (canto superior direito cortado).
            var shape = new[]
            {
                new Point(4, 4), new Point(23, 4), new Point(28, 9),
                new Point(28, 28), new Point(4, 28),
            };
            g.FillPolygon(body, shape);
            g.DrawPolygon(outline, shape);

            g.FillRectangle(label, 9, 15, 14, 9);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }
}
