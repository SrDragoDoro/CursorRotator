using CursorRotator.Core;

namespace CursorRotator.UI;

/// <summary>Tutorial paso a paso (no modal: abre junto a la app).</summary>
public sealed class TutorialForm : Form
{
    /// <summary>
    /// Abre el tutorial JUNTO a la ventana dueña (no modal, arrastrable).
    /// A la derecha si cabe; si no, a la izquierda.
    /// </summary>
    public static void ShowBeside(Form owner, AppSettings settings, bool dark)
    {
        var tuto = new TutorialForm(settings, dark);
        tuto.StartPosition = FormStartPosition.Manual;
        try
        {
            var area = Screen.FromControl(owner).WorkingArea;
            int x = owner.Right + 16;
            if (x + tuto.Width > area.Right)
                x = Math.Max(area.Left, owner.Left - tuto.Width - 16);
            int y = Math.Clamp(owner.Top, area.Top,
                Math.Max(area.Top, area.Bottom - tuto.Height));
            tuto.Location = new Point(
                Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - tuto.Width)), y);
        }
        catch { tuto.StartPosition = FormStartPosition.CenterParent; }
        tuto.Show(owner); // dueña: se cierra con la app, sin bloquearla
    }

    private readonly AppSettings _settings;
    private readonly List<(string Emoji, string Title, string Body)> _steps =
    [
        ("👋", "Bienvenido a CursorRotator",
         "Instala y rota cursores personalizados automáticamente.\nPulsa Siguiente para un tour de 30 segundos."),
        ("📦", "1 · Instalación en 1 clic",
         "El botón púrpura detecta tus .zip/.rar, los extrae a tu carpeta personal, registra los esquemas y evita pedirte permisos de administrador."),
        ("🔁", "2 · Rotación automática",
         "Activa la rotación, elige cada cuánto cambia (segundos, minutos, horas u horas fijas) y usa Aplicar siguiente para probar al instante. Pausar te devuelve el control manual."),
        ("✨", "3 · Estelas y efectos",
         "Activa las estelas, elige estilo en la galería, ajusta tamaño, duración y cantidad, hereda el color de tu cursor y prueba los 20 efectos de clic."),
        ("🔔", "4 · Bandeja y preferencias",
         "Minimiza a la bandeja junto al reloj para que siga rotando. En Opciones Avanzadas: inicio con Windows, topmost para juegos y tope de refresco."),
    ];

    private int _index;
    private Label _lblEmoji, _lblTitle, _lblBody, _lblCount;
    private Button _btnPrev, _btnNext, _btnFinish;
    private CheckBox _chkHide;

    public TutorialForm(AppSettings settings, bool dark)
    {
        _settings = settings;
        Text = "Tutorial";
        Size = new Size(480, 420);
        MinimumSize = new Size(440, 400);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.None;

        // Arrastre desde cualquier punto (sin barra de título): se cablea al
        // formulario Y a cada descendiente (los paneles llenos tapaban el
        // arrastre). Botones y cajas de texto no arrastran (conservan clic).
        bool drag = false;
        Point grabScreen = Point.Empty;
        void BeginDrag(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && s is Control c)
            {
                drag = true;
                grabScreen = c.PointToScreen(e.Location);
            }
        }
        void MoveDrag(object s, MouseEventArgs e)
        {
            if (!drag || s is not Control c) return;
            Point cur = c.PointToScreen(e.Location);
            Location = new Point(Left + cur.X - grabScreen.X, Top + cur.Y - grabScreen.Y);
            grabScreen = cur;
        }
        void EndDrag(object s, MouseEventArgs e) => drag = false;
        void WireDrag(Control c)
        {
            if (c is not ButtonBase and not TextBoxBase)
            {
                c.MouseDown += BeginDrag;
                c.MouseMove += MoveDrag;
                c.MouseUp += EndDrag;
            }
            foreach (Control child in c.Controls)
                WireDrag(child);
        }
        WireDrag(this);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(24) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        _lblEmoji = new Label { AutoSize = true, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 40f), TextAlign = ContentAlignment.MiddleCenter };
        _lblTitle = new Label { AutoSize = true, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 13f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
        _lblBody = new Label { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopCenter };
        _lblCount = new Label { AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        root.Controls.Add(_lblEmoji, 0, 0);
        root.Controls.Add(_lblTitle, 0, 1);
        root.Controls.Add(_lblBody, 0, 2);

        var nav = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        _btnPrev = new Button { Text = "< Anterior", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(110, 34) };
        _btnPrev.Click += (_, _) => ShowStep(_index - 1);
        _btnNext = new Button { Text = "Siguiente >", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(110, 34) };
        _btnNext.Click += (_, _) => ShowStep(_index + 1);
        _btnFinish = new Button { Text = "Finalizar", FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(110, 34) };
        _btnFinish.Click += (_, _) => Close();
        nav.Controls.AddRange([_btnPrev, _btnNext, _btnFinish]);
        root.Controls.Add(nav, 0, 3);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        _chkHide = new CheckBox { Text = "No volver a mostrar", AutoSize = true };
        _chkHide.CheckedChanged += (_, _) =>
        {
            _settings.ShowTutorial = !_chkHide.Checked;
            _settings.Save();
        };
        bottom.Controls.Add(_chkHide);
        bottom.Controls.Add(_lblCount);
        root.Controls.Add(bottom, 0, 4);

        ShowStep(0);
        Theme.Apply(this, dark);
        Theme.WireCheckState(_chkHide, dark);
    }

    private void ShowStep(int i)
    {
        _index = Math.Clamp(i, 0, _steps.Count - 1);
        var (emoji, title, body) = _steps[_index];
        _lblEmoji.Text = emoji;
        _lblTitle.Text = title;
        _lblBody.Text = body;
        _lblCount.Text = $"Paso {_index + 1} de {_steps.Count}";
        _btnPrev.Enabled = _index > 0;
        _btnNext.Enabled = _index < _steps.Count - 1;
    }
}
