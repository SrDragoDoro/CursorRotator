using System.ComponentModel;
using CursorRotator.Core;

namespace CursorRotator.UI;

/// <summary>
/// Examen del paquete: muestra TODOS los cursores encontrados en el
/// .zip/.rar o carpeta (con .inf o sueltos) para instalar todos o solo
/// algunos. Todos marcados por defecto. Los ya instalados van marcados.
/// </summary>
public sealed class VariantSelectDialog : Form
{
    private readonly CheckedListBox _list = new()
    {
        Dock = DockStyle.Fill,
        CheckOnClick = true,
    };
    private readonly ListBox _files = new()
    {
        Dock = DockStyle.Fill,
        Height = 110,
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
        Size = new Size(48, 48),
        SizeMode = PictureBoxSizeMode.Zoom,
    };
    private readonly List<CursorInstaller.InstallCandidate> _candidates;

    /// <summary>Null si el usuario canceló.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<CursorInstaller.InstallCandidate> Selected { get; private set; }

    public VariantSelectDialog(List<CursorInstaller.InstallCandidate> candidates, bool dark)
    {
        _candidates = candidates;
        int loose = candidates.Count(c => c.IsLoose);
        int withInf = candidates.Count - loose;
        Text = $"Examinar paquete ({candidates.Count} cursores: {withInf} con instalador, {loose} sueltos)";
        Size = new Size(620, 560);
        MinimumSize = new Size(500, 420);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var lbl = new Label
        {
            Text = "Se encontraron estos cursores en el paquete. Marca los que quieras instalar (todos por defecto):",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 6),
        };
        root.Controls.Add(lbl, 0, 0);
        root.Controls.Add(_list, 0, 1);

        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            string kind = c.IsLoose ? "[sueltos]" : "[instalador]";
            _list.Items.Add(
                $"{kind} {c.Group} → {c.Scheme.SchemeName} " +
                $"({c.Scheme.SourceFiles.Count} archivos)" +
                (c.AlreadyInstalled ? "  ● instalado" : ""), true);
        }
        _list.SelectedIndexChanged += (_, _) => RefreshDetail();
        root.Controls.Add(_detail, 0, 2);

        var filesRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
        filesRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filesRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filesRow.Controls.Add(_files, 0, 0);
        filesRow.Controls.Add(_preview, 1, 0);
        root.Controls.Add(filesRow, 0, 3);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var btnOk = new Button { Text = "Instalar seleccionados", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(170, 32), DialogResult = DialogResult.OK };
        var btnAll = new Button { Text = "Todos", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(90, 32) };
        btnAll.Click += (_, _) => SetAll(true);
        var btnNone = new Button { Text = "Ninguno", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(90, 32) };
        btnNone.Click += (_, _) => SetAll(false);
        var btnCancel = new Button { Text = "Cancelar", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(100, 32), DialogResult = DialogResult.Cancel };
        buttons.Controls.AddRange([btnOk, btnAll, btnNone, btnCancel]);
        root.Controls.Add(buttons, 0, 4);

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        FormClosing += (_, e) =>
        {
            if (DialogResult != DialogResult.OK) { Selected = null; return; }
            var picked = new List<CursorInstaller.InstallCandidate>();
            for (int i = 0; i < _list.Items.Count; i++)
                if (_list.GetItemChecked(i)) picked.Add(_candidates[i]);
            if (picked.Count == 0)
            {
                MessageBox.Show("No hay nada marcado.", "Examen del paquete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                e.Cancel = true;
                return;
            }
            Selected = picked;
        };

        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        RefreshDetail();
        Theme.Apply(this, dark);
    }

    private void RefreshDetail()
    {
        try
        {
            int idx = _list.SelectedIndex;
            if (idx < 0 || idx >= _candidates.Count)
            {
                _detail.Text = "";
                _files.Items.Clear();
                return;
            }
            var c = _candidates[idx];
            string origin = c.IsLoose ? $"carpeta: {c.InfPath}" : $"instalador: {Path.GetFileName(c.InfPath)}";
            _detail.Text = $"{c.Scheme.SchemeName} — {c.Scheme.SourceFiles.Count} archivos ({origin})" +
                (c.AlreadyInstalled ? "  ● ya instalado" : "");
            _files.BeginUpdate();
            _files.Items.Clear();
            foreach (var f in c.Scheme.SourceFiles.Select(Path.GetFileName).OrderBy(n => n))
                _files.Items.Add(f);
            _files.EndUpdate();

            var old = _preview.Image;
            _preview.Image = null;
            old?.Dispose();
            try
            {
                string first = c.Scheme.SourceFiles.FirstOrDefault();
                if (!string.IsNullOrEmpty(first))
                {
                    var bmp = CursorPreviewCache.Get(first);
                    if (bmp is not null) _preview.Image = bmp;
                }
            }
            catch { }
        }
        catch { }
    }

    private void SetAll(bool check)
    {
        for (int i = 0; i < _list.Items.Count; i++)
            _list.SetItemChecked(i, check);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _preview.Image?.Dispose(); } catch { }
            _list?.Dispose();
            _files?.Dispose();
            _detail?.Dispose();
            _preview?.Dispose();
        }
        base.Dispose(disposing);
    }
}
