namespace CursorRotator.UI;

/// <summary>
/// Contenedor con scroll propio (sin barras nativas): posiciona el contenido
/// manualmente y muestra un <see cref="DarkScrollBar"/> dibujado con GDI+.
/// Ideal para secciones que no deben recortar controles al redimensionar.
/// </summary>
public sealed class DarkScrollPanel : Panel
{
    private readonly DarkScrollBar _bar = new() { Dock = DockStyle.Right, Visible = false };
    private Control _content;
    private bool _updating;

    public DarkScrollBar Bar => _bar;
    public Control Content => _content;

    public DarkScrollPanel()
    {
        AutoScroll = false;
        BorderStyle = BorderStyle.None;
        Controls.Add(_bar);
        _bar.Scrolled += (_, _) => PositionContent();
    }

    public void SetContent(Control content)
    {
        if (_content is not null)
        {
            _content.SizeChanged -= OnContentChanged;
            Controls.Remove(_content);
        }
        _content = content;
        content.Dock = DockStyle.None;
        content.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(content);
        _bar.BringToFront();
        content.SizeChanged += OnContentChanged;
        AttachWheel(content);
        UpdateScroll();
    }

    private void OnContentChanged(object sender, EventArgs e) => UpdateScroll();

    private void AttachWheel(Control root)
    {
        // La rueda sobre el contenido desplaza el panel (excepto en controles
        // que ya la usan: sliders, combos, numéricos y listas).
        if (root is not (TrackBar or ComboBox or NumericUpDown or ListBox))
        {
            root.MouseWheel += (_, e) =>
            {
                if (!_bar.Visible) return;
                if (e is HandledMouseEventArgs h)
                {
                    _bar.Value += e.Delta < 0 ? 60 : -60;
                    h.Handled = true;
                }
            };
        }
        foreach (Control child in root.Controls)
            AttachWheel(child);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (_bar.Visible && e is HandledMouseEventArgs h)
        {
            _bar.Value += e.Delta < 0 ? 60 : -60;
            h.Handled = true;
        }
        else base.OnMouseWheel(e);
    }

    private const int WM_MOUSEWHEEL = 0x020A;

    /// <summary>
    /// Intercepción a nivel de mensajes: la rueda sobre el panel (o sus hijos
    /// que no la consuman) desplaza el contenido sin barras nativas.
    /// </summary>
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEWHEEL && _bar.Visible && _content is not null)
        {
            int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
            if (delta != 0)
            {
                _bar.Value += delta < 0 ? 60 : -60;
                m.Result = IntPtr.Zero;
                return;
            }
        }
        base.WndProc(ref m);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateScroll();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateScroll();
    }

    public void ScrollToTop()
    {
        _bar.Value = 0;
        PositionContent();
    }

    public void UpdateScroll()
    {
        if (_content is null || _updating || !IsHandleCreated && !DesignMode) return;
        _updating = true;
        try
        {
            _content.PerformLayout();
            int contentH = Math.Max(_content.Height, _content.PreferredSize.Height);
            bool need = contentH > ClientSize.Height && ClientSize.Height > 0;
            _bar.Visible = need;
            _content.Width = Math.Max(0, ClientSize.Width - (need ? _bar.Width : 0));
            if (need)
            {
                _bar.Maximum = contentH;
                _bar.LargeChange = ClientSize.Height;
                PositionContent();
            }
            else
            {
                _bar.Value = 0;
                _content.Location = new Point(0, 0);
            }
        }
        finally { _updating = false; }
    }

    private void PositionContent()
    {
        if (_content is null) return;
        int contentH = Math.Max(_content.Height, 1);
        int maxOff = Math.Max(0, contentH - ClientSize.Height);
        _content.Location = new Point(0, -Math.Min(_bar.Value, maxOff));
    }

    public void ApplyTheme(bool dark, Color back)
    {
        BackColor = back;
        _bar.ApplyTheme(dark, back);
    }
}
