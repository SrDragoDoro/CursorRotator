using CursorRotator.Core;

namespace CursorRotator.UI;

/// <summary>
/// Pantalla de descarga/extracción: ProgressBar + estado. Se muestra modal;
/// al aparecer (Shown) corre el trabajo y cierra con OK, Cancel o Abort.
/// </summary>
public sealed class UpdateProgressForm : Form
{
    private readonly string _url;
    private readonly string _zipPath;
    private readonly string _extractDir;
    private readonly ProgressBar _bar = new()
    {
        Dock = DockStyle.Fill,
        Minimum = 0,
        Maximum = 100,
    };
    private readonly Label _lblStatus = new()
    {
        Text = "Descargando paquete...",
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
    };
    private readonly CancellationTokenSource _cts = new();

    public UpdateProgressForm(string url, string zipPath, string extractDir, bool dark)
    {
        _url = url;
        _zipPath = zipPath;
        _extractDir = extractDir;
        Text = "Descargando actualización";
        Size = new Size(420, 140);
        MinimumSize = Size;
        MaximumSize = Size;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9.5f);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(16) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);
        root.Controls.Add(_lblStatus, 0, 0);
        root.Controls.Add(_bar, 0, 1);
        var btnCancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 110, FlatStyle = FlatStyle.Flat };
        btnCancel.Click += (_, _) =>
        {
            try { _cts.Cancel(); } catch { }
        };
        root.Controls.Add(btnCancel, 0, 2);

        Theme.Apply(this, dark);
        Shown += async (_, _) => await RunAsync();
    }

    private void Report(int percent, string status)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(() => Report(percent, status)); } catch { }
            return;
        }
        _bar.Value = Math.Clamp(percent, 0, 100);
        if (!string.IsNullOrEmpty(status)) _lblStatus.Text = status;
        Text = $"Descargando actualización — {Math.Clamp(percent, 0, 100)}%";
    }

    private async Task RunAsync()
    {
        try
        {
            var progress = new Progress<int>(p => Report(p, null));
            await UpdateChecker.DownloadAsync(_url, _zipPath, progress, _cts.Token);
            Report(100, "Extrayendo...");
            await Task.Run(() =>
                System.IO.Compression.ZipFile.ExtractToDirectory(
                    _zipPath, _extractDir, overwriteFiles: true), _cts.Token);
            if (!File.Exists(Path.Combine(_extractDir, AutoUpdater.ExeName)))
                throw new InvalidOperationException(
                    "El paquete no contiene " + AutoUpdater.ExeName + ".");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
        catch (Exception ex)
        {
            try
            {
                MessageBox.Show(this, "No se pudo completar: " + ex.Message,
                    "Actualización", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch { }
            DialogResult = DialogResult.Abort;
            Close();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _cts.Cancel(); } catch { }
            _cts.Dispose();
        }
        base.Dispose(disposing);
    }
}
