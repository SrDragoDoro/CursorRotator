using CursorRotator.Core;
using CursorRotator.Core.Trails;

namespace CursorRotator.UI;

/// <summary>
/// Widget compacto: estado, acciones, intervalo, instalación, estelas
/// (galería + paletas), accesos a gestor/registro/opciones. Sin listas
/// empotradas: esquemas y registro viven en modales.
/// </summary>
public sealed class MainForm : Form
{
    private readonly CursorRotator.Core.CursorRotator _rotator = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private TrayManager _tray;
    private readonly System.Windows.Forms.Timer _clock = new() { Interval = 1000 };
    private SchemesDialog _schemesDialog;
    private LogDialog _logDialog;

    // Paletas (tema oscuro original).
    private static readonly Color Accent = Color.FromArgb(0x7C, 0x5C, 0xFF);
    private static readonly Color OkGreen = Color.FromArgb(0x2E, 0xCC, 0x71);
    private static readonly Color WarnAmber = Color.FromArgb(0xE6, 0x7E, 0x22);

    private bool _dark;
    private Color _bg, _card, _fg, _muted;

    // Controles.
    private Button _btnTheme;
    private Panel _statusDot;
    private Label _lblStatus, _lblCurrent, _lblNext;
    private PictureBox _picCursorActivo, _picCursorProximo;
    private Label _lblCapActivo, _lblCapProximo;
    private Panel _pnlPreview;
    private NumericUpDown _numInterval;
    private ComboBox _cmbUnit;
    private DateTimePicker _dtpTime;
    private ListBox _lstTimes;
    private Button _btnSchemePicker;
    private Button _btnAddTime, _btnRemoveTime;
    /// <summary>Destino elegido para la próxima hora ("" = aleatorio).</summary>
    private string _pickedScheme = "";
    private Button _btnToggle, _btnOnce, _btnManual, _btnInstall;
    private Button _btnGallery, _btnSchemes, _btnViewLog, _btnPrefs, _btnApplyTrail, _btnClickGallery;
    private Button _btnSecondaryGallery;
    private CheckBox _chkSecondaryTrail;
    private Label _lblSecondaryWarn;
    private Panel _progressBack, _progressFill;
    private CheckBox _chkTrail, _chkClicks, _chkClickColor;
    private Button _btnManualColor;
    private Panel _pnlTrailColor;
    private Color _detectedTrailColor = Color.Empty;
    private ComboBox _cmbTrailEngine, _cmbTrailOrigin;
    private RadioButton _rbColorNative, _rbColorInherited, _rbColorManual;
    private TrackBar _trkTrailSize, _trkTrailLife, _trkTrailDensity, _trkClickSize;
    private Label _lblTrailSize, _lblTrailLife, _lblTrailDensity, _lblClickSize;
    private TrailOverlay _trail;
    private Panel _statusCard, _pnlTrail, _pnlTrailScroll;
    private TableLayoutPanel _trailTable;
    private FlowLayoutPanel _colorRow;
    private Panel _root, _mid, _bottom;
    private TableLayoutPanel _top;
    /// <summary>
    /// Último esquema aplicado de verdad (cualquier vía: rotación, azar,
    /// gestor). La vista previa lo usa como fuente de verdad porque
    /// _rotator.CurrentScheme no se entera de los aplicados manuales.
    /// </summary>
    private string _lastAppliedScheme;
    private bool _suspendDirty;
    /// <summary>Salida en curso (el menú Salir no debe ser cancelado).</summary>
    private bool _exiting;
    private ToolTip _tip;
    private readonly Dictionary<Control, string> _tipTexts = new();

    public AppSettings Settings => _settings;
    public bool IsDarkMode => _dark;

    public MainForm(bool startMinimized)
    {
        Text = "CursorRotator — Instalador y rotador de cursores (build 2026-10-11.4)";
        Size = new Size(660, 780);
        MinimumSize = new Size(560, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        _dark = _settings.DarkMode;
        BuildLayout();
        RestoreWindowGeometry();
        ApplyTheme();

        _rotator.SchemeApplied += name => BeginInvoke(() => OnSchemeApplied(name));
        _rotator.Failed += msg => BeginInvoke(() => Log("Rotador: " + msg));
        _clock.Tick += (_, _) => UpdateCountdown();
        _clock.Start();

        UpdateStatus();
        LoadSettingsToUi();
        RefreshCursorPreview();
        RefreshTrailOverlay();
        if (_settings.TrailEnabled)
            SetTrailEnabled(true);
        // La vista de estelas siempre arranca arriba (nunca a mitad).
        try
        {
            if (_pnlTrailScroll is not null)
            {
                _pnlTrailScroll.AutoScrollPosition = new Point(0, 0);
                _pnlTrailScroll.ScrollControlIntoView(_chkTrail);
            }
        }
        catch { }
        Log("CursorRotator build 2026-10-11.4 listo.");

        _tray = new TrayManager(this,
            onShow: () => BeginInvoke(ShowFromTray),
            onToggle: () => BeginInvoke(() => ToggleRotation()),
            onOnce: () => BeginInvoke(() => ApplyNext()),
            onExit: () => BeginInvoke(() => Quit()));

        if (startMinimized) BeginInvoke(HideToTray);
        else if (_settings.StartRotationOnLaunch && SchemeStore.GetSchemeNames().Count > 0)
            ToggleRotation();
    }

    // ---------- Construcción ----------
    private void BuildLayout()
    {
        // El ToolTip nace lo primero: Tip() se usa durante la construcción.
        _tip = new ToolTip
        {
            InitialDelay = 400,
            AutoPopDelay = 5000,
            ReshowDelay = 100,
            UseFading = true,
            UseAnimation = true,
            OwnerDraw = true,
            Active = _settings.ShowTooltips,
        };
        _tip.Draw += (s, e) =>
        {
            using var back = new SolidBrush(Color.FromArgb(0x2A, 0x2A, 0x3D));
            e.Graphics.FillRectangle(back, e.Bounds);
            using var border = new Pen(Accent);
            e.Graphics.DrawRectangle(border, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
            TextRenderer.DrawText(e.Graphics, e.ToolTipText, Font, e.Bounds,
                Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        };
        _tip.Popup += (s, e) =>
        {
            string text = "";
            if (e.AssociatedWindow is Control ctl)
                _tipTexts.TryGetValue(ctl, out text);
            text ??= "";
            var lines = text.Split('\n');
            int w = 0;
            using var g = CreateGraphics();
            foreach (var line in lines)
                w = Math.Max(w, (int)g.MeasureString(line, Font).Width);
            e.ToolTipSize = new Size(Math.Min(320, w + 16), lines.Length * 20 + 10);
        };
        // Raíz con fondo explícito (nunca se ve el gris nativo al estirar).
        // Layout MANUAL (ver LayoutRoot): sin peleas Dock/AutoSize.
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0), AutoScroll = false, BackColor = _bg };
        _root = root;
        Controls.Add(root);
        root.Resize += (_, _) => LayoutRoot();

        // Banda superior fija con el contenido principal. Sin AutoSize: el
        // ancho SIEMPRE sigue a la ventana (con AutoSize la tabla se
        // encogía al contenido y parecía de tamaño fijo).
        var top = new TableLayoutPanel
        {
            AutoSize = false,
            ColumnCount = 1,
        };
        _top = top;
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 0 header
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 1 estado
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); // 2 toggle
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // 3 once+manual
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 4 intervalo
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); // 5 install
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 6 progreso
        root.Controls.Add(top);

        // Cabecera que refluye con la ventana: título a la izquierda,
        // botón de tema siempre visible a la derecha.
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0),
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var titleFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0) };
        titleFlow.Controls.Add(new Label { Text = "🖱️ CursorRotator", Font = new Font("Segoe UI", 16, FontStyle.Bold), AutoSize = true });
        titleFlow.Controls.Add(new Label { Text = "   Instala y rota tus cursores automáticamente", AutoSize = true, Anchor = AnchorStyles.Left });
        _btnTheme = new Button { Text = "🌙", Width = 44, Height = 32, FlatStyle = FlatStyle.Flat, Anchor = AnchorStyles.Right };
        _btnTheme.Click += (_, _) => { _dark = !_dark; _settings.DarkMode = _dark; _settings.Save(); ApplyTheme(); };
        header.Controls.Add(titleFlow, 0, 0);
        header.Controls.Add(_btnTheme, 1, 0);
        top.Controls.Add(header, 0, 0);

        // Estado: textos a la izquierda + vista previa de cursores a la derecha.
        _statusCard = new Panel { Dock = DockStyle.Fill, Height = 108, Padding = new Padding(12), Margin = new Padding(0, 8, 0, 8) };
        _statusDot = new Panel { Size = new Size(14, 14), Location = new Point(14, 14) };
        _lblStatus = new Label { Text = "Pausado", Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(34, 10), AutoSize = true };
        _lblCurrent = new Label { Text = "Esquema actual: —", Location = new Point(14, 36), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
        _lblNext = new Label { Text = "Próximo cambio: —", Location = new Point(14, 58), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
        _statusCard.Controls.AddRange([_statusDot, _lblStatus, _lblCurrent, _lblNext]);

        // Vista previa (derecha): activo + próximo, 40×40 con ToolTip nominal.
        _pnlPreview = new Panel { Dock = DockStyle.Right, Width = 132, Padding = new Padding(0), Margin = new Padding(0) };
        var lblPrevTitle = new Label { Text = "Vista previa", Dock = DockStyle.Top, Height = 16, TextAlign = ContentAlignment.TopCenter, Font = new Font("Segoe UI", 7.5f) };
        var prevCols = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = new Padding(0), Padding = new Padding(0) };
        prevCols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        prevCols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        prevCols.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        prevCols.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _picCursorActivo = new PictureBox { Size = new Size(40, 40), SizeMode = PictureBoxSizeMode.Zoom, Anchor = AnchorStyles.None, Margin = new Padding(0), Cursor = Cursors.Hand };
        _picCursorProximo = new PictureBox { Size = new Size(40, 40), SizeMode = PictureBoxSizeMode.Zoom, Anchor = AnchorStyles.None, Margin = new Padding(0), Cursor = Cursors.Hand };
        _picCursorActivo.Click += (_, _) => OpenSchemeRoles(_picCursorActivo.Tag as string);
        _picCursorProximo.Click += (_, _) => OpenSchemeRoles(_picCursorProximo.Tag as string);
        _lblCapActivo = new Label { Text = "Actual", TextAlign = ContentAlignment.TopCenter, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), AutoSize = false, Height = 16 };
        _lblCapProximo = new Label { Text = "Siguiente", TextAlign = ContentAlignment.TopCenter, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), AutoSize = false, Height = 16 };
        prevCols.Controls.Add(_picCursorActivo, 0, 0);
        prevCols.Controls.Add(_picCursorProximo, 1, 0);
        prevCols.Controls.Add(_lblCapActivo, 0, 1);
        prevCols.Controls.Add(_lblCapProximo, 1, 1);
        _pnlPreview.Controls.Add(prevCols);
        _pnlPreview.Controls.Add(lblPrevTitle);
        _statusCard.Controls.Add(_pnlPreview);
        _pnlPreview.BringToFront();
        top.Controls.Add(_statusCard, 0, 1);

        _btnToggle = new Button { Text = "▶  Activar rotación", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11, FontStyle.Bold), Margin = new Padding(0, 2, 0, 2) };
        _btnToggle.Click += (_, _) => ToggleRotation();
        top.Controls.Add(_btnToggle, 0, 2);

        var secondary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
        secondary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        secondary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _btnOnce = new Button { Text = "⏭ Aplicar siguiente", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 2, 3, 2) };
        _btnOnce.Click += (_, _) => ApplyNext();
        _btnManual = new Button { Text = "🖱️ Ajuste manual...", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, Margin = new Padding(3, 2, 0, 2) };
        _btnManual.Click += (_, _) => { _rotator.Stop(); UpdateStatus(); SchemeStore.OpenMouseProperties(); Log("Control devuelto al usuario (panel de mouse abierto)."); };
        secondary.Controls.Add(_btnOnce, 0, 0);
        secondary.Controls.Add(_btnManual, 1, 0);
        top.Controls.Add(secondary, 0, 3);

        var interval = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        interval.Controls.Add(new Label { Text = "Cambiar cada:", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) });
        _numInterval = new NumericUpDown { Minimum = 1, Maximum = 1440, Value = 15, Width = 70, DecimalPlaces = 1, Increment = 1 };
        _cmbUnit = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
        _cmbUnit.Items.AddRange(["Segundos", "Minutos", "Horas", "Horas fijas diarias"]);
        _cmbUnit.SelectedIndex = 1;
        _dtpTime = new DateTimePicker { Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 90, Visible = false, Value = DateTime.Today.AddHours(9) };
        _btnSchemePicker = new Button { Text = "🎲 Aleatorio", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(150, 28), Visible = false };
        _btnSchemePicker.Click += (_, _) => OpenSchemePicker();
        _btnAddTime = new Button { Text = "+ Añadir", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(80, 28), Visible = false };
        _btnAddTime.Click += (_, _) => AddFixedTime();
        // Lista integrada: más alta + scroll siempre visible (nada de
        // "cuadro sin barra"). Ancha para "11:00 → nombre del esquema".
        _lstTimes = new ListBox
        {
            Width = 210, Height = 84, Visible = false,
            ScrollAlwaysVisible = true, IntegralHeight = false,
        };
        _btnRemoveTime = new Button { Text = "Quitar", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(70, 28), Visible = false };
        _btnRemoveTime.Click += (_, _) => RemoveFixedTime();
        _numInterval.ValueChanged += (_, _) => OnIntervalChanged();
        _cmbUnit.SelectedIndexChanged += (_, _) => { ApplyUnitUi(); OnIntervalChanged(); };
        _dtpTime.ValueChanged += (_, _) => OnIntervalChanged();
        interval.Controls.Add(_numInterval);
        interval.Controls.Add(_cmbUnit);
        interval.Controls.Add(_dtpTime);
        interval.Controls.Add(_btnSchemePicker);
        interval.Controls.Add(_btnAddTime);
        interval.Controls.Add(_lstTimes);
        interval.Controls.Add(_btnRemoveTime);
        top.Controls.Add(interval, 0, 4);

        _btnInstall = new Button { Text = "📦 Instalación en 1 clic", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), Margin = new Padding(0, 6, 0, 0) };
        _btnInstall.Click += async (_, _) => await RunInstallAsync();
        top.Controls.Add(_btnInstall, 0, 5);

        _progressBack = new Panel { Dock = DockStyle.Fill, Height = 10, Margin = new Padding(0, 6, 0, 0), BorderStyle = BorderStyle.FixedSingle };
        _progressFill = new Panel { Dock = DockStyle.Left, Width = 0 };
        _progressBack.Controls.Add(_progressFill);
        top.Controls.Add(_progressBack, 0, 6);

        // Estelas: panel plano sin bordes, mismo fondo que la tarjeta de estado
        // (bloque visual unificado). Scroll interno si falta espacio.
        _pnlTrail = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Padding = new Padding(10, 8, 4, 8), Margin = new Padding(0, 8, 0, 0) };
        _pnlTrailScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BorderStyle = BorderStyle.None };
        _trailTable = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Dock = DockStyle.Top };
        var trail = _trailTable;
        trail.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        trail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        trail.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _chkTrail = new CheckBox { Text = "Activar estelas", AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        _chkTrail.CheckedChanged += (_, _) => SetTrailEnabled(_chkTrail.Checked);
        _btnGallery = new Button { Text = "✨ Estela Continua...", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, Height = 32 };
        _btnGallery.Click += (_, _) => OpenGallery();
        trail.Controls.Add(_chkTrail, 0, 0);
        trail.Controls.Add(_btnGallery, 1, 0);
        trail.SetColumnSpan(_btnGallery, 2);

        // Estela secundaria concurrente (dual trail): mismo motor de render,
        // colección de partículas independiente.
        _chkSecondaryTrail = new CheckBox { Text = "Activar estela secundaria ⚠️ (Puede aumentar exigencia de recursos)", AutoSize = true };
        _chkSecondaryTrail.CheckedChanged += (_, _) => OnSecondaryToggled();
        _btnSecondaryGallery = new Button { Text = "✨ Estela Secundaria: [Ninguna]", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, Height = 32, Enabled = false };
        _btnSecondaryGallery.Click += (_, _) => OpenSecondaryGallery();
        trail.Controls.Add(_chkSecondaryTrail, 0, 1);
        trail.SetColumnSpan(_chkSecondaryTrail, 3);
        trail.Controls.Add(_btnSecondaryGallery, 0, 2);
        trail.SetColumnSpan(_btnSecondaryGallery, 3);
        _lblSecondaryWarn = new Label
        {
            Text = "⚠ Aviso: Ejecutar dos estelas de partículas simultáneas incrementa la carga de la CPU/GPU según la densidad elegida.",
            AutoSize = true,
            ForeColor = Color.FromArgb(0xE6, 0x7E, 0x22),
            Font = new Font("Segoe UI", 8f),
            Visible = false,
            MaximumSize = new Size(480, 0),
        };
        trail.Controls.Add(_lblSecondaryWarn, 0, 3);
        trail.SetColumnSpan(_lblSecondaryWarn, 3);

        trail.Controls.Add(new Label { Text = "Motor:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        _cmbTrailEngine = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        _cmbTrailEngine.Items.AddRange([
            "⏱️ Clásico / Timer (pred.)",
            "⚡ Hook de alta frecuencia",
            "〰️ Spline / Bézier suavizado",
            "🎱 Física con inercia",
            "🧵 Asíncrono multihilo",
            "🔮 Predictivo Hermite",
            "🎯 Raw Input (cero latencia)"]);
        _cmbTrailEngine.SelectedIndexChanged += OnSettingsChanged;
        trail.Controls.Add(_cmbTrailEngine, 1, 4);
        trail.SetColumnSpan(_cmbTrailEngine, 2);

        trail.Controls.Add(new Label { Text = "Origen:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        _cmbTrailOrigin = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        _cmbTrailOrigin.Items.AddRange(["📍 Desde la punta (pred.)", "➡️ Desde la cola (+16,+20)"]);
        _cmbTrailOrigin.SelectedIndexChanged += OnSettingsChanged;
        trail.Controls.Add(_cmbTrailOrigin, 1, 5);
        trail.SetColumnSpan(_cmbTrailOrigin, 2);

        AddSliderRow(trail, 6, "Tamaño:", out _trkTrailSize, out _lblTrailSize, 5, 30, 10);
        AddSliderRow(trail, 7, "Duración:", out _trkTrailLife, out _lblTrailLife, 3, 100, 10);
        AddSliderRow(trail, 8, "Cantidad:", out _trkTrailDensity, out _lblTrailDensity, 1, 30, 10);

        _rbColorNative = new RadioButton { Text = "Predeterminado", AutoSize = true };
        _rbColorInherited = new RadioButton { Text = "Heredado", AutoSize = true };
        _rbColorManual = new RadioButton { Text = "Manual", AutoSize = true };
        _rbColorNative.CheckedChanged += OnSettingsChanged;
        _rbColorInherited.CheckedChanged += OnSettingsChanged;
        _rbColorManual.CheckedChanged += OnSettingsChanged;
        _btnManualColor = new Button { Text = "🎨 Elegir color manual", FlatStyle = FlatStyle.Flat, AutoSize = true, Height = 28 };
        _btnManualColor.Click += (_, _) => PickManualColor();
        _pnlTrailColor = new Panel { Size = new Size(64, 22), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.Gray, Anchor = AnchorStyles.Left };
        var colorRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        _colorRow = colorRow;
        colorRow.Controls.Add(_rbColorNative);
        colorRow.Controls.Add(_rbColorInherited);
        colorRow.Controls.Add(_rbColorManual);
        colorRow.Controls.Add(_btnManualColor);
        colorRow.Controls.Add(_pnlTrailColor);
        trail.Controls.Add(colorRow, 0, 9);
        trail.SetColumnSpan(colorRow, 3);

        _chkClicks = new CheckBox { Text = "Efectos de clic", AutoSize = true };
        _chkClicks.CheckedChanged += OnSettingsChanged;
        _btnClickGallery = new Button { Text = "💥 Efecto al Hacer Clic...", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, Height = 32 };
        _btnClickGallery.Click += (_, _) => OpenClickGallery();
        trail.Controls.Add(_chkClicks, 0, 10);
        trail.Controls.Add(_btnClickGallery, 1, 10);
        trail.SetColumnSpan(_btnClickGallery, 2);

        AddSliderRow(trail, 11, "Tamaño clic:", out _trkClickSize, out _lblClickSize, 5, 30, 10);
        Tip(_trkClickSize, "Tamaño de los efectos de clic (0.5× … 3.0×).");
        _chkClickColor = new CheckBox { Text = "Heredar color para los clics", AutoSize = true };
        _chkClickColor.CheckedChanged += OnSettingsChanged;
        Tip(_chkClickColor, "Si está marcado, el estallido usa la paleta de la estela; si no, sus colores nativos.");
        trail.Controls.Add(_chkClickColor, 0, 12);
        trail.SetColumnSpan(_chkClickColor, 3);

        _btnApplyTrail = new Button { Text = "↻ Reiniciar estela", Dock = DockStyle.Top, FlatStyle = FlatStyle.Flat, Height = 32, Margin = new Padding(0, 6, 0, 0), Enabled = true, MinimumSize = new Size(0, 32), MaximumSize = new Size(10000, 32) };
        _btnApplyTrail.Click += (_, _) => RestartTrailOverlay();
        trail.Controls.Add(_btnApplyTrail, 0, 13);
        trail.SetColumnSpan(_btnApplyTrail, 3);
        _pnlTrailScroll.Controls.Add(trail);
        _pnlTrail.Controls.Add(_pnlTrailScroll);

        // Accesos: gestor, registro y preferencias (Panel2 del splitter).
        var access = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        _btnSchemes = new Button { Text = "📂 Gestor de Esquemas", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(170, 34) };
        _btnSchemes.Click += (_, _) => OpenSchemesDialog();
        _btnViewLog = new Button { Text = "🔍 Ver Registro", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(130, 34) };
        _btnViewLog.Click += (_, _) => OpenLogDialog();
        _btnPrefs = new Button { Text = "⚙️ Opciones Avanzadas", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(170, 34) };
        _btnPrefs.Click += (_, _) => OpenPrefsDialog();
        access.Controls.AddRange([_btnSchemes, _btnViewLog, _btnPrefs]);

        // Banda inferior fija: accesos siempre visibles y anclados abajo.
        var bottom = new Panel { Height = 50, Padding = new Padding(0, 8, 0, 0) };
        _bottom = bottom;
        bottom.Controls.Add(access);
        root.Controls.Add(bottom);

        // Banda intermedia: las estelas absorben todo el espacio restante.
        var mid = new Panel { Padding = new Padding(0, 8, 0, 0) };
        _mid = mid;
        mid.Controls.Add(_pnlTrail);
        root.Controls.Add(mid);

        LayoutRoot();
        SetupTooltips();
        WireCheckStates();
    }

    /// <summary>
    /// Estados marcado/sin marcar inconfundibles (usa el tema VIVO: al
    /// alternar claro/oscuro se refrescan en ApplyTheme).
    /// </summary>
    private void WireCheckStates()
    {
        foreach (var c in new CheckBox[] {
            _chkTrail, _chkSecondaryTrail, _chkClicks, _chkClickColor })
        {
            c.CheckedChanged += (_, _) => Theme.RefreshCheckState(c, _dark);
            Theme.RefreshCheckState(c, _dark);
        }
        foreach (var r in new RadioButton[] {
            _rbColorNative, _rbColorInherited, _rbColorManual })
        {
            r.CheckedChanged += (_, _) => Theme.RefreshCheckState(r, _dark);
            Theme.RefreshCheckState(r, _dark);
        }
    }

    private void RefreshCheckStates()
    {
        foreach (var c in new CheckBox[] {
            _chkTrail, _chkSecondaryTrail, _chkClicks, _chkClickColor })
            Theme.RefreshCheckState(c, _dark);
        foreach (var r in new RadioButton[] {
            _rbColorNative, _rbColorInherited, _rbColorManual })
            Theme.RefreshCheckState(r, _dark);
    }

    /// <summary>
    /// Geometría explícita: top arriba, accesos abajo, estelas con el resto.
    /// Sin ambigüedad de orden Dock: determinista ante cualquier resize.
    /// </summary>
    private void LayoutRoot()
    {
        if (_root is null || _top is null || _mid is null || _bottom is null) return;
        try
        {
            const int px = 16, py = 12, gap = 8, bottomH = 50;
            int W = _root.ClientSize.Width, H = _root.ClientSize.Height;
            if (W <= 0 || H <= 0) return;
            int topH = _top.GetPreferredSize(new Size(Math.Max(0, W - 2 * px), 0)).Height;
            _top.Bounds = new Rectangle(px, py, Math.Max(0, W - 2 * px), topH);
            _bottom.Bounds = new Rectangle(px, H - py - bottomH, Math.Max(0, W - 2 * px), bottomH);
            int midY = _top.Bottom + gap;
            int midH = Math.Max(0, _bottom.Top - gap - midY);
            _mid.Bounds = new Rectangle(px, midY, Math.Max(0, W - 2 * px), midH);
        }
        catch { /* layout por defecto si algo falla */ }
    }

    /// <summary>ToolTip global oscuro y estilizado (owner-drawn).</summary>
    private void Tip(Control control, string text)
    {
        _tipTexts[control] = text;
        _tip.SetToolTip(control, text);
    }

    private void SetupTooltips()
    {
        // Solo textos: el componente único nació al inicio de BuildLayout.
        Tip(_btnToggle, "Activa o pausa la rotación automática de cursores.");
        Tip(_btnOnce, "Aplica ahora mismo el esquema mostrado como 'Siguiente' en la vista previa.");
        Tip(_btnManual, "Abre el panel de mouse de Windows para ajuste manual.");
        Tip(_btnInstall, "Extrae, copia y registra todos los cursores en 1 clic.");
        Tip(_numInterval, "Cada cuántos segundos, minutos u horas cambia el cursor (decimales con el teclado, p. ej. 1.5 min).");
        Tip(_cmbUnit, "Unidad del intervalo... u 'Horas fijas diarias' para programar varias horas concretas.");
        Tip(_dtpTime, "Elige una hora y el cursor destino, y pulsa + Añadir (p. ej. 11:00, 15:00, 18:00).");
        Tip(_btnSchemePicker, "Abre la galería para elegir qué cursor se aplicará a esa hora (o 🎲 Aleatorio).");
        Tip(_btnAddTime, "Añade la hora con su cursor a la agenda diaria.");
        Tip(_lstTimes, "Agenda: hora → cursor (🎲 = aleatorio de la cola). Con scroll.");
        Tip(_btnRemoveTime, "Quita la hora seleccionada de la agenda.");
        Tip(_btnGallery, "Abre la galería visual de 40 estelas por categorías.");
        Tip(_chkSecondaryTrail, "Activa una segunda estela simultánea (dual trail). Aumenta la carga de CPU/GPU según la densidad.");
        Tip(_btnSecondaryGallery, "Elige el efecto de la estela secundaria.");
        Tip(_cmbTrailEngine, "Motor de captura y física: Timer, Hook, Spline, Inercial, Async, Predictivo o Raw Input.");
        Tip(_cmbTrailOrigin, "Punta: la estela nace en el cursor. Cola: nace +16,+20 (sale de atrás).");
        Tip(_trkTrailSize, "Tamaño de las partículas de la estela.");
        Tip(_trkTrailLife, "Duración: hasta 10×, la estela persiste varios segundos.");
        Tip(_trkTrailDensity, "Define el límite máximo de partículas o la densidad del efecto generado.");
        Tip(_chkTrail, "Activa o desactiva el overlay de estelas.");
        Tip(_rbColorNative, "Colores originales de cada efecto, sin heredar nada.");
        Tip(_rbColorInherited, "Tiñe la estela con la paleta del cursor activo (.cur/.ani).");
        Tip(_rbColorManual, "Tiñe la estela con tu color (genera base, claro y oscuro).");
        Tip(_btnManualColor, "Elige un color personalizado para la estela.");
        Tip(_pnlTrailColor, "Color activo detectado o elegido.");
        Tip(_chkClicks, "Activa ráfagas visuales al hacer clic.");
        Tip(_btnClickGallery, "Abre la galería de 20 efectos de clic por categorías.");
        Tip(_btnApplyTrail, "Vuelve a crear el overlay de estelas (limpia hilos y bitmaps si se queda pegada). Los ajustes ya aplican solos al instante.");
        Tip(_btnSchemes, "Gestiona, aplica y elimina esquemas instalados.");
        Tip(_btnViewLog, "Abre el registro de eventos en una ventana.");
        Tip(_btnPrefs, "Preferencias: bandeja, inicio, topmost y refresco.");
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Geometría final + vista arriba del todo, sin importar el foco.
        try
        {
            LayoutRoot();
            ResetTrailView();
        }
        catch { }
        // Re-afirmado tardío: cubre passes de layout/foco posteriores al Shown.
        var once = new System.Windows.Forms.Timer { Interval = 500 };
        once.Tick += (_, _) =>
        {
            once.Stop();
            once.Dispose();
            try
            {
                LayoutRoot();
                ResetTrailView();
                if (_btnTheme is not null && !_btnTheme.IsDisposed)
                    ActiveControl = _btnTheme; // el foco arriba, nunca abajo
            }
            catch { }
        };
        once.Start();
        // Aviso de versión nueva (no bloquea, tragad-errores).
        BeginInvoke(async () =>
        {
            try { await AutoUpdateCheckAsync(); } catch { }
        });
    }

    private void ResetTrailView()
    {
        if (_pnlTrailScroll is null || _pnlTrailScroll.IsDisposed) return;
        _pnlTrailScroll.AutoScrollPosition = new Point(0, 0);
        if (_chkTrail is not null && !_chkTrail.IsDisposed)
            _pnlTrailScroll.ScrollControlIntoView(_chkTrail);
    }

    private void AddSliderRow(TableLayoutPanel trail, int row, string label,
        out TrackBar track, out Label value, int min, int max, int def)
    {
        trail.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        track = new TrackBar { Minimum = min, Maximum = max, Value = def, TickFrequency = 5, Dock = DockStyle.Fill, Height = 30 };
        // Tiempo real en sliders: aplican al instante (arrastre, teclado, rueda
        // y salto absoluto); el resto de controles usa el patrón dirty + botón.
        // OJO: durante LoadSettingsToUi (_suspendDirty) NO aplicar: el guardado
        // a medias envenenaba _settings y el archivo con valores por defecto.
        track.Scroll += (_, _) => { if (!_suspendDirty) ApplyTrailOptions(); };
        track.ValueChanged += (_, _) => { if (!_suspendDirty) ApplyTrailOptions(); };
        // Salto absoluto: clic en la pista salta al punto (no LargeChange).
        track.MouseDown += (s, e) =>
        {
            if (s is not TrackBar tb || tb.Width <= 1) return;
            double ratio = (double)e.X / tb.Width;
            tb.Value = Math.Clamp(
                (int)(tb.Minimum + ratio * (tb.Maximum - tb.Minimum)),
                tb.Minimum, tb.Maximum);
        };
        value = new Label { Text = "1.0×", AutoSize = true, Anchor = AnchorStyles.Left };
        trail.Controls.Add(track, 1, row);
        trail.Controls.Add(value, 2, row);
    }

    // ---------- Tema oscuro ----------
    private void ApplyTheme()
    {
        if (_dark)
        {
            _bg = Color.FromArgb(0x1E, 0x1E, 0x2E); _card = Color.FromArgb(0x2A, 0x2A, 0x3D);
            _fg = Color.White; _muted = Color.FromArgb(170, 170, 190);
        }
        else
        {
            _bg = Color.FromArgb(242, 242, 247); _card = Color.White;
            _fg = Color.Black; _muted = Color.FromArgb(90, 90, 110);
        }
        BackColor = _bg;
        _btnTheme.Text = _dark ? "☀️" : "🌙";
        ApplyThemeTo(Controls, _bg, _fg);
        _statusCard.BackColor = _card;
        _pnlTrail.BackColor = _card;
        _pnlTrailScroll.BackColor = _card;
        _trailTable.BackColor = _card;
        _colorRow.BackColor = _card;
        if (_pnlPreview is not null)
        {
            _pnlPreview.BackColor = _card;
            foreach (Control c in _pnlPreview.Controls) { c.BackColor = _card; c.ForeColor = _muted; }
            foreach (Control c in _pnlPreview.Controls)
                if (c is TableLayoutPanel t)
                    foreach (Control cc in t.Controls)
                    { if (cc is PictureBox pb) pb.BackColor = _card; else { cc.BackColor = _card; cc.ForeColor = _muted; } }
        }
        if (_lblSecondaryWarn is not null)
            _lblSecondaryWarn.ForeColor = Color.FromArgb(0xE6, 0x7E, 0x22);
        _progressBack.BackColor = _bg;
        _progressFill.BackColor = Accent;
        _lblCurrent.ForeColor = _muted;
        _lblNext.ForeColor = _muted;
        if (_pnlTrailColor is not null)
            _pnlTrailColor.BackColor = ActiveDisplayColor();
        RefreshCheckStates();
        NativeMethods.TryUseDarkTitleBar(Handle, _dark);
    }

    private Color ActiveDisplayColor()
    {
        if (_settings.ColorMode == 1 && !_detectedTrailColor.IsEmpty)
            return _detectedTrailColor;
        if (_settings.ColorMode == 2 && !string.IsNullOrEmpty(_settings.ManualColorHex))
            return ParseHex(_settings.ManualColorHex);
        return Color.Gray;
    }

    private static Color ParseHex(string hex)
    {
        try { return ColorTranslator.FromHtml(hex); }
        catch { return Color.Gray; }
    }

    private void ApplyThemeTo(Control.ControlCollection controls, Color bg, Color fg)
    {
        foreach (Control c in controls)
        {
            if (c == _statusCard || c == _pnlTrailColor) continue;
            switch (c)
            {
                case Button b:
                    bool primary = b == _btnToggle || b == _btnInstall;
                    b.BackColor = primary ? Accent : _card;
                    b.ForeColor = primary ? Color.White : fg;
                    b.FlatAppearance.BorderColor = Accent;
                    break;
                case GroupBox gb:
                    gb.ForeColor = fg;
                    ApplyThemeTo(gb.Controls, bg, fg);
                    break;
                case SplitContainer sc:
                    // Sin color propio (se fija aparte): tematizar ambos paneles.
                    ApplyThemeTo(sc.Panel1.Controls, bg, fg);
                    ApplyThemeTo(sc.Panel2.Controls, bg, fg);
                    break;
                case NumericUpDown nud:
                    nud.BackColor = _card; nud.ForeColor = fg;
                    break;
                case ComboBox cb:
                    cb.BackColor = _card; cb.ForeColor = fg;
                    break;
                case CheckBox chk:
                    chk.ForeColor = fg;
                    break;
                default:
                    c.BackColor = bg; c.ForeColor = fg;
                    if (c.HasChildren) ApplyThemeTo(c.Controls, bg, fg);
                    break;
            }
        }
    }

    // ---------- Lógica ----------
    /// <summary>Segundos por cambio (relativo). Mínimo 5 s anti-parpadeo.</summary>
    private double IntervalSeconds()
    {
        double mult = _cmbUnit.SelectedIndex == 2 ? 3600
            : _cmbUnit.SelectedIndex == 0 ? 1 : 60;
        return Math.Max(5, (double)_numInterval.Value * mult);
    }

    private bool IsFixedTime => _cmbUnit.SelectedIndex == 3;

    /// <summary>Rangos del numérico según la unidad + muestra hora o número.</summary>
    private void ApplyUnitUi()
    {
        bool fixed_ = IsFixedTime;
        _numInterval.Visible = !fixed_;
        _dtpTime.Visible = fixed_;
        _btnSchemePicker.Visible = fixed_;
        _btnAddTime.Visible = fixed_;
        _lstTimes.Visible = fixed_;
        _btnRemoveTime.Visible = fixed_;
        if (fixed_)
        {
            // La galería carga los esquemas al abrirse (siempre al día).
            return;
        }
        switch (_cmbUnit.SelectedIndex)
        {
            case 0: // Segundos (decimales vía teclado)
                _numInterval.Minimum = 5; _numInterval.Maximum = 86400; _numInterval.Increment = 5;
                break;
            case 2: // Horas
                _numInterval.Minimum = 1; _numInterval.Maximum = 24; _numInterval.Increment = 1;
                break;
            default: // Minutos (decimales: 0.5 = 30 s)
                _numInterval.Minimum = (decimal)0.5; _numInterval.Maximum = 1440; _numInterval.Increment = (decimal)0.5;
                break;
        }
    }

    /// <summary>Galería para elegir el destino de la hora (clic = elegir).</summary>
    private void OpenSchemePicker()
    {
        try
        {
            using var dlg = new SchemePickerDialog(
                SchemeStore.GetSchemeNames(), _pickedScheme, _settings, _dark);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedScheme is not null)
            {
                _pickedScheme = dlg.SelectedScheme;
                _btnSchemePicker.Text = string.IsNullOrEmpty(_pickedScheme)
                    ? "🎲 Aleatorio" : "➜ " + ShortScheme(_pickedScheme);
            }
        }
        catch (Exception ex) { Log("[Error] No se pudo abrir el selector: " + ex.Message); }
    }

    /// <summary>Horas fijas de la UI como TimeSpan, ordenadas y sin duplicados.</summary>
    private List<TimeSpan> FixedTimesFromUi()
    {
        var list = new List<TimeSpan>();
        if (_lstTimes is null) return list;
        foreach (var item in _lstTimes.Items)
        {
            TimeSpan t;
            if (item is ScheduledEntry e) t = e.TimeOfDay;
            else
            {
                try
                {
                    var parts = (item as string ?? "").Split(':');
                    t = new TimeSpan(int.Parse(parts[0]), int.Parse(parts[1]), 0);
                }
                catch { continue; }
            }
            if (!list.Contains(t)) list.Add(t);
        }
        list.Sort();
        return list;
    }

    /// <summary>Entradas de agenda de la UI (hora + destino).</summary>
    private List<ScheduledEntry> ScheduleFromUi()
    {
        var list = new List<ScheduledEntry>();
        if (_lstTimes is null) return list;
        foreach (var item in _lstTimes.Items)
        {
            if (item is ScheduledEntry e && !string.IsNullOrWhiteSpace(e.Time))
            {
                if (!list.Any(x => x.Time == e.Time))
                    list.Add(new ScheduledEntry { Time = e.Time, Scheme = e.Scheme ?? "" });
            }
        }
        return list.OrderBy(x => x.Time).ToList();
    }

    private void AddFixedTime()
    {
        try
        {
            string hhmm = _dtpTime.Value.ToString("HH:mm");
            string scheme = _pickedScheme ?? "";
            var entries = ScheduleFromUi();
            entries.RemoveAll(x => x.Time == hhmm);
            entries.Add(new ScheduledEntry { Time = hhmm, Scheme = scheme });
            RefreshTimeList(entries);
            OnIntervalChanged();
        }
        catch { }
    }

    private void RemoveFixedTime()
    {
        try
        {
            if (_lstTimes.SelectedItem is ScheduledEntry sel)
            {
                var entries = ScheduleFromUi();
                entries.RemoveAll(x => x.Time == sel.Time);
                RefreshTimeList(entries);
                OnIntervalChanged();
            }
        }
        catch { }
    }

    private void RefreshTimeList(List<ScheduledEntry> entries)
    {
        try
        {
            _lstTimes.Items.Clear();
            foreach (var e in entries.OrderBy(x => x.Time))
                _lstTimes.Items.Add(e);
        }
        catch { }
    }

    /// <summary>Próxima ocurrencia de la agenda (hora + destino o null).</summary>
    private static (DateTime At, string Scheme) NextScheduled(
        List<ScheduledEntry> entries, DateTime now)
    {
        var best = DateTime.MaxValue;
        string scheme = null;
        foreach (var e in entries)
        {
            TimeSpan t;
            try { t = e.TimeOfDay; }
            catch { continue; }
            var cand = now.Date + t;
            if (cand <= now) cand = cand.AddDays(1);
            if (cand < best)
            {
                best = cand;
                scheme = string.IsNullOrEmpty(e.Scheme) ? null : e.Scheme;
            }
        }
        return (best, scheme);
    }

    private static string DescribeFixedTimes(List<ScheduledEntry> entries) =>
        entries.Count == 0 ? "sin horas programadas"
            : "a las " + string.Join(", ", entries.OrderBy(x => x.Time)
                .Select(x => x.Time + (string.IsNullOrEmpty(x.Scheme)
                    ? "→🎲" : "→" + ShortScheme(x.Scheme))));

    private static string ShortScheme(string s) =>
        string.IsNullOrEmpty(s) ? s : (s.Length > 18 ? s[..18] + "…" : s);

    /// <summary>Calcula (primer disparo, periodo, descripción) del intervalo relativo.</summary>
    private (TimeSpan Due, TimeSpan Period, string Text) ComputeSchedule()
    {
        double v = (double)_numInterval.Value;
        double secs = IntervalSeconds();
        string text = _cmbUnit.SelectedIndex == 0 ? $"cada {v:0.#} s"
            : _cmbUnit.SelectedIndex == 2 ? $"cada {v:0.#} h" : $"cada {v:0.#} min";
        return (TimeSpan.Zero, TimeSpan.FromSeconds(secs), text);
    }

    /// <summary>
    /// Arranca la rotación según el programa de la UI (relativo o agenda de
    /// varias horas). Lo usan Activar y el cambio de pool.
    /// </summary>
    private void StartRotation(List<string> pool)
    {
        if (IsFixedTime)
        {
            var entries = ScheduleFromUi();
            if (entries.Count == 0)
            {
                Log("Añade al menos una hora (p. ej. 11:00) para programar.");
                return;
            }
            var snap = entries.Select(x =>
                new ScheduledEntry { Time = x.Time, Scheme = x.Scheme }).ToList();
            _rotator.Start(pool, (DateTime now) => NextScheduled(snap, now));
            Log($"Rotación activa {DescribeFixedTimes(snap)} ({pool.Count} esquemas).");
        }
        else
        {
            var (due, period, text) = ComputeSchedule();
            _rotator.Start(pool, due, period);
            Log($"Rotación activa {text} ({pool.Count} esquemas).");
        }
        try
        {
            ReadIntervalToSettings();
            _settings.Save();
        }
        catch { }
    }

    /// <summary>Vuelca intervalo/hora a _settings (sin guardar).</summary>
    private void ReadIntervalToSettings()
    {
        _settings.IntervalMode = IsFixedTime ? 1 : 0;
        if (_settings.IntervalMode == 0)
        {
            _settings.IntervalSeconds = IntervalSeconds();
            _settings.IntervalMinutes = Math.Max(1, (int)Math.Round(_settings.IntervalSeconds / 60));
        }
        else
        {
            var entries = ScheduleFromUi();
            _settings.Schedule = entries;
            if (entries.Count > 0)
            {
                _settings.ScheduledTimes = entries.Select(x => x.Time).ToList();
                _settings.ScheduledTime = entries[0].Time;
            }
        }
    }

    private void ToggleRotation()
    {
        if (_rotator.IsRunning)
        {
            _rotator.Stop();
            Log("Rotación pausada. Puedes cambiar el cursor a mano.");
        }
        else
        {
            try
            {
                var pool = GetRotationPool();
                StartRotation(pool);
            }
            catch (Exception ex) { Log("No se pudo activar: " + ex.Message); return; }
        }
        UpdateStatus();
        _tray?.SyncToggleText(_rotator.IsRunning);
        RefreshCursorPreview();
    }

    private void OnIntervalChanged()
    {
        // El intervalo se persiste SIEMPRE (corra o no la rotación),
        // para que nunca se pierda al cerrar.
        try
        {
            ReadIntervalToSettings();
            _settings.Save();
        }
        catch { }
        RestartIfRunning();
    }

    private void RestartIfRunning()
    {
        if (!_rotator.IsRunning) return;
        _rotator.Stop();
        ToggleRotation();
    }

    /// <summary>
    /// "Cambiar ahora": aplica EXACTAMENTE el esquema que la vista previa
    /// muestra como "Siguiente" (nada aleatorio). Si la rotación corre, lo
    /// extrae de la cola (la rotación sigue con el posterior); si está
    /// parada, usa el mismo cálculo de la vista previa.
    /// </summary>
    private void ApplyNext()
    {
        try
        {
            string pick = null;
            if (_rotator.IsRunning)
                pick = _rotator.TakeNext();
            if (string.IsNullOrEmpty(pick))
            {
                string active = _lastAppliedScheme ?? _rotator.CurrentScheme
                    ?? FindSchemeByArrow(CursorColorReader.GetActiveArrowPath());
                var pool = GetRotationPool();
                if (pool.Count == 0) { Log("No hay esquemas instalados."); return; }
                pick = pool.FirstOrDefault(s => s != active) ?? active;
            }
            SchemeStore.ApplyScheme(pick);
            OnSchemeApplied(pick);
        }
        catch (Exception ex) { Log("[Error] " + ex.Message); }
    }

    private void ApplySelected(string name)
    {
        try
        {
            SchemeStore.ApplyScheme(name);
            OnSchemeApplied(name);
        }
        catch (Exception ex) { Log("[Error] " + ex.Message); }
    }

    private void OnSchemeApplied(string name)
    {
        _lastAppliedScheme = name;
        _lblCurrent.Text = "Esquema actual: " + name;
        Log("Cursor activo: " + name);
        if (_trail is not null && _settings.ColorMode == 1)
            RefreshTrailOverlay();
        UpdateStatus();
        RefreshCursorPreview();
    }

    /// <summary>
    /// Vista previa activo/próximo: extrae el puntero Arrow (.cur/.ani) de
    /// cada esquema con el parser propio (sin bloquear ni instalar nada) y
    /// lo muestra en 40×40 con ToolTip del nombre exacto del cursor.
    /// </summary>
    private void RefreshCursorPreview()
    {
        try
        {
            // Fuente de verdad: lo último aplicado (manual o rotación),
            // no el estado interno del rotador (que ignora los manuales).
            string active = _lastAppliedScheme ?? _rotator.CurrentScheme;
            string next = _rotator.IsRunning ? _rotator.PeekNext() : null;
            if (_rotator.IsRunning && _settings.IntervalMode == 1)
            {
                // En agenda, "Siguiente" es el destino de la próxima hora.
                try
                {
                    var (_, schedScheme) = NextScheduled(ScheduleFromUi(), DateTime.Now);
                    if (!string.IsNullOrEmpty(schedScheme)) next = schedScheme;
                }
                catch { }
            }
            if (string.IsNullOrEmpty(active))
                active = FindSchemeByArrow(CursorColorReader.GetActiveArrowPath());
            if (string.IsNullOrEmpty(next) && !string.IsNullOrEmpty(active))
            {
                var pool = GetRotationPool();
                next = pool.FirstOrDefault(s => s != active) ?? active;
            }
            SetPreview(_picCursorActivo, _lblCapActivo, active, "Actual", "Actual");
            SetPreview(_picCursorProximo, _lblCapProximo, next, "Siguiente", "Siguiente");
        }
        catch { /* la vista previa jamás debe tumbar la UI */ }
    }

    private void SetPreview(PictureBox pic, Label cap, string scheme, string role, string fallback)
    {
        if (pic is null || pic.IsDisposed) return;
        try
        {
            Bitmap bmp = null;
            string tip = fallback;
            if (!string.IsNullOrEmpty(scheme))
            {
                string arrow = GetSchemeArrowPath(scheme);
                if (!string.IsNullOrEmpty(arrow))
                {
                    bmp = CursorPreviewCache.Get(arrow);
                    tip = $"{role}: {scheme} — {Path.GetFileName(arrow)} (clic: ver roles)";
                }
                else tip = $"{role}: {scheme}";
            }
            var old = pic.Image;
            pic.Image = bmp;
            pic.Tag = scheme;
            old?.Dispose();
            if (cap is not null && !cap.IsDisposed)
                cap.Text = role;
            Tip(pic, tip);
        }
        catch { }
    }

    /// <summary>Ruta del rol Arrow (índice 0) ya sanitizada, o null.</summary>
    private static string GetSchemeArrowPath(string scheme)
    {
        try
        {
            var paths = SchemeStore.GetResolvedSchemePaths(scheme);
            return paths.Length > 0 ? paths[0] : null;
        }
        catch { return null; }
    }

    private static string FindSchemeByArrow(string arrowPath)
    {
        if (string.IsNullOrEmpty(arrowPath)) return null;
        try
        {
            string norm = SchemeStore.ResolveCursorPath(arrowPath);
            foreach (var name in SchemeStore.GetSchemeNames())
            {
                var paths = SchemeStore.GetResolvedSchemePaths(name);
                if (paths.Length > 0 && string.Equals(paths[0], norm,
                    StringComparison.OrdinalIgnoreCase))
                    return name;
            }
        }
        catch { }
        return null;
    }

    private void UpdateStatus()
    {
        bool on = _rotator.IsRunning;
        _statusDot.BackColor = on ? OkGreen : WarnAmber;
        _lblStatus.Text = on ? "● Rotación activa" : "● Pausado";
        _lblStatus.ForeColor = on ? OkGreen : WarnAmber;
        _btnToggle.Text = on ? "⏸  Pausar rotación" : "▶  Activar rotación";
        UpdateCountdown();
    }

    private void UpdateCountdown()
    {
        if (_rotator.IsRunning && _rotator.NextChangeAt is DateTimeOffset next)
        {
            var left = next - DateTimeOffset.Now;
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            _lblNext.Text = left.TotalHours >= 1
                ? $"Próximo cambio: en {(int)left.TotalHours:D2}:{left.Minutes:D2}:{left.Seconds:D2}"
                : $"Próximo cambio: en {left.Minutes:D2}:{left.Seconds:D2}";
        }
        else _lblNext.Text = "Próximo cambio: —";
    }

    private void LoadSettingsToUi()
    {
        _suspendDirty = true;
        try
        {
            int mins = Math.Clamp(_settings.IntervalMinutes, 1, 1440);
        if (_settings.IntervalMode == 1)
        {
            // Horas fijas diarias (hora + destino).
            _cmbUnit.SelectedIndex = 3;
            ApplyUnitUi();
            var saved = _settings.Schedule;
            if (saved is null || saved.Count == 0)
            {
                var legacy = _settings.ScheduledTimes;
                if (legacy is null || legacy.Count == 0)
                    legacy = new List<string> { _settings.ScheduledTime ?? "09:00" };
                saved = legacy.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct()
                    .Select(t => new ScheduledEntry { Time = t.Trim() }).ToList();
            }
            RefreshTimeList(saved);
            try
            {
                var parts = (_settings.ScheduledTime ?? "09:00").Split(':');
                _dtpTime.Value = DateTime.Today.AddHours(int.Parse(parts[0]))
                    .AddMinutes(parts.Length > 1 ? int.Parse(parts[1]) : 0);
            }
            catch { _dtpTime.Value = DateTime.Today.AddHours(9); }
        }
        else if (_settings.IntervalSeconds >= 3600 && _settings.IntervalSeconds % 3600 == 0)
        {
            _cmbUnit.SelectedIndex = 2;
            ApplyUnitUi();
            _numInterval.Value = (decimal)(_settings.IntervalSeconds / 3600);
        }
        else if (_settings.IntervalSeconds >= 60 && _settings.IntervalSeconds % 60 == 0)
        {
            _cmbUnit.SelectedIndex = 1;
            ApplyUnitUi();
            _numInterval.Value = (decimal)(_settings.IntervalSeconds / 60);
        }
        else if (_settings.IntervalSeconds > 0)
        {
            _cmbUnit.SelectedIndex = 0;
            ApplyUnitUi();
            _numInterval.Value = (decimal)Math.Clamp(_settings.IntervalSeconds, 5, 86400);
        }
        else if (mins >= 60 && mins % 60 == 0)
        {
            _cmbUnit.SelectedIndex = 2;
            ApplyUnitUi();
            _numInterval.Value = Math.Min(mins / 60, 24);
        }
        else
        {
            _cmbUnit.SelectedIndex = 1;
            ApplyUnitUi();
            _numInterval.Value = (decimal)Math.Min(mins, 1440);
        }
        _cmbTrailEngine.SelectedIndex = Math.Clamp(_settings.TrailEngineMode, 0, 6);
        _cmbTrailOrigin.SelectedIndex = Math.Clamp(_settings.TrailOrigin, 0, 1);
        _trkTrailSize.Value = Math.Clamp(_settings.TrailSize, 5, 30);
        _trkTrailLife.Value = Math.Clamp(_settings.TrailLife, 3, 100);
        _trkTrailDensity.Value = Math.Clamp(_settings.TrailDensity, 1, 30);
        _rbColorNative.Checked = _settings.ColorMode == 0;
        _rbColorInherited.Checked = _settings.ColorMode == 1;
        _rbColorManual.Checked = _settings.ColorMode == 2;
        _chkClicks.Checked = _settings.ClickEnabled;
        _chkClickColor.Checked = _settings.ClickInheritColor;
        _trkClickSize.Value = Math.Clamp(_settings.ClickSize, 5, 30);
        _chkSecondaryTrail.Checked = _settings.SecondaryTrailEnabled;
        _lblSecondaryWarn.Visible = _settings.SecondaryTrailEnabled;
        _btnSecondaryGallery.Enabled = _settings.SecondaryTrailEnabled;
        _lblClickSize.Text = $"{_trkClickSize.Value / 10f:0.0}×";
        UpdateGalleryButton();
        ApplyTrailOptions();
        if (_settings.TrailEnabled)
            _chkTrail.Checked = true; // dispara SetTrailEnabled
        }
        finally { _suspendDirty = false; }
    }

    // ---------- Estelas ----------
    public void SetTrailEnabled(bool on)
    {
        _settings.TrailEnabled = on;
        _settings.Save();
        if (on)
        {
            try
            {
                _trail ??= new TrailOverlay();
                ApplyTrailOptions();
                if (!_trail.Visible) _trail.Show();
                Log("Estelas activadas.");
            }
            catch (Exception ex) { Log("No se pudo activar la estela: " + ex.Message); }
        }
        else
        {
            try { _trail?.HideOverlay(); } catch { }
            _trail?.Dispose();
            _trail = null;
            Log("Estelas desactivadas.");
        }
    }

    /// <summary>
    /// Re-crea el overlay de estelas (hilo, hook, bitmap limpios). Útil si la
    /// estela se queda pegada tras suspender o cambiar de GPU. Los ajustes ya
    /// están guardados: todo aplica al instante, este botón solo reinicia.
    /// </summary>
    private void RestartTrailOverlay()
    {
        try
        {
            if (!_chkTrail.Checked)
            {
                _chkTrail.Checked = true; // dispara SetTrailEnabled → Apply + Show
                Log("Estelas encendidas para mostrar los cambios.");
                return;
            }
            ApplyTrailOptions();
            if (_trail is not null)
            {
                // Re-crear el overlay garantiza estado limpio (hilo, hook, bitmap).
                try { _trail.HideOverlay(); } catch { }
                _trail.Dispose();
                _trail = null;
                _trail = new TrailOverlay();
                ApplyTrailOptions();
                _trail.Show();
            }
            Log("↻ Estela reiniciada.");
        }
        catch (Exception ex)
        {
            Log("[Error] No se pudo reiniciar la estela: " + ex.Message);
        }
    }

    /// <summary>
    /// Aplicación en vivo (estilo YoloMouse): cada cambio del panel se guarda
    /// y se empuja al motor al instante. Cerrar sin más nunca pierde nada.
    /// </summary>
    private void OnSettingsChanged(object sender, EventArgs e)
    {
        if (_suspendDirty) return;
        ApplyTrailOptions();
    }

    private void ApplyTrailOptions()
    {
        ReadTrailUiToSettings();
        // ClickEffect solo cambia vía galería (ya guardado al elegir).
        _settings.Save();
        _lblTrailSize.Text = $"{_trkTrailSize.Value / 10f:0.0}×";
        _lblTrailLife.Text = $"{_trkTrailLife.Value / 10f:0.0}×";
        _lblTrailDensity.Text = $"{_trkTrailDensity.Value / 10f:0.0}×";
        _lblClickSize.Text = $"{_trkClickSize.Value / 10f:0.0}×";
        _btnManualColor.Enabled = _rbColorManual.Checked;
        RefreshTrailOverlay(_settings.ColorMode != 0);
    }

    /// <summary>
    /// Vuelca TODA la UI a settings (sin tocar el motor): intervalo, panel
    /// de estelas y geometría. Red de seguridad para que al cerrar nunca se
    /// pierda nada, ni siquiera cambios pendientes de "Aplicar cambios".
    /// </summary>
    private void FlushSettings()
    {
        try
        {
            ReadIntervalToSettings();
            ReadTrailUiToSettings();
            if (WindowState == FormWindowState.Normal)
            {
                _settings.WindowX = Location.X;
                _settings.WindowY = Location.Y;
                _settings.WindowW = Width;
                _settings.WindowH = Height;
            }
            _settings.Save();
        }
        catch { /* guardar jamás debe tumbar el cierre */ }
    }

    /// <summary>Lee los controles del panel de estelas a _settings (sin guardar).</summary>
    private void ReadTrailUiToSettings()
    {
        _settings.TrailEngineMode = Math.Max(0, _cmbTrailEngine.SelectedIndex);
        _settings.TrailOrigin = Math.Max(0, _cmbTrailOrigin.SelectedIndex);
        _settings.TrailSize = _trkTrailSize.Value;
        _settings.TrailLife = _trkTrailLife.Value;
        _settings.TrailDensity = _trkTrailDensity.Value;
        // Tríada mutuamente excluyente (RadioButtons): 0 nativo, 1 heredado, 2 manual.
        _settings.ColorMode = _rbColorInherited.Checked ? 1
            : _rbColorManual.Checked ? 2 : 0;
        _settings.TrailUseColor = _settings.ColorMode == 1; // compatibilidad
        _settings.ClickEnabled = _chkClicks.Checked;
        _settings.ClickInheritColor = _chkClickColor.Checked;
        _settings.ClickSize = _trkClickSize.Value;
        _settings.SecondaryTrailEnabled = _chkSecondaryTrail.Checked;
        _settings.SecondaryTrailPreset = Math.Clamp(_settings.SecondaryTrailPreset, 0, 39);
    }

    /// <summary>
    /// Restaura tamaño/posición guardados (validados contra el área visible).
    /// </summary>
    private void RestoreWindowGeometry()
    {
        try
        {
            int w = _settings.WindowW, h = _settings.WindowH;
            if (w < MinimumSize.Width || h < MinimumSize.Height) return;
            var area = Screen.FromControl(this).WorkingArea;
            w = Math.Min(w, area.Width);
            h = Math.Min(h, area.Height);
            int x = Math.Clamp(_settings.WindowX, area.Left, Math.Max(area.Left, area.Right - 120));
            int y = Math.Clamp(_settings.WindowY, area.Top, Math.Max(area.Top, area.Bottom - 120));
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(x, y, w, h);
        }
        catch { /* geometría corrupta: se ignora */ }
    }

    /// <summary>Empuja ajustes + paleta + motor + topmost + pacing + clics al overlay.</summary>
    public void RefreshTrailOverlay(bool announceColor = false)
    {
        _detectedTrailColor = Color.Empty;
        Color[] palette = null;
        if (_settings.ColorMode == 1)
        {
            string arrow = CursorColorReader.GetActiveArrowPath();
            if (!string.IsNullOrEmpty(arrow))
                palette = CursorColorReader.GetPalette(arrow);
            if (palette.Length > 0)
            {
                _detectedTrailColor = palette[0];
                if (announceColor)
                    Log($"Paleta del cursor: {string.Join(" ", palette.Select(c => $"#{c.R:X2}{c.G:X2}{c.B:X2}"))}");
            }
            else if (announceColor)
                Log("No se pudo leer el color del cursor activo (se usa paleta del preset).");
        }
        else if (_settings.ColorMode == 2 && !string.IsNullOrEmpty(_settings.ManualColorHex))
        {
            Color manual = ParseHex(_settings.ManualColorHex);
            palette = CursorColorReader.DerivePalette(manual);
            _detectedTrailColor = manual;
            if (announceColor)
                Log($"Color manual: {_settings.ManualColorHex}");
        }
        if (_pnlTrailColor is not null)
            _pnlTrailColor.BackColor = ActiveDisplayColor();
        UpdateGalleryButton();
        if (_trail is null) return;
        _trail.Options.Preset = (TrailPreset)Math.Clamp(_settings.TrailPreset, 0, 39);
        _trail.Options.Origin = (TrailOrigin)Math.Clamp(_settings.TrailOrigin, 0, 1);
        _trail.Options.SizeScale = Math.Clamp(_settings.TrailSize, 5, 30) / 10f;
        _trail.Options.LifeScale = Math.Clamp(_settings.TrailLife, 3, 100) / 10f;
        _trail.Options.Density = Math.Clamp(_settings.TrailDensity, 1, 30) / 10f;
        _trail.Options.UseCursorColor = palette is { Length: > 0 };
        _trail.Options.CursorColor = _detectedTrailColor;
        _trail.Options.CustomPalette = palette;
        _trail.CaptureMode = (TrailCaptureMode)Math.Clamp(_settings.TrailEngineMode, 0, 6);
        _trail.ForceTopmost = _settings.ForceTopmost;
        _trail.RefreshCapHz = _settings.RefreshCapHz;
        _trail.Clicks.Enabled = _settings.ClickEnabled;
        _trail.Clicks.SelectedIndex = Math.Clamp(_settings.ClickEffect, 0, ClickEffectFactory.Names.Length - 1);
        _trail.ClickSizeScale = Math.Clamp(_settings.ClickSize, 5, 30) / 10f;
        _trail.ClickUseTrailPalette = _settings.ClickInheritColor;
        // Dual trail: preset independiente + parámetros globales heredados.
        _trail.SecondaryEnabled = _settings.SecondaryTrailEnabled;
        _trail.SecondaryPreset = (TrailPreset)Math.Clamp(_settings.SecondaryTrailPreset, 0, 39);
        _trail.SyncSecondaryFromPrimary();
    }

    private void UpdateGalleryButton()
    {
        if (_btnGallery is null) return;
        _btnGallery.Text = "✨ Estela Continua: " + TrailCatalog.LabelOf(
            (TrailPreset)Math.Clamp(_settings.TrailPreset, 0, 39));
        if (_btnClickGallery is null) return;
        _btnClickGallery.Text = "💥 Efecto al Hacer Clic: " + ClickEffectFactory.Names[
            Math.Clamp(_settings.ClickEffect, 0, ClickEffectFactory.Names.Length - 1)];
        if (_btnSecondaryGallery is null) return;
        _btnSecondaryGallery.Text = "✨ Estela Secundaria: " + (_settings.SecondaryTrailEnabled
            ? TrailCatalog.LabelOf((TrailPreset)Math.Clamp(_settings.SecondaryTrailPreset, 0, 39))
            : "[Ninguna]");
    }

    private void OpenGallery()
    {
        using var dlg = new TrailGalleryDialog(
            (TrailPreset)Math.Clamp(_settings.TrailPreset, 0, 39), _dark);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _settings.TrailPreset = (int)dlg.Selected;
            _settings.Save();
            RefreshTrailOverlay();
        }
    }

    /// <summary>
    /// Toggle de la estela secundaria: muestra el aviso de CPU/GPU, habilita
    /// el selector y aplica al instante (persistido en settings.json).
    /// </summary>
    private void OnSecondaryToggled()
    {
        bool on = _chkSecondaryTrail.Checked;
        _settings.SecondaryTrailEnabled = on;
        _settings.Save();
        _lblSecondaryWarn.Visible = on;
        _btnSecondaryGallery.Enabled = on;
        UpdateGalleryButton();
        if (!_suspendDirty)
            ApplyTrailOptions();
        if (on)
            Log("Estela secundaria activada (dual trail).");
    }

    private void OpenSecondaryGallery()
    {
        using var dlg = new TrailGalleryDialog(
            (TrailPreset)Math.Clamp(_settings.SecondaryTrailPreset, 0, 39), _dark);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _settings.SecondaryTrailPreset = (int)dlg.Selected;
            _settings.Save();
            UpdateGalleryButton();
            ApplyTrailOptions();
        }
    }

    private void OpenClickGallery()
    {
        using var dlg = new ClickGalleryDialog(
            Math.Clamp(_settings.ClickEffect, 0, ClickEffectFactory.Names.Length - 1), _dark);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _settings.ClickEffect = dlg.Selected;
            _settings.Save();
            UpdateGalleryButton();
            ApplyTrailOptions(); // en vivo: guarda y empuja al motor al instante
        }
    }

    private void PickManualColor()
    {
        using var dlg = new ColorDialog { FullOpen = true };
        if (!string.IsNullOrEmpty(_settings.ManualColorHex))
        {
            try { dlg.Color = ParseHex(_settings.ManualColorHex); } catch { }
        }
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _settings.ManualColorHex =
            $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
        _settings.Save();
        // Exclusión mutua: elegir manual activa el modo manual.
        if (!_rbColorManual.Checked)
            _rbColorManual.Checked = true; // dispara ApplyTrailOptions
        else
            RefreshTrailOverlay(announceColor: true);
    }

    /// <summary>
    /// Rejilla por roles del esquema (clic en la vista previa): lupa estilo
    /// CursorFX/YoloMouse con los 15 roles y sus archivos exactos.
    /// </summary>
    private void OpenSchemeRoles(string scheme)
    {
        if (string.IsNullOrEmpty(scheme)) return;
        using var dlg = new SchemePreviewDialog(scheme, _dark);
        dlg.ShowDialog(this);
    }

    private void OpenSchemesDialog()
    {        if (_schemesDialog is not null && !_schemesDialog.IsDisposed)
        {
            _schemesDialog.Activate();
            return;
        }
        _schemesDialog = new SchemesDialog(CursorInstaller.DefaultStoreDir, _settings, _dark);
        _schemesDialog.Applied += OnSchemeApplied;
        _schemesDialog.RotationPoolChanged += OnRotationPoolChanged;
        _schemesDialog.ShowDialog(this);
        _schemesDialog.Applied -= OnSchemeApplied;
        _schemesDialog.RotationPoolChanged -= OnRotationPoolChanged;
        _schemesDialog = null;
    }

    /// <summary>Pool de rotación: registrados menos los desmarcados.</summary>
    private List<string> GetRotationPool()
    {
        var excluded = _settings.ExcludedFromRotation;
        return SchemeStore.GetSchemeNames()
            .Where(s => !excluded.Contains(s)).ToList();
    }

    private void OnRotationPoolChanged()
    {
        var pool = GetRotationPool();
        Log($"Pool de rotación: {pool.Count} esquemas.");
        if (_rotator.IsRunning)
        {
            // Reinicia para aplicar el pool al instante.
            _rotator.Stop();
            if (pool.Count > 0)
            {
                try
                {
                    StartRotation(pool);
                    Log("Rotación reiniciada con el nuevo pool.");
                }
                catch (Exception ex) { Log("[Error] " + ex.Message); UpdateStatus(); }
            }
            else
            {
                Log("Sin esquemas marcados: rotación pausada.");
                UpdateStatus();
            }
        }
    }

    private void OpenLogDialog()
    {
        if (_logDialog is not null && !_logDialog.IsDisposed)
        {
            _logDialog.Activate();
            return;
        }
        _logDialog = new LogDialog(_dark);
        _logDialog.ShowDialog(this);
        _logDialog = null;
    }

    private void OpenPrefsDialog()
    {
        using var dlg = new PrefsDialog(_settings, _dark);
        dlg.RelaunchTutorial += () =>
            TutorialForm.ShowBeside(this, AppSettings.Load(), _dark);
        dlg.CheckUpdatesNow += async () => await CheckForUpdatesAsync(silent: false);
        dlg.ShowDialog(this);
        if (_tip is not null)
            _tip.Active = _settings.ShowTooltips;
        RefreshTrailOverlay(); // por si cambió topmost o pacing
    }

    /// <summary>
    /// Comprueba el último release de GitHub y ofrece descargarlo.
    /// Silencioso: solo habla si hay novedad (ni errores ni "al día").
    /// </summary>
    private async Task CheckForUpdatesAsync(bool silent)
    {
        try
        {
            string repo = (_settings.UpdateRepo ?? "").Trim();
            if (repo.Length == 0)
            {
                if (!silent)
                    MessageBox.Show("Configura el repositorio en Opciones Avanzadas " +
                        "(formato 'dueño/CursorRotator').", "Actualizaciones",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var info = await UpdateChecker.CheckAsync(repo);
            if (!info.HasUpdate || info.Tag == _settings.SkippedVersion)
            {
                if (!silent)
                    MessageBox.Show($"Estás al día (versión {UpdateChecker.CurrentVersion}).",
                        "Actualizaciones", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var btnDl = new TaskDialogCommandLinkButton(
                "&Descargar e instalar",
                "Descarga el paquete y actualiza la app sola");
            var btnSkip = new TaskDialogButton("&Omitir esta versión");
            var btnClose = new TaskDialogButton("Cerrar");
            var page = new TaskDialogPage
            {
                Caption = "Actualización disponible",
                Heading = $"¡Hay una nueva versión disponible ({info.Tag})! " +
                          "¿Deseas descargarla e instalarla automáticamente ahora?",
                Text = info.Notes,
                Icon = TaskDialogIcon.Information,
                Buttons = { btnDl, btnSkip, btnClose },
            };
            var clicked = TaskDialog.ShowDialog(this, page);
            if (clicked == btnDl)
                await DownloadUpdateAsync(info);
            else if (clicked == btnSkip)
            {
                _settings.SkippedVersion = info.Tag;
                _settings.Save();
            }
        }
        catch (Exception ex)
        {
            if (silent) Log("Actualizaciones: " + ex.Message);
            else MessageBox.Show("No se pudo comprobar: " + ex.Message,
                "Actualizaciones", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task DownloadUpdateAsync(UpdateInfo info)
    {
        try
        {
            if (string.IsNullOrEmpty(info.DownloadUrl))
            {
                if (!string.IsNullOrEmpty(info.PageUrl))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = info.PageUrl,
                        UseShellExecute = true,
                    });
                return;
            }
            var (zipPath, extractDir, batPath) = AutoUpdater.PrepareStaging();
            DialogResult done;
            using (var dlg = new UpdateProgressForm(info.DownloadUrl, zipPath, extractDir, _dark))
                done = dlg.ShowDialog(this);
            if (done != DialogResult.OK) return; // cancelado o fallido (ya avisado)
            string appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            Log($"Instalando {info.Tag}…");
            AutoUpdater.GenerateUpdateScript(extractDir, appDir, batPath, zipPath);
            AutoUpdater.LaunchUpdate(batPath);
            Log("Reiniciando para aplicar la actualización…");
            await Task.Delay(300);
            Quit();
        }
        catch (Exception ex)
        {
            Log("[Error] Actualización: " + ex.Message);
            MessageBox.Show("No se pudo actualizar: " + ex.Message,
                "Actualización", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>Aviso automático al abrir (máx. 1 vez al día, silencioso).</summary>
    private async Task AutoUpdateCheckAsync()
    {
        try
        {
            if (!_settings.CheckUpdates) return;
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (_settings.LastUpdateCheck == today) return;
            _settings.LastUpdateCheck = today;
            _settings.Save();
            await CheckForUpdatesAsync(silent: true);
        }
        catch { }
    }

    /// <summary>Progreso temático (el ProgressBar nativo no admite dark mode).</summary>
    private void SetProgress(int percent)
    {
        if (_progressFill is null || _progressFill.IsDisposed) return;
        if (_progressFill.InvokeRequired)
        {
            try { _progressFill.BeginInvoke(() => SetProgress(percent)); } catch { }
            return;
        }
        int w = _progressBack.ClientSize.Width;
        _progressFill.Width = Math.Clamp(w * percent / 100, 0, w);
    }

    private async Task RunInstallAsync()
    {
        string source = CursorInstaller.AutoDetectSource();
        if (source is null)
        {
            using var dlg = new FolderBrowserDialog { Description = "Elige la carpeta con los cursores (.zip/.rar o carpetas extraídas)" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            source = dlg.SelectedPath;
        }
        _btnInstall.Enabled = false;
        SetProgress(0);
        Log("Instalando desde: " + source);
        var progress = new Progress<(int Percent, string Message)>(t =>
        {
            SetProgress(Math.Clamp(t.Percent, 0, 100));
            Log(t.Message);
        });
        var installer = new CursorInstaller();
        bool skipAllDup = false;
        List<CursorInstaller.InstallCandidate> AskVariants(List<CursorInstaller.InstallCandidate> candidates)
        {
            if (InvokeRequired)
                return (List<CursorInstaller.InstallCandidate>)Invoke(new Func<List<CursorInstaller.InstallCandidate>>(() => AskVariants(candidates)));
            using var dlg = new VariantSelectDialog(candidates, _dark);
            return dlg.ShowDialog(this) == DialogResult.OK ? dlg.Selected : null;
        }
        CursorInstaller.DuplicateAction AskDuplicate(string name)
        {
            if (InvokeRequired)
                return (CursorInstaller.DuplicateAction)Invoke(new Func<CursorInstaller.DuplicateAction>(() => AskDuplicate(name)));
            if (skipAllDup) return CursorInstaller.DuplicateAction.Skip;
            using var dlg = new DuplicateConfirmDialog(name, _dark);
            dlg.ShowDialog(this);
            if (dlg.Result == CursorInstaller.DuplicateAction.SkipAll) skipAllDup = true;
            return dlg.Result;
        }
        var result = await installer.InstallAsync(source, CursorInstaller.DefaultStoreDir,
            progress, CancellationToken.None, skipStatic: !_settings.IncludeStatic,
            selectVariantsAsync: c => Task.FromResult(AskVariants(c)),
            confirmDuplicateAsync: n => Task.FromResult(AskDuplicate(n)));
        SetProgress(100);
        Log($"Listo: {result.Installed.Count} esquemas, {result.Errors.Count} errores, {result.Skipped.Count} omitidos.");
        foreach (var s in result.Skipped) Log("  ○ " + s);
        foreach (var e in result.Errors) Log("  ⚠ " + e);
        _btnInstall.Enabled = true;
    }

    private void Log(string message) => LogStore.Append(message);

    // ---------- Bandeja ----------
    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        _tray.ShowBalloon("CursorRotator sigue en la bandeja 🖱️");
    }

    /// <summary>Trae la ventana al frente (la usa la 2ª instancia vía señal).</summary>
    public void ShowMainWindow()
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(ShowMainWindow); } catch { }
            return;
        }
        ShowFromTray();
    }

    private void ShowFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void Quit()
    {
        if (_exiting) return;
        _exiting = true;
        FlushSettings();
        try { _trail?.Dispose(); } catch { }
        _trail = null;
        _tray.Dispose();
        _rotator.Dispose();
        Application.Exit();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        FlushSettings();
        // Salida real (menú Salir / apagado / End Task): no interceptar nunca.
        // Solo la X y Alt+F4 minimizan a bandeja.
        if (_settings.MinimizeToTrayOnClose && !_exiting
            && e.CloseReason != CloseReason.WindowsShutDown
            && e.CloseReason != CloseReason.TaskManagerClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        _rotator.Stop();
        try { _trail?.Dispose(); } catch { }
        _trail = null;
        _tray.Dispose();
        base.OnFormClosing(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
            HideToTray();
    }
}

