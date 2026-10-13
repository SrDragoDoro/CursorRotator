using CursorRotator.Core.Trails;

namespace CursorRotator.UI;

/// <summary>Galería de efectos de clic: 3 categorías + búsqueda universal.</summary>
public sealed class ClickGalleryDialog : Form
{
    private TabControl _tabs;
    private TextBox _txtSearch;
    private Panel _searchPanel;
    private FlowLayoutPanel _results;
    private int _selected;
    private readonly bool _dark;
    private readonly ToolTip _tip = new();

    public int Selected => _selected;

    private static readonly (int Index, string Tab)[] Cats =
    [
        (0, "Ondas"), (4, "Ondas"), (10, "Ondas"), (7, "Ondas"),
        (19, "Ondas"), (14, "Ondas"), (16, "Ondas"), (18, "Ondas"),
        (1, "Partículas"), (2, "Partículas"), (11, "Partículas"), (8, "Partículas"),
        (15, "Partículas"), (17, "Partículas"), (5, "Partículas"),
        (3, "Sólidos"), (13, "Sólidos"), (6, "Sólidos"), (9, "Sólidos"), (12, "Sólidos"),
    ];

    private static readonly string[] Tabs = ["Ondas", "Partículas", "Sólidos"];

    public ClickGalleryDialog(int current, bool dark)
    {
        _selected = Math.Clamp(current, 0, ClickEffectFactory.Names.Length - 1);
        _dark = dark;
        Text = "Efecto al Hacer Clic";
        Size = new Size(560, 440);
        MinimumSize = new Size(480, 380);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // búsqueda
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // pestañas
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // cerrar
        Controls.Add(root);

        _txtSearch = new TextBox { BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 6, 0) };
        _txtSearch.TextChanged += (_, _) => ApplyFilter();
        _txtSearch.KeyDown += SearchKeyDown;
        _tip.SetToolTip(_txtSearch, "Filtrar efectos en todas las pestañas");
        var magnifier = new Label { Text = "🔍", AutoSize = false, Width = 30, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleCenter };
        _searchPanel = new Panel { Dock = DockStyle.Fill, Height = 32, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 0, 0, 6) };
        _searchPanel.Controls.Add(_txtSearch);
        _searchPanel.Controls.Add(magnifier);
        root.Controls.Add(_searchPanel, 0, 0);
        magnifier.ForeColor = ForeColor;

        _tabs = new TabControl { Dock = DockStyle.Fill };
        foreach (string tab in Tabs)
        {
            var page = new TabPage(tab);
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                Padding = new Padding(8),
            };
            foreach (var (index, t) in Cats)
            {
                if (t != tab) continue;
                int idx = index;
                var btn = new Button
                {
                    Text = ClickEffectFactory.Names[idx],
                    AutoSize = true,
                    MinimumSize = new Size(200, 40),
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(4),
                    Tag = idx,
                };
                _tip.SetToolTip(btn, "Efecto " + ClickEffectFactory.Names[idx]);
                btn.Click += (_, _) =>
                {
                    _selected = idx;
                    DialogResult = DialogResult.OK;
                    Close();
                };
                if (idx == _selected)
                {
                    btn.Font = new Font(Font, FontStyle.Bold);
                    btn.FlatAppearance.BorderSize = 2;
                }
                flow.Controls.Add(btn);
            }
            page.Controls.Add(flow);
            _tabs.TabPages.Add(page);
        }
        foreach (TabPage page in _tabs.TabPages)
        {
            foreach (Control c in page.Controls[0].Controls)
            {
                if (c is Button b && b.Tag is int p && p == _selected)
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
        _searchPanel.BackColor = BackColor;
        _txtSearch.BackColor = BackColor;
        _txtSearch.ForeColor = ForeColor;
        magnifier.ForeColor = ForeColor;
        foreach (TabPage page in _tabs.TabPages)
            foreach (Control c in page.Controls[0].Controls)
                if (c is Button b && b.Tag is int p && p == _selected)
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
        for (int i = 0; i < ClickEffectFactory.Names.Length; i++)
        {
            string label = ClickEffectFactory.Names[i];
            if (!label.ToLowerInvariant().Contains(q)) continue;
            int idx = i;
            var btn = new Button
            {
                Text = label,
                AutoSize = true,
                MinimumSize = new Size(200, 40),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(4),
            };
            _tip.SetToolTip(btn, "Efecto " + label);
            btn.Click += (_, _) =>
            {
                _selected = idx;
                DialogResult = DialogResult.OK;
                Close();
            };
            btn.BackColor = _dark ? Color.FromArgb(0x2A, 0x2A, 0x3D) : Color.White;
            btn.ForeColor = _dark ? Color.White : Color.Black;
            btn.FlatAppearance.BorderColor = Theme.Accent;
            if (idx == _selected)
            {
                btn.Font = new Font(Font, FontStyle.Bold);
                btn.FlatAppearance.BorderSize = 2;
            }
            _results.Controls.Add(btn);
        }
    }
}
