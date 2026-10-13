using System.Drawing.Drawing2D;

namespace CursorRotator.UI;

/// <summary>Icono de bandeja (NotifyIcon) con menú y globo informativo.</summary>
public sealed class TrayManager : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _toggleItem;
    private bool _disposed;

    public TrayManager(Form owner, Action onShow, Action onToggle, Action onOnce, Action onExit)
    {
        _toggleItem = new ToolStripMenuItem("Activar rotación", null, (_, _) => onToggle());
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Mostrar", null, (_, _) => onShow()));
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new ToolStripMenuItem("Aplicar siguiente", null, (_, _) => onOnce()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Salir", null, (_, _) => onExit()));

        _icon = new NotifyIcon
        {
            Icon = BuildIcon(),
            Text = "CursorRotator",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => onShow();
        SyncToggleText(false);
    }

    public void SyncToggleText(bool running)
    {
        _toggleItem.Text = running ? "Pausar rotación" : "Activar rotación";
        _icon.Text = running ? "CursorRotator (rotando)" : "CursorRotator (pausado)";
    }

    public void ShowBalloon(string text) =>
        _icon.ShowBalloonTip(3000, "CursorRotator", text, ToolTipIcon.Info);

    private static Icon BuildIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(Color.FromArgb(0x7C, 0x5C, 0xFF));
            g.FillRoundedRectangle(brush, new Rectangle(1, 1, 30, 30), 7);
            // Flecha de cursor blanca.
            g.FillPolygon(Brushes.White, [
                new Point(9, 6), new Point(9, 23),
                new Point(14, 18), new Point(17, 24),
                new Point(20, 23), new Point(17, 17),
                new Point(22, 17),
            ]);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _icon.Visible = false;
        _icon.Dispose();
    }
}

internal static class GraphicsExt
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle r, int radius)
    {
        using var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
