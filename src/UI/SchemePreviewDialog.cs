using CursorRotator.Core;

namespace CursorRotator.UI;

/// <summary>
/// Galería por roles de un esquema: lo mejor de la lupa de CursorFX, la
/// rejilla de YoloMouse y la tira con insignia de Mousecape. Muestra los 15
/// roles estándar con su vista previa, nombre de rol y archivo exacto;
/// marca archivos faltantes y si es el tema aplicado actualmente.
/// </summary>
public sealed class SchemePreviewDialog : Form
{
    private readonly List<PictureBox> _pics = new();

    public SchemePreviewDialog(string schemeName, bool dark)
    {
        Text = "Roles — " + schemeName;
        Size = new Size(660, 540);
        MinimumSize = new Size(480, 380);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9f);

        string[] paths;
        try { paths = SchemeStore.GetResolvedSchemePaths(schemeName); }
        catch { paths = []; }
        bool applied = IsAppliedScheme(schemeName, paths);
        int ok = paths.Count(File.Exists);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var lblHead = new Label
        {
            Text = $"{schemeName}  ·  {ok}/{paths.Length} archivos  ·  " +
                   (applied ? "● Aplicado ahora" : "No aplicado"),
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 6),
        };
        root.Controls.Add(lblHead, 0, 0);

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true };
        root.Controls.Add(flow, 0, 1);

        var tip = new ToolTip();
        for (int i = 0; i < SchemeStore.StandardRoles.Length; i++)
        {
            string role = SchemeStore.StandardRoles[i];
            string file = i < paths.Length ? paths[i] : null;
            bool exists = file is not null && File.Exists(file);
            Bitmap bmp = exists ? CursorPreviewCache.Get(file) : null;
            var cell = new Panel
            {
                Size = new Size(112, 118),
                Margin = new Padding(4),
                BorderStyle = BorderStyle.FixedSingle,
            };
            var pic = new PictureBox
            {
                Size = new Size(48, 48),
                Location = new Point(32, 8),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = bmp,
            };
            var lblRole = new Label
            {
                Text = role,
                Location = new Point(4, 60),
                Size = new Size(104, 20),
                TextAlign = ContentAlignment.TopCenter,
                Font = new Font("Segoe UI", 8f),
            };
            var lblFile = new Label
            {
                Text = file is null ? "—"
                    : exists ? Path.GetFileName(file) : "⚠ falta archivo",
                Location = new Point(4, 80),
                Size = new Size(104, 34),
                TextAlign = ContentAlignment.TopCenter,
                Font = new Font("Segoe UI", 7.5f),
            };
            cell.Controls.AddRange([pic, lblRole, lblFile]);
            string full = $"{role} → {file ?? "(sin archivo en el esquema)"}";
            tip.SetToolTip(cell, full);
            tip.SetToolTip(pic, full);
            tip.SetToolTip(lblFile, full);
            flow.Controls.Add(cell);
            _pics.Add(pic);
        }

        var btnClose = new Button
        {
            Text = "Cerrar",
            DialogResult = DialogResult.OK,
            Dock = DockStyle.Fill,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
        };
        root.Controls.Add(btnClose, 0, 2);

        FormClosed += (_, _) =>
        {
            foreach (var p in _pics)
                try { p.Image?.Dispose(); } catch { }
        };

        Theme.Apply(this, dark);
    }

    /// <summary>¿Es este esquema el que Windows tiene aplicado ahora mismo?</summary>
    public static bool IsAppliedScheme(string schemeName, string[] resolvedPaths = null)
    {
        try
        {
            resolvedPaths ??= SchemeStore.GetResolvedSchemePaths(schemeName);
            if (resolvedPaths.Length == 0) return false;
            string arrow = CursorColorReader.GetActiveArrowPath();
            if (string.IsNullOrEmpty(arrow)) return false;
            return string.Equals(SchemeStore.ResolveCursorPath(arrow), resolvedPaths[0],
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
