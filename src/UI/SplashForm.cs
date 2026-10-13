using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Reflection;

namespace CursorRotator.UI;

/// <summary>
/// Pantalla de carga profesional 800×450 sobre la imagen pre-renderizada
/// del proyecto. Los textos de versión/estado y la barra se inyectan como
/// controles nativos dinámicos (overlay UI), no quemados en la imagen.
/// Ciclo de vida: se muestra ANTES del Form principal; un Timer interno
/// anima el progreso mientras corre la inicialización real. Al llegar al
/// 100% se cierra con <see cref="DialogResult.OK"/> y Program.cs continúa
/// con <c>Application.Run(new MainForm(...))</c>.
/// </summary>
public sealed class SplashForm : Form
{
    private readonly Label _lblVersion;
    private readonly Label _lblStatus;
    private readonly GradientBar _bar;
    private readonly System.Windows.Forms.Timer _timer;
    private int _displayProgress;
    private int _targetProgress = 10;
    private readonly string[] _steps =
    [
        "Inicializando motores de estelas...",
        "Cargando presets de partículas...",
        "Buscando esquemas de cursores registrados...",
        "Analizando cursor activo...",
        "Listo...",
    ];
    private int _stepIndex;

    /// <summary>
    /// Trabajo real de arranque (lee ajustes + esquemas + primer .ani).
    /// Recibe Report(percent, status) thread-safe. Lo asigna Program.cs.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<Action<int, string>, Task> BootTask { get; set; }

    public SplashForm()
    {
        // 1. Configuración base: sin bordes, centrada, 800×450.
        Text = "CursorRotator";
        Size = new Size(800, 450);
        MinimumSize = Size;
        MaximumSize = Size;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        Font = new Font("Segoe UI", 9f);
        BackColor = Color.FromArgb(0x0B, 0x0B, 0x18);

        // Imagen de fondo (recurso embebido ..\assets\splash.jpg).
        BackgroundImage = LoadSplashImage();
        BackgroundImageLayout = ImageLayout.Stretch;

        // 2a. Versión (esquina superior derecha, transparente).
        string ver = GetAppVersion();
        _lblVersion = new Label
        {
            Text = $"VERSIÓN {ver} (64-bit)\n2026 SrDragónDoro Community",
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            TextAlign = ContentAlignment.TopRight,
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.WhiteSmoke,
            BackColor = Color.Transparent,
        };
        // Sombra sutil para legibilidad sobre la imagen (se pinta bajo el texto).
        _lblVersion.Paint += (_, e) =>
        {
            e.Graphics.DrawString(_lblVersion.Text, _lblVersion.Font,
                Brushes.Black, new PointF(1, 1));
        };
        Controls.Add(_lblVersion);

        // 2c. Registro de inicialización (esquina inferior izquierda).
        _lblStatus = new Label
        {
            Text = _steps[0],
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(150, 230, 255),
            BackColor = Color.Transparent,
        };
        Controls.Add(_lblStatus);

        // 2b. Barra de progreso custom (GDI+, degradado cian→púrpura).
        _bar = new GradientBar
        {
            Height = 16,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };
        Controls.Add(_bar);

        Shown += async (_, _) =>
        {
            LayoutOverlay();
            _timer.Start();
            try
            {
                if (BootTask is not null)
                    await BootTask(Report).ConfigureAwait(true);
                else
                    await Task.Delay(1200).ConfigureAwait(true);
            }
            catch { /* el splash jamás debe tumbar el arranque */ }
            Finish();
        };
        Resize += (_, _) => LayoutOverlay();

        // Timer de animación: suaviza la barra hacia el objetivo y rota
        // los mensajes si la inicialización real tarda más de lo previsto.
        _timer = new System.Windows.Forms.Timer { Interval = 40 };
        _timer.Tick += (_, _) =>
        {
            if (IsDisposed) return;
            if (_displayProgress < _targetProgress)
            {
                _displayProgress = Math.Min(_targetProgress, _displayProgress + 2);
                _bar.Value = _displayProgress;
            }
            else if (_displayProgress < 90 && _stepIndex < _steps.Length - 1)
            {
                // Avance ambiental hasta el 90% mientras trabaja el BootTask.
                _stepIndex++;
                _lblStatus.Text = _steps[_stepIndex];
                _targetProgress = Math.Min(90, _targetProgress + 12);
                LayoutOverlay();
            }
        };
    }

    private void LayoutOverlay()
    {
        if (_lblVersion is null || _lblStatus is null || _bar is null) return;
        _lblVersion.Location = new Point(ClientSize.Width - _lblVersion.Width - 16, 10);
        _lblStatus.Location = new Point(16, ClientSize.Height - 52);
        _bar.Bounds = new Rectangle(ClientSize.Width / 2 - 20, ClientSize.Height - 54,
            Math.Max(100, ClientSize.Width / 2), 16);
    }

    /// <summary>Actualiza barra + estado (seguro desde cualquier hilo).</summary>
    public void Report(int percent, string status)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(() => Report(percent, status)); } catch { }
            return;
        }
        _targetProgress = Math.Clamp(percent, 0, 100);
        if (!string.IsNullOrEmpty(status))
            _lblStatus.Text = status;
        // Salto inmediato si el reporte viene de la carga real.
        if (_targetProgress >= _displayProgress)
        {
            _displayProgress = _targetProgress;
            _bar.Value = _displayProgress;
        }
        LayoutOverlay();
    }

    private void Finish()
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(Finish); } catch { }
            return;
        }
        try { _timer.Stop(); } catch { }
        _bar.Value = 100;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static string GetAppVersion()
    {
        try
        {
            // AssemblyVersion (csproj <Version>) con fallback a ProductVersion.
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            if (v is not null) return v.ToString(3);
            return Application.ProductVersion;
        }
        catch { return Application.ProductVersion; }
    }

    /// <summary>
    /// Resuelve la imagen del splash: prueba todos los nombres de manifiesto
    /// plausibles y, como respaldo, el archivo junto al .exe / proyecto.
    /// </summary>
    private static Image LoadSplashImage()
    {
        var asm = Assembly.GetExecutingAssembly();
        string[] candidates =
        [
            "CursorRotator.assets.splash.jpg",
            "CursorRotator.splash.jpg",
            "splash.jpg",
        ];
        foreach (var name in candidates)
        {
            try
            {
                using var s = asm.GetManifestResourceStream(name);
                if (s is not null) return Image.FromStream(s);
            }
            catch { }
        }
        string[] files =
        [
            Path.Combine(AppContext.BaseDirectory, "splash.jpg"),
            Path.Combine(AppContext.BaseDirectory, "assets", "splash.jpg"),
        ];
        foreach (var f in files)
        {
            try { if (File.Exists(f)) return Image.FromFile(f); } catch { }
        }
        return null;
    }

    /// <summary>
    /// Barra con degradado lineal cian→púrpura y esquinas redondeadas
    /// (Opción A: Custom Paint, sin el ProgressBar nativo verde/cuadrado).
    /// </summary>
    private sealed class GradientBar : Control
    {
        private int _value;
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Value
        {
            get => _value;
            set { _value = Math.Clamp(value, 0, 100); Invalidate(); }
        }

        public GradientBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Rounded(rect, 8);
            using var back = new SolidBrush(Color.FromArgb(120, 10, 10, 25));
            e.Graphics.FillPath(back, path);
            int fill = (int)((Width - 2) * _value / 100f);
            if (fill > 0)
            {
                using var grad = new LinearGradientBrush(
                    new Rectangle(1, 1, Math.Max(1, fill), Height - 2),
                    Color.FromArgb(70, 220, 255), Color.FromArgb(150, 90, 255),
                    LinearGradientMode.Horizontal);
                using var fillPath = Rounded(new Rectangle(1, 1, fill, Height - 2), 7);
                e.Graphics.FillPath(grad, fillPath);
            }
            using var border = new Pen(Color.FromArgb(90, 120, 160, 220));
            e.Graphics.DrawPath(border, path);
        }

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
