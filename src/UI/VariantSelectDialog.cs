using System.ComponentModel;
using CursorRotator.Core;

namespace CursorRotator.UI;

/// <summary>
/// Examen del paquete: muestra TODOS los cursores encontrados en los
/// .zip/.rar/etc. o carpetas (con .inf o sueltos) para instalar todos o solo
/// algunos. Incluye buscador, filtro de "solo nuevos", vista previa grande y
/// una tira con todos los roles del esquema. Todos marcados por defecto; los
/// ya instalados se señalan con claridad.
/// </summary>
public sealed class VariantSelectDialog : Form
{
    private readonly CheckedListBox _list = new()
    {
        Dock = DockStyle.Fill,
        CheckOnClick = true,
        IntegralHeight = false,
    };
    private readonly ListBox _files = new()
    {
        Dock = DockStyle.Fill,
        ScrollAlwaysVisible = true,
        IntegralHeight = false,
    };
    private readonly Label _detail = new()
    {
        Dock = DockStyle.Fill,
        AutoSize = true,
    };
    private readonly PictureBox _preview = new()
    {
        Size = new Size(72, 72),
        SizeMode = PictureBoxSizeMode.Zoom,
        Anchor = AnchorStyles.Top | AnchorStyles.Left,
    };
    private readonly FlowLayoutPanel _strip = new()
    {
        AutoSize = true,
        WrapContents = true,
        MaximumSize = new Size(164, 0),
    };
    private readonly TextBox _txtSearch = new()
    {
        Width = 220,
        PlaceholderText = "Buscar por nombre o grupo...",
    };
    private readonly CheckBox _chkOnlyNew = new()
    {
        Text = "Solo nuevos (ocultar ya instalados)",
        AutoSize = true,
        Anchor = AnchorStyles.Left,
    };
    private readonly Label _lblCount = new()
    {
        AutoSize = true,
        Anchor = AnchorStyles.Left,
    };
    private readonly ToolTip _tip = new() { InitialDelay = 400, AutoPopDelay = 6000 };

    /// <summary>Candidatos completos (índice estable; el filtro solo oculta).</summary>
    private readonly List<CursorInstaller.InstallCandidate> _all;
    /// <summary>Estado marcado por índice de <see cref="_all"/> (todos true).</summary>
    private readonly bool[] _checkedAll;
    /// <summary>Índices de <see cref="_all"/> visibles con el filtro actual.</summary>
    private readonly List<int> _visible = new();
    private bool _suspend;

    /// <summary>Null si el usuario canceló.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<CursorInstaller.InstallCandidate> Selected { get; private set; }

    public VariantSelectDialog(List<CursorInstaller.InstallCandidate> candidates, bool dark)
    {
        _all = candidates;
        _checkedAll = Enumerable.Repeat(true, candidates.Count).ToArray();
        int loose = candidates.Count(c => c.IsLoose);
        int withInf = candidates.Count - loose;
        Text = $"Examinar paquete ({candidates.Count} cursores: {withInf} con instalador, {loose} sueltos)";
        Size = new Size(700, 620);
        MinimumSize = new Size(560, 460);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // instrucción
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // filtro
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));   // lista
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // detalle
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));   // archivos+preview
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // botones
        Controls.Add(root);

        var lbl = new Label
        {
            Text = "Se encontraron estos cursores. Marca los que quieras instalar (todos por defecto). " +
                   "Usa el buscador para filtrar.",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 6),
        };
        root.Controls.Add(lbl, 0, 0);

        var filterBar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
        filterBar.Controls.Add(_txtSearch);
        filterBar.Controls.Add(_chkOnlyNew);
        filterBar.Controls.Add(_lblCount);
        root.Controls.Add(filterBar, 0, 1);
        root.Controls.Add(_list, 0, 2);

        _txtSearch.TextChanged += (_, _) => Rebuild();
        _chkOnlyNew.CheckedChanged += (_, _) => Rebuild();
        _list.SelectedIndexChanged += (_, _) => RefreshDetail();
        _list.ItemCheck += (_, e) =>
        {
            if (_suspend) return;
            if (e.Index >= 0 && e.Index < _visible.Count)
                _checkedAll[_visible[e.Index]] = e.NewValue == CheckState.Checked;
            UpdateCount();
        };
        root.Controls.Add(_detail, 0, 3);

        // Zona inferior: lista de archivos + vista previa (grande + tira de roles).
        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(_files, 0, 0);
        var previewCol = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Margin = new Padding(6, 0, 0, 0) };
        previewCol.Controls.Add(_preview, 0, 0);
        previewCol.Controls.Add(_strip, 0, 1);
        bottom.Controls.Add(previewCol, 1, 0);
        root.Controls.Add(bottom, 0, 4);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        var btnOk = new Button { Text = "Instalar seleccionados", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(170, 32) };
        var btnAll = new Button { Text = "Todos", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(90, 32) };
        btnAll.Click += (_, _) => SetAllVisible(true);
        var btnNone = new Button { Text = "Ninguno", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(90, 32) };
        btnNone.Click += (_, _) => SetAllVisible(false);
        var btnCancel = new Button { Text = "Cancelar", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(100, 32), DialogResult = DialogResult.Cancel };
        btnOk.Click += (_, _) => DialogResult = DialogResult.OK;
        buttons.Controls.AddRange([btnOk, btnAll, btnNone, btnCancel]);
        root.Controls.Add(buttons, 0, 5);

        _tip.SetToolTip(_txtSearch, "Filtra la lista por nombre de esquema o grupo del paquete.");
        _tip.SetToolTip(_chkOnlyNew, "Oculta los esquemas que ya están instalados.");
        _tip.SetToolTip(btnAll, "Marca todos los cursores visibles con el filtro actual.");
        _tip.SetToolTip(btnNone, "Desmarca todos los cursores visibles con el filtro actual.");
        _tip.SetToolTip(_preview, "Vista previa del primer archivo del esquema seleccionado.");
        _tip.SetToolTip(_strip, "Todos los roles del esquema seleccionado.");

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        FormClosing += (_, e) =>
        {
            if (DialogResult != DialogResult.OK) { Selected = null; return; }
            var picked = new List<CursorInstaller.InstallCandidate>();
            for (int i = 0; i < _all.Count; i++)
                if (_checkedAll[i]) picked.Add(_all[i]);
            if (picked.Count == 0)
            {
                MessageBox.Show("No hay nada marcado.", "Examen del paquete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                e.Cancel = true;
                return;
            }
            Selected = picked;
        };

        Rebuild();
        Theme.Apply(this, dark);
    }

    /// <summary>Reconstruye la lista visible según el buscador y el filtro.</summary>
    private void Rebuild()
    {
        _suspend = true;
        _list.BeginUpdate();
        _list.Items.Clear();
        _visible.Clear();
        string q = _txtSearch.Text.Trim();
        bool onlyNew = _chkOnlyNew.Checked;
        for (int i = 0; i < _all.Count; i++)
        {
            var c = _all[i];
            if (onlyNew && c.AlreadyInstalled) continue;
            if (q.Length > 0 && !Matches(c, q)) continue;
            _visible.Add(i);
            _list.Items.Add(LabelOf(c), _checkedAll[i]);
        }
        _list.EndUpdate();
        _suspend = false;
        UpdateCount();
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        else RefreshDetail();
    }

    private static bool Matches(CursorInstaller.InstallCandidate c, string q) =>
        c.Scheme.SchemeName.Contains(q, StringComparison.OrdinalIgnoreCase)
        || (c.Group ?? "").Contains(q, StringComparison.OrdinalIgnoreCase);

    private static string LabelOf(CursorInstaller.InstallCandidate c)
    {
        string kind = c.IsLoose ? "[sueltos]" : "[instalador]";
        return $"{kind} {c.Group} → {c.Scheme.SchemeName} " +
               $"({c.Scheme.SourceFiles.Count} archivos)" +
               (c.AlreadyInstalled ? "  ⛔ YA INSTALADO" : "");
    }

    private void UpdateCount()
    {
        int checkedCount = _checkedAll.Count(x => x);
        _lblCount.Text = $"  {checkedCount} de {_all.Count} marcados · " +
                         $"{_visible.Count} visibles";
    }

    private void RefreshDetail()
    {
        try
        {
            int idx = _list.SelectedIndex;
            if (idx < 0 || idx >= _visible.Count)
            {
                ClearDetail();
                return;
            }
            var c = _all[_visible[idx]];
            string origin = c.IsLoose ? $"carpeta: {c.InfPath}" : $"instalador: {Path.GetFileName(c.InfPath)}";
            _detail.Text = $"{c.Scheme.SchemeName} — {c.Scheme.SourceFiles.Count} archivos ({origin})";
            _detail.ForeColor = c.AlreadyInstalled
                ? Color.FromArgb(0xE6, 0x7E, 0x22)
                : ForeColor;
            if (c.AlreadyInstalled)
                _detail.Text += "  ⛔ Ya instalado: se preguntará antes de sobrescribir.";

            _files.BeginUpdate();
            _files.Items.Clear();
            foreach (var f in c.Scheme.SourceFiles.Select(Path.GetFileName).OrderBy(n => n))
                _files.Items.Add(f);
            _files.EndUpdate();

            // Vista previa grande (primer archivo).
            var oldBig = _preview.Image;
            _preview.Image = null;
            oldBig?.Dispose();
            string first = c.Scheme.SourceFiles.FirstOrDefault();
            if (!string.IsNullOrEmpty(first))
            {
                var bmp = CursorPreviewCache.Get(first);
                if (bmp is not null) _preview.Image = bmp;
            }

            // Tira: un preview por rol (máx. 10) para ver el esquema completo.
            ClearStrip();
            foreach (var f in c.Scheme.SourceFiles.Take(10))
            {
                var pic = new PictureBox
                {
                    Size = new Size(32, 32),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Margin = new Padding(0, 0, 2, 2),
                };
                try { pic.Image = CursorPreviewCache.Get(f); } catch { }
                _tip.SetToolTip(pic, Path.GetFileNameWithoutExtension(f));
                _strip.Controls.Add(pic);
            }
        }
        catch { }
    }

    private void ClearDetail()
    {
        _detail.Text = "";
        _detail.ForeColor = ForeColor;
        _files.Items.Clear();
        var old = _preview.Image;
        _preview.Image = null;
        old?.Dispose();
        ClearStrip();
    }

    private void ClearStrip()
    {
        foreach (Control c in _strip.Controls)
        {
            if (c is PictureBox pb) { var img = pb.Image; pb.Image = null; img?.Dispose(); }
        }
        _strip.Controls.Clear();
    }

    private void SetAllVisible(bool check)
    {
        for (int i = 0; i < _visible.Count; i++)
        {
            _checkedAll[_visible[i]] = check;
            _list.SetItemChecked(i, check);
        }
        UpdateCount();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _preview.Image?.Dispose(); } catch { }
            ClearStrip();
            _list?.Dispose();
            _files?.Dispose();
            _detail?.Dispose();
            _preview?.Dispose();
            _strip?.Dispose();
            _txtSearch?.Dispose();
            _chkOnlyNew?.Dispose();
            _lblCount?.Dispose();
            _tip?.Dispose();
        }
        base.Dispose(disposing);
    }
}
