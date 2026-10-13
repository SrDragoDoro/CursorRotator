using CursorRotator.Core;

namespace CursorRotator.UI;

/// <summary>
/// Selector de variantes: cuando un paquete trae varios instaladores
/// (Normal / 2x Speed / Static...), el usuario marca los que quiere
/// (todos por defecto) o cancela. Los ya instalados van marcados.
/// </summary>
public sealed class VariantSelectDialog : Form
{
    private readonly CheckedListBox _list = new()
    {
        Dock = DockStyle.Fill,
        CheckOnClick = true,
    };
    private readonly List<CursorInstaller.InstallCandidate> _candidates;

    /// <summary>Null si el usuario canceló.</summary>
    public List<CursorInstaller.InstallCandidate> Selected { get; private set; }

    public VariantSelectDialog(List<CursorInstaller.InstallCandidate> candidates, bool dark)
    {
        _candidates = candidates;
        Text = $"Seleccionar variantes ({candidates.Count} instaladores)";
        Size = new Size(560, 480);
        MinimumSize = new Size(460, 360);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var lbl = new Label
        {
            Text = "El paquete trae varios instaladores. Marca los que quieras instalar:",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 6),
        };
        root.Controls.Add(lbl, 0, 0);
        root.Controls.Add(_list, 0, 1);

        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            _list.Items.Add(
                $"{c.Group} → {c.Scheme.SchemeName} " +
                $"({c.Scheme.SourceFiles.Count} archivos)" +
                (c.AlreadyInstalled ? "  ● instalado" : ""), true);
        }

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var btnOk = new Button { Text = "Instalar seleccionados", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(170, 32), DialogResult = DialogResult.OK };
        var btnAll = new Button { Text = "Todos", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(90, 32) };
        btnAll.Click += (_, _) => SetAll(true);
        var btnNone = new Button { Text = "Ninguno", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(90, 32) };
        btnNone.Click += (_, _) => SetAll(false);
        var btnCancel = new Button { Text = "Cancelar", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(100, 32), DialogResult = DialogResult.Cancel };
        buttons.Controls.AddRange([btnOk, btnAll, btnNone, btnCancel]);
        root.Controls.Add(buttons, 0, 2);

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
                MessageBox.Show("No hay nada marcado.", "Variantes",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                e.Cancel = true;
                return;
            }
            Selected = picked;
        };

        Theme.Apply(this, dark);
    }

    private void SetAll(bool check)
    {
        for (int i = 0; i < _list.Items.Count; i++)
            _list.SetItemChecked(i, check);
    }
}

