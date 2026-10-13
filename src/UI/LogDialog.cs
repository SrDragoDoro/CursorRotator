using CursorRotator.Core;

namespace CursorRotator.UI;

/// <summary>Visor del registro de eventos (modal): snapshot + cola en vivo.</summary>
public sealed class LogDialog : Form
{
    private readonly RichTextBox _view = new()
    {
        ReadOnly = true,
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.FixedSingle,
    };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 500 };
    private int _shown;

    public LogDialog(bool dark)
    {
        Text = "Registro de Eventos";
        Size = new Size(560, 420);
        MinimumSize = new Size(420, 300);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9f);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);
        root.Controls.Add(_view, 0, 0);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var btnClear = new Button { Text = "Limpiar vista", FlatStyle = FlatStyle.Flat, AutoSize = true };
        btnClear.Click += (_, _) => { _view.Clear(); _shown = LogStore.Count; };
        var btnClose = new Button { Text = "Cerrar", DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(100, 30) };
        buttons.Controls.Add(btnClear);
        buttons.Controls.Add(btnClose);
        root.Controls.Add(buttons, 0, 1);

        foreach (var line in LogStore.Snapshot())
            _view.AppendText(line + Environment.NewLine);
        _shown = LogStore.Count;
        _timer.Tick += (_, _) => AppendNew();
        _timer.Start();

        Theme.Apply(this, dark);
    }

    private void AppendNew()
    {
        var snap = LogStore.Snapshot();
        for (int i = _shown; i < snap.Count; i++)
            _view.AppendText(snap[i] + Environment.NewLine);
        _shown = snap.Count;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();
        base.OnFormClosed(e);
    }
}
