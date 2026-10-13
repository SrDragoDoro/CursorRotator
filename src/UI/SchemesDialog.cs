using System.Drawing.Imaging;
using CursorRotator.Core;

namespace CursorRotator.UI;

/// <summary>
/// Gestor de esquemas como GALERÍA de tarjetas (estilo Custom Cursor):
/// icono grande como diferenciador + nombre. Brillo normal = rota;
/// oscurecida = fuera de la rotación. Clic en la tarjeta alterna la
/// rotación (los botones no alternan: disparan su acción). Cada tarjeta
/// trae 🔍 (roles) y ✔ (aplicar). Eliminar va en menú contextual.
/// </summary>
public sealed class SchemesDialog : Form
{
    private readonly string _storeDir;
    private readonly AppSettings _settings;
    private readonly bool _dark;
    private readonly Color _bg, _card, _fg, _muted;
    private readonly List<string> _all = new();

    private TextBox _txtSearch;
    private Label _lblApplied;
    private FlowLayoutPanel _flow;
    private ContextMenuStrip _batchMenu;
    private ContextMenuStrip _cardMenu;
    private readonly ToolTip _tip = new();

    public event Action<string> Applied;
    public event Action RotationPoolChanged;

    public SchemesDialog(string storeDir, AppSettings settings, bool dark)
    {
        _storeDir = storeDir;
        _settings = settings;
        _dark = dark;
        Theme.Palettes(dark, out _bg, out _card, out _fg, out _muted);

        Text = "Gestor de Esquemas";
        Size = new Size(640, 600);
        MinimumSize = new Size(480, 440);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.Sizable;
        BackColor = _bg;
        ForeColor = _fg;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12), BackColor = _bg };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // hint
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // aplicado
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // búsqueda
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // tarjetas
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // botones
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // cerrar
        Controls.Add(root);

        var lblHint = new Label
        {
            Text = "Clic en la tarjeta: entra/sale de la rotación · 🔍 ver roles · ✔ aplicar",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 4),
            BackColor = _bg,
            ForeColor = _fg,
        };
        root.Controls.Add(lblHint, 0, 0);

        _lblApplied = new Label
        {
            Text = "Aplicado ahora: —",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9f, FontStyle.Italic),
            Padding = new Padding(0, 0, 0, 4),
            BackColor = _bg,
            ForeColor = _muted,
        };
        root.Controls.Add(_lblApplied, 0, 1);

        _txtSearch = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6), BackColor = _card, ForeColor = _fg, BorderStyle = BorderStyle.FixedSingle };
        _txtSearch.TextChanged += (_, _) => ApplyFilter();
        _tip.SetToolTip(_txtSearch, "Filtrar esquemas por nombre");
        root.Controls.Add(_txtSearch, 0, 2);

        _flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = true,
            BackColor = _bg,
        };
        root.Controls.Add(_flow, 0, 3);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, BackColor = _bg };
        var btnRefresh = new Button { Text = "↻ Actualizar", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(110, 32) };
        btnRefresh.Click += (_, _) => RefreshList();
        var btnBatch = new Button { Text = "⚙️ Opciones de Rotación", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(180, 32) };
        btnBatch.Click += (s, e) => _batchMenu.Show((Control)s, new Point(0, ((Control)s).Height));
        var chkAnim = new CheckBox { Text = "✨ Animar", AutoSize = true, Checked = _settings.CardAnimation, Anchor = AnchorStyles.Left, BackColor = _bg };
        chkAnim.CheckedChanged += (_, _) => SetCardAnimation(chkAnim.Checked);
        foreach (var b in new[] { btnRefresh, btnBatch })
        {
            b.BackColor = _card;
            b.ForeColor = _fg;
            b.FlatAppearance.BorderColor = Theme.Accent;
        }
        Theme.WireCheckState(chkAnim, _dark);
        _tip.SetToolTip(btnBatch, "Marcar o desmarcar todos los visibles para la rotación");
        _tip.SetToolTip(chkAnim, "Activa o apaga la órbita del degradado en los marcos");
        buttons.Controls.AddRange([btnRefresh, btnBatch]);
        buttons.Controls.Add(chkAnim);
        root.Controls.Add(buttons, 0, 4);

        _batchMenu = new ContextMenuStrip();
        _batchMenu.Items.Add("Marcar todos", null, (_, _) => SetAllChecked(true));
        _batchMenu.Items.Add("Desmarcar todos", null, (_, _) => SetAllChecked(false));

        _cardMenu = new ContextMenuStrip();
        var miDelete = new ToolStripMenuItem("🗑 Eliminar esquema");
        miDelete.Click += (_, _) =>
        {
            if (_cardMenu.Tag is string name) DeleteScheme(name);
        };
        _cardMenu.Items.Add(miDelete);

        FlowFramePanel.MotionEnabled = _settings.CardAnimation;

        var btnClose = new Button { Text = "Cerrar", DialogResult = DialogResult.OK, Dock = DockStyle.Fill, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = _card, ForeColor = _fg };
        btnClose.FlatAppearance.BorderColor = Theme.Accent;
        root.Controls.Add(btnClose, 0, 5);

        RefreshList();
    }

    // ---------- Galería ----------

    private void RefreshList()
    {
        _all.Clear();
        _all.AddRange(SchemeStore.GetSchemeNames());
        SyncCards();
        RefreshAppliedLabel();
    }

    /// <summary>
    /// Sincroniza la galería con el registro SIN reconstruir: quita las que
    /// ya no existen, añade las nuevas, refresca las demás in-situ y conserva
    /// orden y scroll (el botón Actualizar ya no parpadea).
    /// </summary>
    private void SyncCards()
    {
        if (_flow is null) return;
        CursorPreviewCache.Clear(); // los archivos pueden haber cambiado en disco
        _flow.SuspendLayout();
        try
        {
            var wanted = new HashSet<string>(_all, StringComparer.OrdinalIgnoreCase);
            for (int i = _flow.Controls.Count - 1; i >= 0; i--)
            {
                var c = _flow.Controls[i];
                if (c.Tag is not string n || !wanted.Contains(n))
                {
                    _flow.Controls.RemoveAt(i);
                    DisposeCard(c);
                }
            }
            var existing = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
            foreach (Control c in _flow.Controls)
                if (c.Tag is string n && !existing.ContainsKey(n))
                    existing[n] = c;
            string applied = FindAppliedName();
            int idx = 0;
            foreach (var name in _all)
            {
                if (existing.TryGetValue(name, out var card))
                {
                    _flow.Controls.SetChildIndex(card, idx);
                    UpdateCardVisualCore(card, name,
                        !_settings.ExcludedFromRotation.Contains(name));
                }
                else
                {
                    var fresh = MakeCard(name, applied);
                    _flow.Controls.Add(fresh);
                    _flow.Controls.SetChildIndex(fresh, idx);
                }
                idx++;
            }
            UpdateDots(applied);
        }
        finally { _flow.ResumeLayout(); }
        ApplyFilter();
    }

    /// <summary>
    /// Filtro sin reconstruir: solo oculta/muestra tarjetas (sin parpadeo
    /// ni lag al escribir). La galería completa se construye una vez.
    /// </summary>
    private void ApplyFilter()
    {
        if (_flow is null) return;
        string q = (_txtSearch?.Text ?? "").Trim().ToLowerInvariant();
        _flow.SuspendLayout();
        try
        {
            foreach (Control card in _flow.Controls)
            {
                string name = card.Tag as string ?? "";
                card.Visible = q.Length == 0 || name.ToLowerInvariant().Contains(q);
            }
        }
        finally { _flow.ResumeLayout(); }
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

    /// <summary>Busca un descendiente por Name (marco → interior).</summary>
    private static Control FindDeep(Control root, string name)
    {
        try
        {
            var found = root.Controls.Find(name, true);
            if (found.Length > 0) return found[0];
        }
        catch { }
        return null;
    }

    private Panel MakeCard(string name, string appliedName)
    {
        bool inRotation = !_settings.ExcludedFromRotation.Contains(name);
        bool applied = string.Equals(name, appliedName, StringComparison.Ordinal);

        // Marco animado exterior + contenido interior (158×136).
        var frame = new FlowFramePanel
        {
            Size = new Size(164, 142),
            Margin = new Padding(6),
            Cursor = Cursors.Hand,
            Tag = name,
        };
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _card,
            Cursor = Cursors.Hand,
            Tag = name,
        };
        frame.Controls.Add(card);

        Bitmap bmp = null;
        try
        {
            var paths = SchemeStore.GetResolvedSchemePaths(name);
            if (paths.Length > 0)
                bmp = CursorPreviewCache.Get(paths[0]);
        }
        catch { }
        if (bmp is not null)
            frame.SetPalette(CursorPalette.EnsureTwo(CursorPalette.PaletteOf(bmp)));
        frame.Animated = inRotation && bmp is not null;
        if (!inRotation && bmp is not null)
        {
            // Fuera de rotación: icono oscurecido (efecto opacidad).
            var dim = DimBitmap(bmp, 0.38f);
            bmp.Dispose();
            bmp = dim;
        }

        var pic = new PictureBox
        {
            Name = "pic",
            Size = new Size(48, 48),
            Location = new Point(55, 6),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = bmp,
            BackColor = _card,
            Cursor = Cursors.Hand,
            Tag = name,
        };
        var dot = new Label
        {
            Text = "●",
            AutoSize = true,
            Location = new Point(136, 2),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0x2E, 0xCC, 0x71),
            BackColor = _card,
            Visible = applied,
            Cursor = Cursors.Hand,
            Tag = name,
        };
        var lbl = new Label
        {
            Name = "lbl",
            Text = name,
            Location = new Point(3, 58),
            Size = new Size(152, 36),
            TextAlign = ContentAlignment.TopCenter,
            Font = new Font("Segoe UI", 8f),
            ForeColor = inRotation ? _fg : _muted,
            BackColor = _card,
            AutoEllipsis = true,
            Cursor = Cursors.Hand,
            Tag = name,
        };
        var row = new FlowLayoutPanel
        {
            Location = new Point(3, 100),
            Size = new Size(152, 34),
            BackColor = _card,
            Margin = new Padding(0),
        };
        var btnRoles = new Button
        {
            Text = "🔍",
            Size = new Size(70, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = _card,
            ForeColor = _fg,
            Cursor = Cursors.Default,
            Tag = name,
        };
        btnRoles.FlatAppearance.BorderColor = Theme.Accent;
        var btnApply = new Button
        {
            Text = "✔",
            Size = new Size(70, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = _card,
            ForeColor = _fg,
            Cursor = Cursors.Default,
            Tag = name,
        };
        btnApply.FlatAppearance.BorderColor = Theme.Accent;
        btnRoles.Click += (_, _) => OpenRoles(name);
        btnApply.Click += (_, _) => ApplyScheme(name);
        row.Controls.AddRange([btnRoles, btnApply]);

        card.Controls.AddRange([pic, dot, lbl, row]);

        // Clic en tarjeta/icono/nombre = alternar rotación (los botones no).
        EventHandler toggle = (_, _) => ToggleCard(name);
        frame.Click += toggle;
        card.Click += toggle;
        pic.Click += toggle;
        lbl.Click += toggle;
        dot.Click += toggle;
        // Menú contextual (eliminar) con clic derecho en la tarjeta.
        void Menu(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                _cardMenu.Tag = name;
                var ctl = s as Control ?? card;
                _cardMenu.Show(ctl, e.Location);
            }
        }
        frame.MouseDown += Menu;
        card.MouseDown += Menu;
        pic.MouseDown += Menu;
        lbl.MouseDown += Menu;

        string verb = inRotation ? "sacar de" : "meter en";
        string tipText = $"{name}\nClic: {verb} la rotación · Clic derecho: eliminar";
        _tip.SetToolTip(frame, tipText);
        _tip.SetToolTip(card, tipText);
        _tip.SetToolTip(pic, tipText);
        _tip.SetToolTip(lbl, tipText);
        _tip.SetToolTip(btnRoles, $"Ver los 15 roles de '{name}'");
        _tip.SetToolTip(btnApply, $"Aplicar '{name}' ahora");
        return frame;
    }

    /// <summary>Oscurece un bitmap (simula opacidad) para tarjetas inactivas.</summary>
    private static Bitmap DimBitmap(Bitmap src, float f)
    {
        var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        using var attrs = new ImageAttributes();
        attrs.SetColorMatrix(new ColorMatrix(new float[][]
        {
            [f, 0, 0, 0, 0],
            [0, f, 0, 0, 0],
            [0, 0, f, 0, 0],
            [0, 0, 0, 1, 0],
            [0, 0, 0, 0, 1],
        }));
        g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height),
            0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
        return bmp;
    }

    /// <summary>Interruptor de la órbita: persiste y repinta los marcos.</summary>
    private void SetCardAnimation(bool on)
    {
        _settings.CardAnimation = on;
        _settings.Save();
        FlowFramePanel.MotionEnabled = on;
        try
        {
            foreach (Control card in _flow.Controls)
            {
                card.Invalidate(true);
                foreach (Control c in card.Controls) c.Invalidate(true);
            }
        }
        catch { }
        Toast.ShowToast(this, on ? "Animación de marcos activada" : "Animación de marcos desactivada", _dark);
    }

    // ---------- Acciones ----------

    /// <summary>
    /// Clic en la tarjeta: entra/sale de la rotación actualizando SOLO esa
    /// tarjeta (sin recargar la galería: sin parpadeo ni salto de scroll).
    /// </summary>
    private void ToggleCard(string name)
    {
        bool wasIn = !_settings.ExcludedFromRotation.Contains(name);
        if (wasIn)
            _settings.ExcludedFromRotation.Add(name);
        else
            _settings.ExcludedFromRotation.Remove(name);
        _settings.Save();
        RotationPoolChanged?.Invoke();
        UpdateCardVisual(name, !wasIn);
        Toast.ShowToast(this,
            $"Cursor \"{name}\" " + (!wasIn ? "activado" : "desactivado"), _dark);
    }

    private void UpdateCardVisual(string name, bool inRotation)
    {
        try
        {
            _flow.SuspendLayout();
            try
            {
                foreach (Control card in _flow.Controls)
                {
                    if (!string.Equals(card.Tag as string, name, StringComparison.Ordinal)) continue;
                    UpdateCardVisualCore(card, name, inRotation);
                }
            }
            finally { _flow.ResumeLayout(); }
        }
        catch { }
    }

    /// <summary>Refresca todas las tarjetas visibles sin reconstruir nada.</summary>
    private void RefreshVisibleCards()
    {
        try
        {
            _flow.SuspendLayout();
            try
            {
                foreach (Control card in _flow.Controls)
                {
                    string name = card.Tag as string;
                    if (name is null) continue;
                    UpdateCardVisualCore(card, name,
                        !_settings.ExcludedFromRotation.Contains(name));
                }
            }
            finally { _flow.ResumeLayout(); }
        }
        catch { }
    }

    private void UpdateCardVisualCore(Control frame, string name, bool inRotation)
    {
        // frame = marco exterior; el contenido vive en el interior.
        Bitmap full = null;
        try
        {
            var paths = SchemeStore.GetResolvedSchemePaths(name);
            if (paths.Length > 0)
                full = CursorPreviewCache.Get(paths[0]);
        }
        catch { }
        if (frame is FlowFramePanel ff)
        {
            // Paleta del icono a todo color (antes de oscurecer).
            if (full is not null)
                ff.SetPalette(CursorPalette.EnsureTwo(CursorPalette.PaletteOf(full)));
            // Inactivo = efecto apagado + marco gris.
            ff.Animated = inRotation && full is not null;
        }
        var pb = FindDeep(frame, "pic") as PictureBox;
        if (pb is not null)
        {
            var old = pb.Image;
            Bitmap shown = full;
            full = null; // la propiedad pasa al PictureBox
            if (!inRotation && shown is not null)
            {
                var dim = DimBitmap(shown, 0.38f);
                shown.Dispose();
                shown = dim;
            }
            pb.Image = shown;
            try { old?.Dispose(); } catch { }
            _tip.SetToolTip(pb,
                $"{name}\nClic: {(inRotation ? "sacar de" : "meter en")} la rotación");
        }
        try { full?.Dispose(); } catch { }
        var lbl = FindDeep(frame, "lbl");
        if (lbl is not null)
        {
            lbl.ForeColor = inRotation ? _fg : _muted;
            _tip.SetToolTip(lbl,
                $"{name}\nClic: {(inRotation ? "sacar de" : "meter en")} la rotación");
        }
        _tip.SetToolTip(frame,
            $"{name}\nClic: {(inRotation ? "sacar de" : "meter en")} la rotación · Clic derecho: eliminar");
    }

    private void ApplyScheme(string name)
    {
        try
        {
            SchemeStore.ApplyScheme(name);
            Applied?.Invoke(name);
            RefreshAppliedLabel();
            UpdateDots(name);
            Toast.ShowToast(this, $"Cursor \"{name}\" aplicado", _dark);
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudo aplicar: " + ex.Message, "Esquemas",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void UpdateDots(string appliedName)
    {
        try
        {
            foreach (Control card in _flow.Controls)
            {
                if (card.Tag as string is null) continue;
                bool on = string.Equals(card.Tag as string, appliedName, StringComparison.Ordinal);
                SetDotVisible(card, on);
            }
        }
        catch { }
    }

    private static void SetDotVisible(Control root, bool on)
    {
        try
        {
            foreach (Control c in root.Controls)
            {
                if (c is Label l && l.Text == "●") l.Visible = on;
                if (c.HasChildren) SetDotVisible(c, on);
            }
        }
        catch { }
    }

    private void OpenRoles(string name)
    {
        try
        {
            using var dlg = new SchemePreviewDialog(name, _dark);
            dlg.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudo abrir la vista: " + ex.Message,
                "Ver roles", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void DeleteScheme(string name)
    {
        if (MessageBox.Show($"¿Eliminar el esquema '{name}'?\nSe quitará del registro y sus archivos.",
                "Eliminar esquema", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        if (SchemeStore.DeleteScheme(name, _storeDir))
        {
            _settings.ExcludedFromRotation.Remove(name);
            _settings.Save();
            RefreshList();
            RotationPoolChanged?.Invoke();
            Toast.ShowToast(this, $"Cursor \"{name}\" eliminado", _dark);
        }
        else
            MessageBox.Show("No se pudo eliminar.", "Esquemas",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void SetAllChecked(bool check)
    {
        // Opera sobre las tarjetas visibles (respeta el filtro actual).
        var visible = new List<string>();
        foreach (Control card in _flow.Controls)
            if (card.Visible && card.Tag is string name)
                visible.Add(name);
        if (check)
            _settings.ExcludedFromRotation.RemoveAll(visible.Contains);
        else
            foreach (var s in visible)
                if (!_settings.ExcludedFromRotation.Contains(s))
                    _settings.ExcludedFromRotation.Add(s);
        _settings.Save();
        RefreshVisibleCards();
        RotationPoolChanged?.Invoke();
    }

    /// <summary>Insignia "Applied Cape" de Mousecape: qué esquema tiene Windows ahora.</summary>
    private void RefreshAppliedLabel()
    {
        string match = FindAppliedName();
        _lblApplied.Text = "Aplicado ahora: " + (match ?? "(personalizado de Windows)");
    }

    private string FindAppliedName()
    {
        try
        {
            string arrow = CursorColorReader.GetActiveArrowPath();
            if (string.IsNullOrEmpty(arrow)) return null;
            string norm = SchemeStore.ResolveCursorPath(arrow);
            foreach (var s in _all)
            {
                try
                {
                    var p = SchemeStore.GetResolvedSchemePaths(s);
                    if (p.Length > 0 && string.Equals(p[0], norm,
                        StringComparison.OrdinalIgnoreCase))
                        return s;
                }
                catch { }
            }
        }
        catch { }
        return null;
    }
}
