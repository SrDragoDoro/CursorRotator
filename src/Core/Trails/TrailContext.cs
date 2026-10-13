namespace CursorRotator.Core.Trails;

/// <summary>Partícula compartida por los estilos clásicos y varios avanzados.</summary>
public struct TrailParticle
{
    public float X, Y, Vx, Vy;
    public float Life, MaxLife;
    public float Size;
    public float Hue;
    public int Seed;
    public char Glyph;
}

/// <summary>
/// Contexto compartido: opciones, cursor, tiempo, historial, partículas y
/// parámetros físicos (los fija el motor activo). Lo muta un solo hilo.
/// </summary>
public sealed class TrailContext
{
    public TrailOptions Options { get; } = new();
    public float CursorX, CursorY, CursorVx, CursorVy;
    public float Time;
    public readonly List<(float X, float Y, float T)> History = new();
    public readonly List<TrailParticle> Particles = new();
    public int MaxAlive = 40;

    // Física configurable por el motor (Inercial la modifica).
    public float GravityY;
    public bool BounceEdges;
    public RectangleF BounceBounds;
    public float Momentum = 1f;
    /// <summary>Centro de la pantalla virtual (para caleidoscopio).</summary>
    public PointF ScreenCenter;

    private float _emitFrac;

    /// <summary>
    /// Presupuesto de emisión con acumulador fraccional: a densidad 0.1× emite
    /// ~1 partícula cada 5 eventos; a 3.0× triplica. Determinista en conteo.
    /// </summary>
    public int ConsumeEmission(float baseRate)
    {
        _emitFrac += baseRate * Options.Density;
        int n = (int)Math.Floor(_emitFrac);
        _emitFrac -= n;
        return n;
    }

    /// <summary>Tope escalado por densidad (mínimo sensato garantizado).</summary>
    public int ScaledCap(int baseCap) =>
        Math.Max(4, (int)Math.Round(baseCap * Options.Density));

    public void ResetEmission() => _emitFrac = 0;

    public void Spawn(float x, float y, float vx, float vy,
        float life, float size, float hue = 0, char glyph = '\0')
    {
        Particles.Add(new TrailParticle
        {
            X = x, Y = y, Vx = vx, Vy = vy,
            MaxLife = life, Life = life, Size = size, Hue = hue,
            Seed = Random.Shared.Next(1000), Glyph = glyph,
        });
    }

    /// <summary>Color base: heredado del cursor o paleta del preset.</summary>
    public Color BaseColor(Color fallback)
    {
        if (Options.UseCursorColor && !Options.CursorColor.IsEmpty)
            return Options.CursorColor;
        return fallback;
    }

    /// <summary>
    /// Color de la paleta activa por semilla (reparto secuencial). Si no hay
    /// paleta personalizada, devuelve el fallback (lógica unicolor anterior).
    /// </summary>
    public Color PalettePick(int seed, Color fallback)
    {
        var pal = Options.CustomPalette;
        if (Options.UseCursorColor && pal is { Length: > 0 })
            return pal[Math.Abs(seed) % pal.Length];
        return BaseColor(fallback);
    }

    public static Color Lerp(Color a, Color b, float f) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * f),
        (int)(a.G + (b.G - a.G) * f),
        (int)(a.B + (b.B - a.B) * f));

    public static float HueOf(Color c)
    {
        float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
        float mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
        if (mx == mn) return 0;
        float d = mx - mn;
        float h = mx == r ? (g - b) / d + (g < b ? 6 : 0)
                : mx == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return h * 60;
    }

    public static Color FromHsv(float h, float s, float v)
    {
        h = ((h % 360) + 360) % 360;
        float c = v * s;
        float x = c * (1 - MathF.Abs((h / 60) % 2 - 1));
        float m = v - c;
        float rp, gp, bp;
        if (h < 60) { rp = c; gp = x; bp = 0; }
        else if (h < 120) { rp = x; gp = c; bp = 0; }
        else if (h < 180) { rp = 0; gp = c; bp = x; }
        else if (h < 240) { rp = 0; gp = x; bp = c; }
        else if (h < 300) { rp = x; gp = 0; bp = c; }
        else { rp = c; gp = 0; bp = x; }
        return Color.FromArgb(
            (int)((rp + m) * 255), (int)((gp + m) * 255), (int)((bp + m) * 255));
    }
}
