using CursorRotator.Core;

namespace CursorRotator.UI;

/// <summary>
/// Preferencias (modal): lee AppSettings al abrir y guarda cada cambio al
/// instante (CheckedChanged/SelectedIndexChanged → Save).
/// </summary>
public sealed class PrefsDialog : Form
{
    private readonly AppSettings _settings;
    private CheckBox _chkTray, _chkAutostart, _chkStartOnLaunch, _chkIncludeStatic;
    private CheckBox _chkForceTopmost, _chkTooltips, _chkTutorial, _chkSplash, _chkAutoUpdate;
    private ComboBox _cmbRefresh;
    private TextBox _txtRepo;
    private readonly ToolTip _tip = new();

    /// <summary>La principal lo conecta al comprobador de versiones.</summary>
    public event Func<Task> CheckUpdatesNow;

    public PrefsDialog(AppSettings settings, bool dark)
    {
        _settings = settings;
        Text = "Preferencias";
        Size = new Size(470, 560);
        MinimumSize = new Size(440, 520);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(16) };
        Controls.Add(root);
        int row = 0;
        void AddCheck(out CheckBox chk, string text)
        {
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            chk = new CheckBox { Text = text, AutoSize = true, Margin = new Padding(0, 6, 0, 6) };
            root.Controls.Add(chk, 0, row++);
        }

        AddCheck(out _chkTray, "Minimizar a la bandeja al cerrar");
        AddCheck(out _chkAutostart, "Iniciar con Windows");
        AddCheck(out _chkStartOnLaunch, "Rotar al abrir la app");
        AddCheck(out _chkIncludeStatic, "Incluir variantes estáticas (sin animación)");
        AddCheck(out _chkForceTopmost, "Forzar estela sobre pantalla completa (juegos)");
        AddCheck(out _chkTooltips, "Mostrar descripciones emergentes (ToolTips)");
        AddCheck(out _chkTutorial, "Mostrar tutorial al inicio");
        AddCheck(out _chkSplash, "Mostrar pantalla de carga al inicio");

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var refreshRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        refreshRow.Controls.Add(new Label { Text = "Estabilización de refresco:", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 5, 0, 0) });
        _cmbRefresh = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
        _cmbRefresh.Items.AddRange([
            "Auto-Detect (por monitor)",
            "Capped 60 FPS",
            "Capped 144 FPS",
            "Uncapped (máx. rendimiento)"]);
        refreshRow.Controls.Add(_cmbRefresh);
        root.Controls.Add(refreshRow, 0, row++);

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var updateRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        updateRow.Controls.Add(new Label { Text = "Repo GitHub:", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 5, 0, 0) });
        _txtRepo = new TextBox { Width = 170, PlaceholderText = "dueño/CursorRotator" };
        _chkAutoUpdate = new CheckBox { Text = "Avisar", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 3, 0, 0) };
        var btnCheckNow = new Button { Text = "🔄 Buscar ahora", FlatStyle = FlatStyle.Flat, AutoSize = true, Height = 28 };
        btnCheckNow.Click += async (_, _) =>
        {
            if (CheckUpdatesNow is not null) await CheckUpdatesNow();
        };
        updateRow.Controls.Add(_txtRepo);
        updateRow.Controls.Add(_chkAutoUpdate);
        updateRow.Controls.Add(btnCheckNow);
        root.Controls.Add(updateRow, 0, row++);

        // Estado inicial desde memoria (binding manual).
        _chkTray.Checked = _settings.MinimizeToTrayOnClose;
        _chkAutostart.Checked = _settings.StartWithWindows;
        _chkStartOnLaunch.Checked = _settings.StartRotationOnLaunch;
        _chkIncludeStatic.Checked = _settings.IncludeStatic;
        _chkForceTopmost.Checked = _settings.ForceTopmost;
        _chkTooltips.Checked = _settings.ShowTooltips;
        _chkTutorial.Checked = _settings.ShowTutorial;
        _chkSplash.Checked = _settings.ShowSplash;
        _txtRepo.Text = _settings.UpdateRepo ?? "";
        _chkAutoUpdate.Checked = _settings.CheckUpdates;
        _cmbRefresh.SelectedIndex = _settings.RefreshCapHz switch
        {
            60 => 1,
            144 => 2,
            -1 => 3,
            _ => 0,
        };

        _chkTray.CheckedChanged += (_, _) => Save();
        _chkAutostart.CheckedChanged += (_, _) => Save();
        _chkStartOnLaunch.CheckedChanged += (_, _) => Save();
        _chkIncludeStatic.CheckedChanged += (_, _) => Save();
        _chkForceTopmost.CheckedChanged += (_, _) => Save();
        _chkTooltips.CheckedChanged += (_, _) => Save();
        _chkTutorial.CheckedChanged += (_, _) => Save();
        _chkSplash.CheckedChanged += (_, _) => Save();
        _txtRepo.TextChanged += (_, _) => Save();
        _chkAutoUpdate.CheckedChanged += (_, _) => Save();
        _cmbRefresh.SelectedIndexChanged += (_, _) => Save();

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var btnTutorial = new Button { Text = "🎓 Reanudar Tutorial", FlatStyle = FlatStyle.Flat, Dock = DockStyle.Fill, Height = 32 };
        btnTutorial.Click += (_, _) => RelaunchTutorial?.Invoke();
        root.Controls.Add(btnTutorial, 0, row++);

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(new Panel(), 0, row++);
        var btnClose = new Button { Text = "Cerrar", DialogResult = DialogResult.OK, Dock = DockStyle.Fill, Height = 36, FlatStyle = FlatStyle.Flat };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(btnClose, 0, row);

        Theme.Apply(this, dark);
        foreach (var c in new CheckBox[] {
            _chkTray, _chkAutostart, _chkStartOnLaunch, _chkIncludeStatic,
            _chkForceTopmost, _chkTooltips, _chkTutorial, _chkSplash, _chkAutoUpdate })
            Theme.WireCheckState(c, dark);
        _tip.SetToolTip(_txtRepo, "Repositorio 'dueño/nombre' donde publicas los releases (para el aviso de versiones).");
        _tip.SetToolTip(_chkAutoUpdate, "Avisar al abrir si hay versión nueva (máx. 1 vez al día).");
        _tip.SetToolTip(_chkForceTopmost,
            "Reaplica TopMost cada 2 s. Funciona en juegos borderless; no en fullscreen exclusivo.");
        _tip.SetToolTip(_chkTooltips, "Activa o apaga los globos de ayuda en la ventana principal.");
        _tip.SetToolTip(btnTutorial, "Vuelve a mostrar el tutorial paso a paso.");
    }

    /// <summary>La ventana principal lo conecta para reabrir el tutorial.</summary>
    public event Action RelaunchTutorial;

    private void Save()
    {
        _settings.MinimizeToTrayOnClose = _chkTray.Checked;
        _settings.StartWithWindows = _chkAutostart.Checked;
        _settings.StartRotationOnLaunch = _chkStartOnLaunch.Checked;
        _settings.IncludeStatic = _chkIncludeStatic.Checked;
        _settings.ForceTopmost = _chkForceTopmost.Checked;
        _settings.ShowTooltips = _chkTooltips.Checked;
        _settings.ShowTutorial = _chkTutorial.Checked;
        _settings.ShowSplash = _chkSplash.Checked;
        _settings.UpdateRepo = (_txtRepo.Text ?? "").Trim();
        _settings.CheckUpdates = _chkAutoUpdate.Checked;
        _settings.RefreshCapHz = _cmbRefresh.SelectedIndex switch
        {
            1 => 60,
            2 => 144,
            3 => -1,
            _ => 0,
        };
        _settings.Save();
        _settings.ApplyAutostart();
    }
}
