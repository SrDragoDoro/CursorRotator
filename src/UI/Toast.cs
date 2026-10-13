namespace CursorRotator.UI;

/// <summary>
/// Aviso flotante no intrusivo (estilo toast): aparece abajo-derecha de la
/// ventana dueña, se atenúa solo y no roba el foco. Solo uno visible a la
/// vez (el nuevo cierra al anterior).
/// </summary>
public sealed class Toast : Form
{
    private static Toast _current;
    private readonly System.Windows.Forms.Timer _life = new() { Interval = 50 };
    private double _ttl = 1.8;

    public static void ShowToast(Control owner, string message, bool dark)
    {
        try { _current?.Close(); } catch { }
        _current = null;
        try
        {
            var t = new Toast(message, dark);
            var form = owner.FindForm() ?? owner as Form;
            if (form is not null)
            {
                // Esquina SUPERIOR derecha: no tapa botones ni listas.
                Point p = form.PointToScreen(new Point(
                    Math.Max(0, form.ClientSize.Width - t.Width - 16), 16));
                var area = Screen.FromControl(form).WorkingArea;
                p.X = Math.Clamp(p.X, area.Left, Math.Max(area.Left, area.Right - t.Width));
                p.Y = Math.Clamp(p.Y, area.Top, Math.Max(area.Top, area.Bottom - t.Height));
                t.Location = p;
                t.Show(form);
            }
            else t.Show();
            _current = t;
        }
        catch { }
    }

    private Toast(string message, bool dark)
    {
        Theme.Palettes(dark, out var bg, out var card, out var fg, out _);
        Text = "";
        // Compacto y ajustado al texto (mín. 200, máx. 420).
        int w;
        try
        {
            using var g = CreateGraphics();
            var sz = TextRenderer.MeasureText(g, message,
                new Font("Segoe UI", 8.5f),
                new Size(388, 40),
                TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            w = Math.Clamp(sz.Width + 32, 200, 420);
        }
        catch { w = 260; }
        Size = new Size(w, 38);
        MinimumSize = Size;
        MaximumSize = Size;
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = card;
        Opacity = 0.96;

        var lbl = new Label
        {
            Text = message,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = fg,
            BackColor = card,
            AutoEllipsis = true,
        };
        lbl.Click += (_, _) => Close();
        Controls.Add(lbl);
        Click += (_, _) => Close();

        _life.Tick += (_, _) =>
        {
            _ttl -= 0.05;
            if (_ttl <= 0.5)
                Opacity = Math.Max(0, _ttl * 2);
            if (_ttl <= 0)
            {
                _life.Stop();
                Close();
            }
        };
        Shown += (_, _) => _life.Start();
        FormClosed += (_, _) =>
        {
            _life.Stop();
            _life.Dispose();
            if (_current == this) _current = null;
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_NOACTIVATE = 0x08000000;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // Marco marcado: borde exterior oscuro + interior acento, redondeado.
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var outer = new Pen(Color.FromArgb(0, 0, 0), 2);
        using var inner = new Pen(Theme.Accent, 3);
        using var path = RoundedRect(new Rectangle(2, 2, Width - 5, Height - 5), 9);
        e.Graphics.DrawPath(outer, path);
        using var path2 = RoundedRect(new Rectangle(5, 5, Width - 11, Height - 11), 6);
        e.Graphics.DrawPath(inner, path2);
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { path.AddRectangle(r); return path; }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
