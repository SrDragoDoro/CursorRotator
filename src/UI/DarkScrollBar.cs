using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace CursorRotator.UI;

/// <summary>
/// Barra de desplazamiento vertical minimalista dibujada con GDI+,
/// acorde al tema (thumb redondeado #45455A en oscuro).
/// </summary>
public sealed class DarkScrollBar : Control
{
    private int _maximum = 100;
    private int _value;
    private Color _thumb = Color.FromArgb(0x45, 0x45, 0x5A);
    private Color _thumbHover = Color.FromArgb(0x6A, 0x6A, 0x85);
    private bool _hover, _drag;
    private int _grabOffset;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minimum { get; set; }
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int LargeChange { get; set; } = 50;
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SmallChange { get; set; } = 10;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum
    {
        get => _maximum;
        set { _maximum = Math.Max(0, value); Value = _value; Invalidate(); }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            int v = Math.Clamp(value, Minimum, Math.Max(Minimum, Maximum - LargeChange));
            if (v == _value) return;
            _value = v;
            Invalidate();
            Scrolled?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler Scrolled;

    public DarkScrollBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = false;
        Width = 12;
    }

    public void ApplyTheme(bool dark, Color back)
    {
        BackColor = back;
        _thumb = dark ? Color.FromArgb(0x45, 0x45, 0x5A) : Color.FromArgb(0xB4, 0xB4, 0xC8);
        _thumbHover = dark ? Color.FromArgb(0x6A, 0x6A, 0x85) : Color.FromArgb(0x8A, 0x8A, 0xA5);
        Invalidate();
    }

    private void Metrics(out int thumbY, out int thumbH)
    {
        int trackH = Math.Max(1, Height - 8);
        thumbH = Maximum <= 0 ? trackH
            : Math.Max(26, LargeChange * trackH / Math.Max(1, Maximum));
        thumbH = Math.Min(thumbH, trackH);
        int range = Maximum - LargeChange;
        thumbY = 4 + (range <= 0 ? 0 : _value * (trackH - thumbH) / range);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Metrics(out int y, out int h);
        using var path = RoundedRect(new Rectangle(Width / 2 - 3, y, 6, h), 3);
        using var b = new SolidBrush(_hover || _drag ? _thumbHover : _thumb);
        e.Graphics.FillPath(b, path);
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Metrics(out int y, out int h);
        if (e.Y >= y && e.Y <= y + h)
        {
            _drag = true;
            _grabOffset = e.Y - y;
            Capture = true;
        }
        else
        {
            Value += e.Y < y ? -LargeChange : LargeChange; // salto de página
        }
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_drag) return;
        int trackH = Math.Max(1, Height - 8);
        Metrics(out _, out int h);
        int range = Maximum - LargeChange;
        if (range > 0)
            Value = (e.Y - _grabOffset - 4) * range / Math.Max(1, trackH - h);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _drag = false;
        Capture = false;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        Invalidate();
    }
}
