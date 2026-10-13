using CursorRotator.Core;

namespace CursorRotator.UI;

/// <summary>
/// Selector de esquema con tarjetas (misma galería que el Gestor, pero sin
/// activar/desactivar: clic en la tarjeta = elegir y cerrar). Incluye la
/// tarjeta "🎲 Aleatorio" (devuelve "").
/// </summary>
public sealed class SchemePickerDialog : Form
{
    private readonly Color _card, _fg, _muted;
    private readonly Color _hover;
    private TextBox _txtSearch;
    private FlowLayoutPanel _flow;
    private readonly List<string> _schemes;
    private readonly string _current;
    private readonly AppSettings _settings;
    private readonly ToolTip _tip = new();

    /// <summary>Esquema elegido ("" = aleatorio). Null si canceló.</summary>
    public string SelectedScheme { get; private set; }

    private bool _picked;

    public SchemePickerDialog(List<string> schemes, string current, AppSettings settings, bool dark)
    {
        _schemes = schemes;
        _current = current ?? "";
        _settings = settings;
        Theme.Palettes(dark, out var bg, out _card, out _fg, out _muted);
        _hover = dark ? Color.FromArgb(0x36, 0x36, 0x52) : Color.FromArgb(0xE6, 0xE0, 0xFF);

        Text = "Elegir cursor";
        Size = new Size(640, 560);
        MinimumSize = new Size(480, 400);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.Sizable;
        BackColor = bg;
        ForeColor = _fg;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12), BackColor = bg };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var lbl = new Label
        {
            Text = "Clic en la tarjeta para elegir (la primera es aleatorio):",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 6),
            BackColor = bg,
            ForeColor = _fg,
        };
        root.Controls.Add(lbl, 0, 0);

        _txtSearch = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6), BackColor = _card, ForeColor = _fg, BorderStyle = BorderStyle.FixedSingle };
        _txtSearch.TextChanged += (_, _) => BuildCards();
        root.Controls.Add(_txtSearch, 0, 1);

        _flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, BackColor = bg };
        root.Controls.Add(_flow, 0, 2);

        var btnCancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(140, 34), FlatStyle = FlatStyle.Flat, BackColor = _card, ForeColor = _fg };
        btnCancel.FlatAppearance.BorderColor = Theme.Accent;
        var chkAnim = new CheckBox { Text = "✨ Animar", AutoSize = true, Checked = _settings.CardAnimation, BackColor = bg, Anchor = AnchorStyles.Left };
        chkAnim.CheckedChanged += (_, _) =>
        {
            _settings.CardAnimation = chkAnim.Checked;
            _settings.Save();
            FlowFramePanel.MotionEnabled = chkAnim.Checked;
            try
            {
                foreach (Control c in _flow.Controls) c.Invalidate(true);
            }
            catch { }
        };
        Theme.WireCheckState(chkAnim, dark);
        _tip.SetToolTip(chkAnim, "Activa o apaga la órbita del degradado en los marcos");
        var bottomRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, BackColor = bg };
        bottomRow.Controls.Add(chkAnim);
        bottomRow.Controls.Add(btnCancel);
        root.Controls.Add(bottomRow, 0, 3);
        CancelButton = btnCancel;

        FlowFramePanel.MotionEnabled = _settings.CardAnimation;
        BuildCards();
    }

    private IEnumerable<string> VisibleNames()
    {
        string q = (_txtSearch?.Text ?? "").Trim().ToLowerInvariant();
        return _schemes.Where(s => q.Length == 0 || s.ToLowerInvariant().Contains(q));
    }

    private void BuildCards()
    {
        if (_flow is null) return;
        Point scroll = new(Math.Abs(_flow.AutoScrollPosition.X), Math.Abs(_flow.AutoScrollPosition.Y));
        foreach (Control c in _flow.Controls)
            DisposeCard(c);
        _flow.Controls.Clear();
        _flow.SuspendLayout();
        try
        {
            _flow.Controls.Add(MakeRandomCard());
            foreach (var name in VisibleNames())
                _flow.Controls.Add(MakeCard(name));
        }
        finally { _flow.ResumeLayout(); }
        try { _flow.AutoScrollPosition = scroll; } catch { }
    }

    private static void DisposeCard(Control card)
    {
        try
        {
            DisposeImages(card);
            card.Dispose();
        }
        catch { }
    }

    private static void DisposeImages(Control c)
    {
        try
        {
            if (c is PictureBox pb)
                try { pb.Image?.Dispose(); } catch { }
            foreach (Control child in c.Controls)
                DisposeImages(child);
        }
        catch { }
    }

    /// <summary>Tarjeta especial "🎲 Aleatorio" (sin archivo de cursor).</summary>
    private Panel MakeRandomCard() =>
        MakeCardBase("", "🎲 Aleatorio", isCurrent: string.IsNullOrEmpty(_current), dice: true);

    private Panel MakeCard(string name)
    {
        Bitmap bmp = null;
        try
        {
            var paths = SchemeStore.GetResolvedSchemePaths(name);
            if (paths.Length > 0)
                bmp = CursorPreviewCache.Get(paths[0]);
        }
        catch { }
        var card = MakeCardBase(name, name,
            isCurrent: string.Equals(name, _current, StringComparison.Ordinal), dice: false);
        var inner = card.Controls.Count > 0 ? card.Controls[0] : card;
        foreach (Control c in inner.Controls)
            if (c is PictureBox pb) pb.Image = bmp;
        // Marco animado con la paleta del cursor (siempre activo aquí).
        if (bmp is not null)
            card.SetPalette(CursorPalette.EnsureTwo(CursorPalette.PaletteOf(bmp)));
        else
            card.SetPalette(CursorPalette.DefaultPalette());
        card.Animated = true;
        return card;
    }

    private FlowFramePanel MakeCardBase(string value, string caption, bool isCurrent, bool dice)
    {
        var frame = new FlowFramePanel
        {
            Size = new Size(150, 128),
            Margin = new Padding(6),
            Cursor = Cursors.Hand,
            Tag = value,
        };
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _card,
            Cursor = Cursors.Hand,
            Tag = value,
        };
        frame.Controls.Add(card);
        // Aleatorio: paleta de acento de la app.
        frame.SetPalette(CursorPalette.DefaultPalette());
        frame.Animated = true;
        Control icon;
        if (dice)
        {
            icon = new Label
            {
                Text = "🎲",
                Size = new Size(48, 48),
                Location = new Point(48, 6),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 26f),
                BackColor = _card,
                ForeColor = _fg,
                Cursor = Cursors.Hand,
            };
        }
        else
        {
            icon = new PictureBox
            {
                Size = new Size(48, 48),
                Location = new Point(48, 6),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = _card,
                Cursor = Cursors.Hand,
            };
        }
        var dot = new Label
        {
            Text = "●",
            AutoSize = true,
            Location = new Point(122, 2),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0x2E, 0xCC, 0x71),
            BackColor = _card,
            Visible = isCurrent,
            Cursor = Cursors.Hand,
        };
        var lbl = new Label
        {
            Text = caption,
            Location = new Point(3, 58),
            Size = new Size(138, 56),
            TextAlign = ContentAlignment.TopCenter,
            Font = new Font("Segoe UI", 8f),
            ForeColor = _fg,
            BackColor = _card,
            AutoEllipsis = true,
            Cursor = Cursors.Hand,
        };
        card.Controls.AddRange([icon, dot, lbl]);

        void Pick(object s, EventArgs e)
        {
            SelectedScheme = value;
            _picked = true;
            DialogResult = DialogResult.OK;
            Close();
        }
        frame.Click += Pick;
        card.Click += Pick;
        icon.Click += Pick;
        lbl.Click += Pick;
        dot.Click += Pick;

        void PaintHover(Color c)
        {
            card.BackColor = c;
            foreach (Control child in card.Controls) child.BackColor = c;
        }
        void HoverOn(object s, EventArgs e) => PaintHover(_hover);
        void HoverOff(object s, EventArgs e) => PaintHover(_card);
        frame.MouseEnter += HoverOn;
        frame.MouseLeave += HoverOff;
        card.MouseEnter += HoverOn;
        card.MouseLeave += HoverOff;
        icon.MouseEnter += HoverOn;
        icon.MouseLeave += HoverOff;
        lbl.MouseEnter += HoverOn;
        lbl.MouseLeave += HoverOff;

        _tip.SetToolTip(frame, dice ? "Cambio aleatorio de la cola" : caption);
        _tip.SetToolTip(card, dice ? "Cambio aleatorio de la cola" : caption);
        return frame;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult != DialogResult.OK || !_picked) SelectedScheme = null;
        base.OnFormClosing(e);
    }
}
