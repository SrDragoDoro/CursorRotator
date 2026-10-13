using CursorRotator.Core;
using CursorRotator.Core.Trails;

namespace CursorRotator.UI;

/// <summary>Galería visual de estelas: pestañas por categoría, un botón por estilo.</summary>
public sealed class TrailGalleryDialog : Form
{
    private TrailPreset _selected;
    private readonly bool _dark;
    private TabControl _tabs;
    private TextBox _txtSearch;
    private Panel _searchPanel;
    private FlowLayoutPanel _results;
    private readonly ToolTip _tip = new();

    public TrailPreset Selected => _selected;

    public TrailGalleryDialog(TrailPreset current, bool dark)
    {
        _selected = current;
        _dark = dark;
        Text = "Seleccionar Estela";
        Size = new Size(620, 480);
        MinimumSize = new Size(520, 400);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // búsqueda
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // pestañas/resultados
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // cerrar
        Controls.Add(root);

        // Barra de búsqueda moderna: lupa + textbox sin bordes.
        var searchPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 32,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, 6),
        };
        var magnifier = new Label { Text = "🔍", AutoSize = false, Width = 30, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleCenter };
        _txtSearch = new TextBox { BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 6, 0) };
        _txtSearch.TextChanged += (_, _) => ApplyFilter();
        _txtSearch.KeyDown += SearchKeyDown;
        _tip.SetToolTip(_txtSearch, "Filtrar estelas en todas las pestañas");
        searchPanel.Controls.Add(_txtSearch);
        searchPanel.Controls.Add(magnifier);
        root.Controls.Add(searchPanel, 0, 0);
        _searchPanel = searchPanel;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        _tabs = tabs;
        foreach (string tab in TrailCatalog.Tabs)
        {
            var page = new TabPage(tab);
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                Padding = new Padding(8),
            };
            foreach (var e in TrailCatalog.All)
            {
                if (e.Tab != tab) continue;
                var entry = e;
                var btn = new Button
                {
                    Text = entry.Label,
                    AutoSize = true,
                    MinimumSize = new Size(200, 40),
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(4),
                    Tag = entry.Preset,
                };
                _tip.SetToolTip(btn, entry.Tip);                btn.Click += (_, _) =>
                {
                    _selected = entry.Preset;
                    DialogResult = DialogResult.OK;
                    Close();
                };
                if (entry.Preset == current)
                {
                    btn.Font = new Font(Font, FontStyle.Bold);
                    btn.FlatAppearance.BorderSize = 2;
                }
                flow.Controls.Add(btn);
            }
            page.Controls.Add(flow);
            tabs.TabPages.Add(page);
        }
        // Abrir en la pestaña del estilo actual.
        foreach (TabPage page in _tabs.TabPages)
        {
            foreach (Control c in page.Controls[0].Controls)
            {
                if (c is Button b && b.Tag is TrailPreset p && p == current)
                {
                    _tabs.SelectedTab = page;
                    break;
                }
            }
        }
        root.Controls.Add(_tabs, 0, 1);

        // Panel plano de resultados: se superpone a las pestañas al buscar.
        _results = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = true,
            Padding = new Padding(8),
            Visible = false,
        };
        root.Controls.Add(_results, 0, 1);
        _results.BringToFront();

        var btnClose = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Dock = DockStyle.Fill, Height = 34, FlatStyle = FlatStyle.Flat };
        root.Controls.Add(btnClose, 0, 2);

        Theme.Apply(this, dark);
        // El buscador hereda el fondo del diálogo (textbox sin bordes).
        _searchPanel.BackColor = BackColor;
        _txtSearch.BackColor = BackColor;
        _txtSearch.ForeColor = ForeColor;
        magnifier.ForeColor = ForeColor;
        // Resaltar selección con acento (tras el tema).
        foreach (TabPage page in _tabs.TabPages)
            foreach (Control c in page.Controls[0].Controls)
                if (c is Button b && b.Tag is TrailPreset p && p == current)
                    b.FlatAppearance.BorderColor = Theme.Accent;
    }

    /// <summary>
    /// Foco estricto en el buscador: las teclas de navegación no deben mover
    /// pestañas ni focos (el filtro no toca Focus/Select de otros controles).
    /// </summary>
    private void SearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Control || e.Alt) return; // atajos (Ctrl+A/C/V/X) intactos
        switch (e.KeyCode)
        {
            case Keys.Left:
            case Keys.Right:
            case Keys.Up:
            case Keys.Down:
            case Keys.Tab:
            case Keys.Prior:
            case Keys.Next:
            case Keys.Home:
            case Keys.End:
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;
        }
    }

    /// <summary>
    /// Resultados planos: con texto se ocultan las pestañas y se clonan los
    /// botones coincidentes en una lista unificada (los originales intactos).
    /// </summary>
    private void ApplyFilter()
    {
        string q = (_txtSearch?.Text ?? "").Trim().ToLowerInvariant();
        if (q.Length == 0)
        {
            _results.Visible = false;
            _tabs.Visible = true;
            return;
        }
        _tabs.Visible = false;
        _results.Visible = true;
        _results.BringToFront();
        _results.Controls.Clear();
        foreach (var e in TrailCatalog.All)
        {
            if (!e.Label.ToLowerInvariant().Contains(q)) continue;
            var entry = e;
            var btn = new Button
            {
                Text = entry.Label,
                AutoSize = true,
                MinimumSize = new Size(200, 40),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(4),
            };
            _tip.SetToolTip(btn, entry.Tip);
            btn.Click += (_, _) =>
            {
                _selected = entry.Preset;
                DialogResult = DialogResult.OK;
                Close();
            };
            btn.BackColor = _dark ? Color.FromArgb(0x2A, 0x2A, 0x3D) : Color.White;
            btn.ForeColor = _dark ? Color.White : Color.Black;
            btn.FlatAppearance.BorderColor = Theme.Accent;
            if (entry.Preset == _selected)
            {
                btn.Font = new Font(Font, FontStyle.Bold);
                btn.FlatAppearance.BorderSize = 2;
            }
            _results.Controls.Add(btn);
        }
    }
}
