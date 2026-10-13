using CursorRotator.Core;

namespace CursorRotator.UI;

/// <summary>
/// Confirmación de duplicado: el esquema ya está instalado. Ofrece
/// reinstalarlo igualmente, omitirlo u omitir todos los duplicados
/// restantes de la tanda.
/// </summary>
public sealed class DuplicateConfirmDialog : Form
{
    public CursorInstaller.DuplicateAction Result { get; private set; } = CursorInstaller.DuplicateAction.Skip;

    public DuplicateConfirmDialog(string schemeName, bool dark)
    {
        Text = "Esquema ya instalado";
        Size = new Size(480, 240);
        MinimumSize = new Size(440, 220);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(16) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var lblMain = new Label
        {
            Text = $"El cursor '{schemeName}' ya se encuentra instalado.\n¿Quieres instalarlo igualmente?",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 6),
        };
        root.Controls.Add(lblMain, 0, 0);

        var lblNote = new Label
        {
            Text = "(Es probable que sea un error del nombre del archivo pero que no sean los mismos.)",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            Padding = new Padding(0, 0, 0, 6),
        };
        root.Controls.Add(lblNote, 0, 1);
        root.Controls.Add(new Panel(), 0, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var btnYes = new Button { Text = "Instalar igualmente", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(150, 34) };
        btnYes.Click += (_, _) => { Result = CursorInstaller.DuplicateAction.Overwrite; Close(); };
        var btnSkip = new Button { Text = "Omitir", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(100, 34) };
        btnSkip.Click += (_, _) => { Result = CursorInstaller.DuplicateAction.Skip; Close(); };
        var btnSkipAll = new Button { Text = "Omitir todos los duplicados", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(200, 34) };
        btnSkipAll.Click += (_, _) => { Result = CursorInstaller.DuplicateAction.SkipAll; Close(); };
        buttons.Controls.AddRange([btnYes, btnSkip, btnSkipAll]);
        root.Controls.Add(buttons, 0, 3);

        AcceptButton = btnYes;
        Theme.Apply(this, dark);
    }
}

