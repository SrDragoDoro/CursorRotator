using System.Runtime.InteropServices;

namespace CursorRotator.UI;

/// <summary>
/// Lista con barra nativa oculta (ShowScrollBar) y <see cref="DarkScrollBar"/>
/// sincronizado vía LB_GETCOUNT / LB_GETTOPINDEX. Estética oscura total.
/// </summary>
public sealed class DarkListBox : Panel
{
    private const int SB_VERT = 1;
    private const int LB_GETCOUNT = 0x018B;
    private const int LB_GETTOPINDEX = 0x018E;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, [MarshalAs(UnmanagedType.Bool)] bool bShow);

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private readonly CheckedListBox _inner = new()
    {
        BorderStyle = BorderStyle.None,
        IntegralHeight = false,
        Dock = DockStyle.Fill,
        CheckOnClick = true,
    };
    private readonly DarkScrollBar _bar = new() { Dock = DockStyle.Right, Visible = false };
    private readonly System.Windows.Forms.Timer _sync = new() { Interval = 300 };
    private bool _syncing;

    public CheckedListBox Inner => _inner;
    public DarkScrollBar Bar => _bar;

    public DarkListBox()
    {
        BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(_inner);
        Controls.Add(_bar);
        _inner.HandleCreated += (_, _) => ShowScrollBar(_inner.Handle, SB_VERT, false);
        _inner.MouseWheel += (_, e) =>
        {
            if (!_bar.Visible) return;
            _inner.TopIndex = Math.Max(0, _inner.TopIndex + (e.Delta < 0 ? 3 : -3));
            if (e is HandledMouseEventArgs h) h.Handled = true;
            SyncBar();
        };
        _inner.SelectedIndexChanged += (_, _) => SyncBar();
        _inner.Resize += (_, _) => SyncBar();
        _bar.Scrolled += (_, _) =>
        {
            if (_syncing || !_inner.IsHandleCreated) return;
            _syncing = true;
            try { _inner.TopIndex = _bar.Value; }
            finally { _syncing = false; }
        };
        _sync.Tick += (_, _) => SyncBar();
        _sync.Start();
    }

    public void SyncBar()
    {
        if (_syncing || !_inner.IsHandleCreated) return;
        _syncing = true;
        try
        {
            int count = SendMessage(_inner.Handle, LB_GETCOUNT, 0, 0);
            int top = SendMessage(_inner.Handle, LB_GETTOPINDEX, 0, 0);
            int visible = Math.Max(1, _inner.ClientSize.Height / Math.Max(1, _inner.ItemHeight));
            bool need = count > visible;
            _bar.Visible = need;
            if (need)
            {
                _bar.Maximum = count;
                _bar.LargeChange = visible;
                _bar.Value = top;
            }
        }
        finally { _syncing = false; }
    }

    public void ApplyTheme(bool dark, Color back, Color card, Color fore)
    {
        BackColor = back;
        _inner.BackColor = card;
        _inner.ForeColor = fore;
        _bar.ApplyTheme(dark, back);
        SyncBar();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _sync.Dispose();
        base.Dispose(disposing);
    }
}
