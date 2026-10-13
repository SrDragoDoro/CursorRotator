namespace CursorRotator.UI;

/// <summary>Paletas y pintado genérico para diálogos.</summary>
public static class Theme
{
    public static readonly Color Accent = Color.FromArgb(0x7C, 0x5C, 0xFF);

    public static void Palettes(bool dark, out Color bg, out Color card, out Color fg, out Color muted)
    {
        if (dark)
        {
            bg = Color.FromArgb(0x1E, 0x1E, 0x2E); card = Color.FromArgb(0x2A, 0x2A, 0x3D);
            fg = Color.White; muted = Color.FromArgb(170, 170, 190);
        }
        else
        {
            bg = Color.FromArgb(242, 242, 247); card = Color.White;
            fg = Color.Black; muted = Color.FromArgb(90, 90, 110);
        }
    }

    /// <summary>Aplica fondo/texto recursivamente; ignora controles marcados.</summary>
    public static void Apply(Control root, bool dark, Predicate<Control> skip = null)
    {
        Palettes(dark, out var bg, out var card, out var fg, out _);
        root.BackColor = bg;
        root.ForeColor = fg;
        ApplyTo(root.Controls, dark, bg, card, fg, skip);
    }

    private static void ApplyTo(Control.ControlCollection controls, bool dark,
        Color bg, Color card, Color fg, Predicate<Control> skip)
    {
        foreach (Control c in controls)
        {
            if (skip is not null && skip(c)) continue;
            switch (c)
            {
                case Button b:
                    b.BackColor = card;
                    b.ForeColor = fg;
                    b.FlatAppearance.BorderColor = Accent;
                    break;
                case ListBox lb:
                    lb.BackColor = card; lb.ForeColor = fg; lb.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case TextBoxBase tb:
                    tb.BackColor = card; tb.ForeColor = fg; tb.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case GroupBox gb:
                    gb.ForeColor = fg;
                    ApplyTo(gb.Controls, dark, bg, card, fg, skip);
                    break;
                case NumericUpDown nud:
                    nud.BackColor = card; nud.ForeColor = fg;
                    break;
                case ComboBox cb:
                    cb.BackColor = card; cb.ForeColor = fg;
                    break;
                case CheckBox:
                case RadioButton:
                    c.ForeColor = fg;
                    break;
                case DarkScrollPanel dsp:
                    dsp.BackColor = bg;
                    ApplyTo(dsp.Controls, dark, bg, card, fg, skip);
                    dsp.Bar.ApplyTheme(dark, bg);
                    break;
                case DarkListBox dlb:
                    dlb.ApplyTheme(dark, bg, card, fg);
                    break;
                default:
                    c.BackColor = bg; c.ForeColor = fg;
                    if (c.HasChildren) ApplyTo(c.Controls, dark, bg, card, fg, skip);
                    break;
            }
        }
    }

    /// <summary>
    /// Estados de casillas/radios inconfundibles en ambos temas (el modo
    /// claro lavaba el check nativo): marcado = negrita + pastilla/foco.
    /// Llamar DESPUÉS de Apply (respeta el tema) y tras cada cambio de tema.
    /// </summary>
    public static void WireCheckState(CheckBox c, bool dark)
    {
        c.CheckedChanged += (_, _) => RefreshCheckState(c, dark);
        RefreshCheckState(c, dark);
    }

    public static void WireCheckState(RadioButton r, bool dark)
    {
        r.CheckedChanged += (_, _) => RefreshCheckState(r, dark);
        RefreshCheckState(r, dark);
    }

    public static void RefreshCheckState(CheckBox c, bool dark)
    {
        bool on = c.Checked;
        if (c.Font.Bold != on)
            c.Font = new Font(c.Font.FontFamily, c.Font.Size,
                on ? FontStyle.Bold : FontStyle.Regular);
        if (dark)
        {
            c.BackColor = Color.Transparent;
            c.ForeColor = on ? Color.FromArgb(0xC9, 0xBC, 0xFF) : Color.White;
        }
        else
        {
            c.BackColor = on ? Color.FromArgb(0xE6, 0xE0, 0xFF) : Color.Transparent;
            c.ForeColor = on ? Color.FromArgb(0x3D, 0x2B, 0x7A) : Color.Black;
        }
    }

    public static void RefreshCheckState(RadioButton r, bool dark)
    {
        bool on = r.Checked;
        if (r.Font.Bold != on)
            r.Font = new Font(r.Font.FontFamily, r.Font.Size,
                on ? FontStyle.Bold : FontStyle.Regular);
        if (dark)
        {
            r.BackColor = Color.Transparent;
            r.ForeColor = on ? Color.FromArgb(0xC9, 0xBC, 0xFF) : Color.White;
        }
        else
        {
            r.BackColor = on ? Color.FromArgb(0xE6, 0xE0, 0xFF) : Color.Transparent;
            r.ForeColor = on ? Color.FromArgb(0x3D, 0x2B, 0x7A) : Color.Black;
        }
    }
}
