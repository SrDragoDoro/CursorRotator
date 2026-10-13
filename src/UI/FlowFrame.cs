using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CursorRotator.UI;

/// <summary>
/// Panel-marco con borde degradado animado (flujo) en la paleta del cursor.
/// Activo = animado con los colores del cursor; inactivo = marco gris fijo.
/// Un único timer compartido repinta todos los marcos (~16 FPS).
/// </summary>
public sealed class FlowFramePanel : Panel
{
    private List<Color> _palette = CursorPalette.DefaultPalette();
    private bool _animated = true;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Animated
    {
        get => _animated;
        set { _animated = value; Invalidate(); }
    }

    /// <summary>Interruptor global del movimiento (todas las instancias).</summary>
    public static bool MotionEnabled { get; set; } = true;

    public void SetPalette(IEnumerable<Color> colors)
    {
        _palette = CursorPalette.EnsureTwo(colors);
        Invalidate();
    }

    public FlowFramePanel()
    {
        Padding = new Padding(3);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        FlowAnimator.Register(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) FlowAnimator.Unregister(this);
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
        using var path = Rounded(r, 8);
        if (_animated && _palette.Count >= 2)
        {
            // Degradado de la paleta ORBITANDO el marco (una vuelta ~10 s)
            // o fijo si el movimiento global está apagado.
            float angle = MotionEnabled ? FlowAnimator.Phase * 360f : 0f;
            var bounds = new Rectangle(0, 0, Width, Height);
            using var brush = new LinearGradientBrush(bounds,
                _palette[0], _palette[_palette.Count - 1], angle);
            brush.InterpolationColors = SpreadBlend(_palette.ToArray());
            using var pen = new Pen(brush, 3);
            e.Graphics.DrawPath(pen, path);
        }
        else
        {
            using var pen = new Pen(Color.FromArgb(0x66, 0x66, 0x72), 2);
            e.Graphics.DrawPath(pen, path);
        }
    }

    /// <summary>Reparte la paleta en todo el rango 0..1.</summary>
    private static ColorBlend SpreadBlend(Color[] colors)
    {
        int n = Math.Max(2, colors.Length);
        var cols = new Color[n];
        var pos = new float[n];
        for (int i = 0; i < n; i++)
        {
            cols[i] = colors[i % colors.Length];
            pos[i] = i / (float)(n - 1);
        }
        return new ColorBlend { Colors = cols, Positions = pos };
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
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

/// <summary>
/// Un único timer de UI repinta los marcos animados y avanza la fase
/// orbital (~10 s por vuelta). Sin marcos vivos = apagado.
/// </summary>
internal static class FlowAnimator
{
    private static System.Windows.Forms.Timer _timer;
    private static readonly List<WeakReference<Control>> _refs = new();
    private static float _phase;

    public static float Phase => _phase;

    public static void Register(Control c)
    {
        Prune();
        foreach (var r in _refs)
            if (r.TryGetTarget(out var t) && ReferenceEquals(t, c)) return;
        _refs.Add(new WeakReference<Control>(c));
        EnsureTimer();
    }

    public static void Unregister(Control c)
    {
        for (int i = _refs.Count - 1; i >= 0; i--)
            if (!_refs[i].TryGetTarget(out var t) || ReferenceEquals(t, c))
                _refs.RemoveAt(i);
        if (_refs.Count == 0) StopTimer();
    }

    private static void EnsureTimer()
    {
        if (_timer is not null) return;
        _timer = new System.Windows.Forms.Timer { Interval = 60 };
        _timer.Tick += (_, _) =>
        {
            _phase = (_phase + 0.006f) % 1f;
            Prune();
            if (_refs.Count == 0) { StopTimer(); return; }
            foreach (var r in _refs)
                if (r.TryGetTarget(out var c) && !c.IsDisposed && c.Visible
                    && c is FlowFramePanel f && f.Animated && FlowFramePanel.MotionEnabled)
                    try { c.Invalidate(); } catch { }
        };
        _timer.Start();
    }

    private static void StopTimer()
    {
        try { _timer?.Stop(); } catch { }
        try { _timer?.Dispose(); } catch { }
        _timer = null;
    }

    private static void Prune()
    {
        for (int i = _refs.Count - 1; i >= 0; i--)
            if (!_refs[i].TryGetTarget(out var t) || t.IsDisposed)
                _refs.RemoveAt(i);
    }
}

/// <summary>Paleta de un bitmap (mismas reglas del histograma de color).</summary>
internal static class CursorPalette
{
    public static List<Color> DefaultPalette() => new()
    {
        Color.FromArgb(0x7C, 0x5C, 0xFF),
        Color.FromArgb(0x46, 0xDC, 0xFF),
    };

    public static List<Color> EnsureTwo(IEnumerable<Color> colors)
    {
        var list = (colors ?? Enumerable.Empty<Color>()).Take(4).ToList();
        if (list.Count >= 2) return list;
        var b = list.Count == 1 ? list[0] : Color.FromArgb(0x7C, 0x5C, 0xFF);
        return new List<Color>
        {
            b,
            Color.FromArgb(
                Math.Min(255, b.R + (int)((255 - b.R) * 0.45)),
                Math.Min(255, b.G + (int)((255 - b.G) * 0.45)),
                Math.Min(255, b.B + (int)((255 - b.B) * 0.45))),
        };
    }

    /// <summary>Top-3 colores del bitmap (sin leer disco: usa los píxeles).</summary>
    public static List<Color> PaletteOf(Bitmap bmp, int max = 3)
    {
        try
        {
            var hist = new Dictionary<int, (long R, long G, long B, long N)>();
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                int bytes = Math.Abs(data.Stride) * data.Height;
                var buf = new byte[bytes];
                Marshal.Copy(data.Scan0, buf, 0, bytes);
                for (int i = 0; i < bytes; i += 4)
                {
                    int a = buf[i + 3];
                    if (a < 200) continue;
                    int r = buf[i + 2], g = buf[i + 1], b = buf[i];
                    if (r < 20 && g < 20 && b < 20) continue;
                    if (r > 245 && g > 245 && b > 245) continue;
                    int key = (r >> 4 << 8) | (g >> 4 << 4) | (b >> 4);
                    var e = hist.TryGetValue(key, out var v) ? v : (0, 0, 0, 0);
                    e.R += r; e.G += g; e.B += b; e.N++;
                    hist[key] = e;
                }
            }
            finally { bmp.UnlockBits(data); }

            var result = new List<Color>();
            var picked = new List<(int R, int G, int B)>();
            foreach (var kv in hist.OrderByDescending(kv => kv.Value.N))
            {
                int qr = (kv.Key >> 8) & 0xF, qg = (kv.Key >> 4) & 0xF, qb = kv.Key & 0xF;
                if (picked.Any(p => Math.Abs(p.R - qr) < 2
                                 && Math.Abs(p.G - qg) < 2 && Math.Abs(p.B - qb) < 2))
                    continue;
                picked.Add((qr, qg, qb));
                var v = kv.Value;
                result.Add(Color.FromArgb((int)(v.R / v.N), (int)(v.G / v.N), (int)(v.B / v.N)));
                if (result.Count >= Math.Clamp(max, 1, 5)) break;
            }
            return result.Count > 0 ? result : DefaultPalette();
        }
        catch { return DefaultPalette(); }
    }
}
