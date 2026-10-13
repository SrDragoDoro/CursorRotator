using System.Drawing.Drawing2D;

namespace CursorRotator.Core.Trails;

/// <summary>Contrato de un efecto de clic (ráfaga en el puntero).</summary>
public interface IClickEffect : IDisposable
{
    string Name { get; }
    /// <summary>Multiplicador de tamaño (slider de clic). Lo fija el motor.</summary>
    float BurstScale { get; set; }
    void Trigger(float x, float y, Color[] palette);
    void Update(float dt);
    void Draw(Graphics g);
    bool HasActivity { get; }
    RectangleF GetBounds();
}

/// <summary>Base con pool de partículas + integración común.</summary>
public abstract class ClickEffectBase : IClickEffect
{
    protected struct CP
    {
        public float X, Y, Vx, Vy, Life, MaxLife, Size, Rot, Spin;
        public Color Col;
        public char Glyph;
    }

    protected readonly List<CP> Parts = new();
    protected float SizeScale = 1f;
    public abstract string Name { get; }

    public virtual void Trigger(float x, float y, Color[] palette) { }

    protected void Add(float x, float y, float vx, float vy, float life, float size, Color col,
        char glyph = '\0', float spin = 0)
    {
        if (Parts.Count >= 140) Parts.RemoveAt(0);
        Parts.Add(new CP
        {
            X = x, Y = y, Vx = vx, Vy = vy, Life = life, MaxLife = life,
            Size = size * BurstScale, Rot = 0, Spin = spin, Col = col, Glyph = glyph,
        });
    }

    protected float Gravity;
    protected float Friction = 2f;

    /// <inheritdoc/>
    public float BurstScale { get; set; } = 1f;

    /// <summary>Escala un radio/tamaño por el slider de clic.</summary>
    protected float S(float v) => v * BurstScale;

    public virtual void Update(float dt)
    {
        for (int i = Parts.Count - 1; i >= 0; i--)
        {
            var p = Parts[i];
            p.Life -= dt;
            if (p.Life <= 0) { Parts.RemoveAt(i); continue; }
            p.Vx *= MathF.Max(0, 1 - Friction * dt);
            p.Vy *= MathF.Max(0, 1 - Friction * dt);
            p.Vy += Gravity * dt;
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            p.Rot += p.Spin * dt;
            Parts[i] = p;
        }
    }

    public virtual bool HasActivity => Parts.Count > 0;

    public virtual RectangleF GetBounds()
    {
        if (Parts.Count == 0) return RectangleF.Empty;
        float x0 = float.MaxValue, y0 = float.MaxValue;
        float x1 = float.MinValue, y1 = float.MinValue;
        foreach (var p in Parts)
        {
            float r = Math.Max(8, p.Size * 2);
            if (p.X - r < x0) x0 = p.X - r;
            if (p.Y - r < y0) y0 = p.Y - r;
            if (p.X + r > x1) x1 = p.X + r;
            if (p.Y + r > y1) y1 = p.Y + r;
        }
        return RectangleF.FromLTRB(x0, y0, x1, y1);
    }

    public abstract void Draw(Graphics g);
    public virtual void Dispose() { }

    protected static Color Pick(Color[] pal, int i, Color fallback) =>
        pal is { Length: > 0 } ? pal[Math.Abs(i) % pal.Length] : fallback;
}

/// <summary>Anillo expansivo genérico (base de varios efectos).</summary>
public abstract class RingEffectBase : ClickEffectBase
{
    protected float X, Y, R, Life, MaxLife = 0.6f;
    protected float Growth = 420f;
    protected Color RingColor = Color.White;

    public override void Trigger(float x, float y, Color[] palette)
    {
        X = x; Y = y; R = 6; Life = MaxLife;
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (Life > 0)
        {
            Life -= dt;
            R += Growth * BurstScale * dt;
        }
    }

    public override bool HasActivity => base.HasActivity || Life > 0;

    public override RectangleF GetBounds()
    {
        var b = base.GetBounds();
        if (Life <= 0) return b;
        var ring = RectangleF.FromLTRB(X - R, Y - R, X + R, Y + R);
        return b.IsEmpty ? ring : RectangleF.Union(b, ring);
    }

    protected void DrawRing(Graphics g, Color col, float widthMul = 1f)
    {
        if (Life <= 0) return;
        float t = Life / MaxLife;
        using var pen = new Pen(Color.FromArgb((int)(210 * t), col),
            Math.Max(1, 4 * t * widthMul * BurstScale));
        g.DrawEllipse(pen, X - R, Y - R, R * 2, R * 2);
    }
}

public static class ClickEffectFactory
{
    public static readonly string[] Names =
    [
        "🌊 Onda de Choque", "💦 Salpicadura", "🎯 Implosión", "🧩 Fragmentación",
        "🪐 Anillos Cuánticos", "🔢 Binario", "🧊 Grieta", "⚡ EMP",
        "🔆 Chispas de Soldadura", "🛸 Ataque Orbital", "🫧 Burbuja",
        "💥 Supernova", "❀ Mandala", "✴️ Shuriken", "👻 Eco/Fantasma",
        "❄️ Copos de Nieve", "🌌 Agujero de Gusano", "🎉 Confeti Explosivo",
        "🧲 Anomalía Magnética", "📡 Radar",
    ];

    public static IClickEffect Create(int index) => index switch
    {
        1 => new SplatEffect(),
        2 => new ImplosionEffect(),
        3 => new FragmentEffect(),
        4 => new QuantumEffect(),
        5 => new BinaryEffect(),
        6 => new CrackEffect(),
        7 => new EmpEffect(),
        8 => new WeldEffect(),
        9 => new StrikeEffect(),
        10 => new BubblePopEffect(),
        11 => new SupernovaEffect(),
        12 => new MandalaEffect(),
        13 => new ShurikenEffect(),
        14 => new GhostEffect(),
        15 => new SnowEffect(),
        16 => new WormholeEffect(),
        17 => new BoomConfettiEffect(),
        18 => new MagneticEffect(),
        19 => new RadarEffect(),
        _ => new ShockwaveEffect(),
    };
}

// 1. Onda de choque.
internal sealed class ShockwaveEffect : RingEffectBase
{
    public override string Name => "Onda de Choque";
    public override void Draw(Graphics g)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        try { DrawRing(g, Color.White); }
        finally { g.SmoothingMode = old; }
    }
}

// 2. Salpicadura.
internal sealed class SplatEffect : ClickEffectBase
{
    public override string Name => "Salpicadura";
    public override void Trigger(float x, float y, Color[] palette)
    {
        Friction = 6f;
        for (int i = 0; i < 26; i++)
        {
            float a = Random.Shared.NextSingle() * MathF.PI * 2;
            float sp = 150 + Random.Shared.NextSingle() * 450;
            Add(x, y, MathF.Cos(a) * sp, MathF.Sin(a) * sp, 0.55f,
                3 + Random.Shared.NextSingle() * 5, Pick(palette, i, Color.FromArgb(120, 200, 255)));
        }
    }
    public override void Draw(Graphics g)
    {
        foreach (var p in Parts)
        {
            float t = p.Life / p.MaxLife;
            float r = Math.Max(1, p.Size * t);
            using var b = new SolidBrush(Color.FromArgb((int)(240 * t), p.Col));
            g.FillEllipse(b, p.X - r, p.Y - r, r * 2, r * 2);
        }
    }
}

// 3. Implosión.
internal sealed class ImplosionEffect : ClickEffectBase
{
    private float _cx, _cy;
    public override string Name => "Implosión";
    public override void Trigger(float x, float y, Color[] palette)
    {
        _cx = x; _cy = y;
        Friction = 0f;
        for (int i = 0; i < 30; i++)
        {
            float a = Random.Shared.NextSingle() * MathF.PI * 2;
            float r = 130 + Random.Shared.NextSingle() * 120;
            float px = x + MathF.Cos(a) * r, py = y + MathF.Sin(a) * r;
            // Velocidad inicial hacia el centro + aceleración en Update.
            Add(px, py, -MathF.Cos(a) * 120, -MathF.Sin(a) * 120, 0.7f,
                2.5f + Random.Shared.NextSingle() * 3, Pick(palette, i, Color.White));
        }
    }
    public override void Update(float dt)
    {
        for (int i = Parts.Count - 1; i >= 0; i--)
        {
            var p = Parts[i];
            p.Life -= dt;
            float dx = _cx - p.X, dy = _cy - p.Y;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            // Al llegar al centro: fundido rápido, no corte.
            if (p.Life <= 0 || d < 6)
            {
                p.Life -= dt * 6;
                if (p.Life <= 0 || d < 3) { Parts.RemoveAt(i); continue; }
            }
            if (d > 0.5f)
            {
                p.Vx += dx / d * 1500 * dt;
                p.Vy += dy / d * 1500 * dt;
            }
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            Parts[i] = p;
        }
    }
    public override void Draw(Graphics g)
    {
        foreach (var p in Parts)
        {
            float t = Math.Clamp(p.Life / p.MaxLife, 0, 1);
            using var b = new SolidBrush(Color.FromArgb((int)(240 * t), p.Col));
            g.FillEllipse(b, p.X - 2, p.Y - 2, 4, 4);
        }
    }
}

// 4. Fragmentación.
internal sealed class FragmentEffect : ClickEffectBase
{
    public override string Name => "Fragmentación";
    public override void Trigger(float x, float y, Color[] palette)
    {
        Friction = 1.6f; Gravity = 500;
        for (int i = 0; i < 16; i++)
        {
            float a = Random.Shared.NextSingle() * MathF.PI * 2;
            float sp = 180 + Random.Shared.NextSingle() * 380;
            Add(x, y, MathF.Cos(a) * sp, MathF.Sin(a) * sp - 120, 0.8f,
                5 + Random.Shared.NextSingle() * 7, Pick(palette, i, Color.FromArgb(255, 200, 120)),
                spin: (Random.Shared.NextSingle() * 2 - 1) * 12);
        }
    }
    public override void Draw(Graphics g)
    {
        foreach (var p in Parts)
        {
            float t = p.Life / p.MaxLife;
            float s = Math.Max(2, p.Size * t);
            float ca = MathF.Cos(p.Rot), sa = MathF.Sin(p.Rot);
            var pts = new PointF[]
            {
                new(p.X + ca * s, p.Y + sa * s),
                new(p.X - ca * s * 0.8f - sa * s * 0.7f, p.Y - sa * s * 0.8f + ca * s * 0.7f),
                new(p.X - ca * s * 0.8f + sa * s * 0.7f, p.Y - sa * s * 0.8f - ca * s * 0.7f),
            };
            using var b = new SolidBrush(Color.FromArgb((int)(240 * t), p.Col));
            g.FillPolygon(b, pts);
        }
    }
}

// 5. Anillos cuánticos.
internal sealed class QuantumEffect : RingEffectBase
{
    private float _phase;
    public override string Name => "Anillos Cuánticos";
    public override void Update(float dt)
    {
        base.Update(dt);
        _phase += dt * 7;
    }
    public override void Draw(Graphics g)
    {
        if (Life <= 0) return;
        float t = Life / MaxLife;
        // Dos elipses con rotación 3D simulada (escala X oscilante).
        for (int k = 0; k < 2; k++)
        {
            float sx = Math.Abs(MathF.Cos(_phase + k * MathF.PI / 2));
            float rx = Math.Max(2, R * (0.25f + 0.75f * sx));
            using var pen = new Pen(Color.FromArgb((int)(200 * t),
                k == 0 ? Color.FromArgb(140, 230, 255) : Color.FromArgb(240, 160, 255)), 2 * BurstScale);
            g.DrawEllipse(pen, X - rx, Y - R, rx * 2, R * 2);
        }
    }
}

// 6. Binario.
internal sealed class BinaryEffect : ClickEffectBase
{
    private Font _font;
    private float _fontScale = -1;
    public override string Name => "Binario";
    public override void Trigger(float x, float y, Color[] palette)
    {
        Friction = 0.6f;
        for (int i = 0; i < 22; i++)
            Add(x + (Random.Shared.NextSingle() * 2 - 1) * 60,
                y + (Random.Shared.NextSingle() * 2 - 1) * 20,
                (Random.Shared.NextSingle() * 2 - 1) * 40,
                100 + Random.Shared.NextSingle() * 160, 0.9f, 10,
                Pick(palette, i, Color.FromArgb(90, 255, 150)),
                glyph: Random.Shared.Next(2) == 0 ? '0' : '1');
    }
    public override void Draw(Graphics g)
    {
        float want = 11 * BurstScale;
        if (_font is null || Math.Abs(_fontScale - want) > 0.5f)
        {
            _font?.Dispose();
            _font = new Font("Consolas", want, FontStyle.Bold, GraphicsUnit.Pixel);
            _fontScale = want;
        }
        foreach (var p in Parts)
        {
            float t = p.Life / p.MaxLife;
            using var b = new SolidBrush(Color.FromArgb((int)(235 * t), p.Col));
            g.DrawString(p.Glyph.ToString(), _font, b, p.X, p.Y);
        }
    }
    public override void Dispose() { _font?.Dispose(); _font = null; }
}

// 7. Grieta (persiste 1 s, estática).
internal sealed class CrackEffect : ClickEffectBase
{
    private readonly List<PointF[]> _segs = new();
    private float _life, _x0, _y0, _x1, _y1;
    public override string Name => "Grieta";
    public override void Trigger(float x, float y, Color[] palette)
    {
        _segs.Clear();
        _life = 1f;
        _x0 = _y0 = float.MaxValue;
        _x1 = _y1 = float.MinValue;
        // 7 grietas radiales con subdivisión fractal.
        for (int b = 0; b < 7; b++)
        {
            float ang = b / 7f * MathF.PI * 2 + Random.Shared.NextSingle() * 0.5f;
            var line = new List<PointF> { new(x, y) };
            float cx = x, cy = y;
            float step = (26 + Random.Shared.NextSingle() * 30) * BurstScale;
            for (int k = 0; k < 4; k++)
            {
                ang += (Random.Shared.NextSingle() * 2 - 1) * 0.55f;
                cx += MathF.Cos(ang) * step;
                cy += MathF.Sin(ang) * step;
                // Punto medio desplazado = diente fractal.
                float mx = (line[^1].X + cx) / 2 + (Random.Shared.NextSingle() * 2 - 1) * 9;
                float my = (line[^1].Y + cy) / 2 + (Random.Shared.NextSingle() * 2 - 1) * 9;
                line.Add(new PointF(mx, my));
                line.Add(new PointF(cx, cy));
                if (mx < _x0) _x0 = mx; if (my < _y0) _y0 = my;
                if (mx > _x1) _x1 = mx; if (my > _y1) _y1 = my;
                if (cx < _x0) _x0 = cx; if (cy < _y0) _y0 = cy;
                if (cx > _x1) _x1 = cx; if (cy > _y1) _y1 = cy;
            }
            _segs.Add(line.ToArray());
        }
    }
    public override void Update(float dt) => _life = Math.Max(0, _life - dt);
    public override bool HasActivity => _life > 0 || base.HasActivity;
    public override void Draw(Graphics g)
    {
        if (_life <= 0) return;
        using var pen = new Pen(Color.FromArgb((int)(220 * _life), Color.FromArgb(200, 230, 255)), 1.5f);
        foreach (var s in _segs)
            if (s.Length > 1) g.DrawLines(pen, s);
    }
    public override RectangleF GetBounds()
    {
        var b = base.GetBounds();
        if (_life <= 0 || _x0 > _x1) return b;
        var m = RectangleF.FromLTRB(_x0 - 4, _y0 - 4, _x1 + 4, _y1 + 4);
        return b.IsEmpty ? m : RectangleF.Union(b, m);
    }
}

// 8. EMP.
internal sealed class EmpEffect : RingEffectBase
{
    public override string Name => "EMP";
    public override void Draw(Graphics g)
    {
        if (Life <= 0) return;
        float t = Life / MaxLife;
        // Deformación: disco complementario expansivo + anillo.
        using var fill = new SolidBrush(Color.FromArgb((int)(60 * t), Color.FromArgb(255, 80, 220)));
        g.FillEllipse(fill, X - R, Y - R, R * 2, R * 2);
        DrawRing(g, Color.FromArgb(255, 120, 255));
    }
}

// 9. Chispas de soldadura.
internal sealed class WeldEffect : ClickEffectBase
{
    private float _floorY;
    public override string Name => "Chispas de Soldadura";
    public override void Trigger(float x, float y, Color[] palette)
    {
        Friction = 0.8f; Gravity = 900;
        _floorY = y + 70;
        for (int i = 0; i < 24; i++)
        {
            float a = -MathF.PI / 2 + (Random.Shared.NextSingle() * 2 - 1) * 1.1f;
            float sp = 250 + Random.Shared.NextSingle() * 450;
            Add(x, y, MathF.Cos(a) * sp, MathF.Sin(a) * sp, 0.8f,
                8 + Random.Shared.NextSingle() * 10,
                i % 2 == 0 ? Color.White : Color.FromArgb(255, 220, 90));
        }
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        // Rebote parabólico en el suelo virtual.
        for (int i = 0; i < Parts.Count; i++)
        {
            var p = Parts[i];
            if (p.Y > _floorY && p.Vy > 0)
            {
                p.Y = _floorY;
                p.Vy = -p.Vy * 0.45f;
                p.Vx *= 0.7f;
                Parts[i] = p;
            }
        }
    }
    public override void Draw(Graphics g)
    {
        foreach (var p in Parts)
        {
            float t = p.Life / p.MaxLife;
            using var pen = new Pen(Color.FromArgb((int)(240 * t), p.Col),
                Math.Max(1, p.Size * 0.25f));
            g.DrawLine(pen, p.X, p.Y, p.X - p.Vx * 0.03f, p.Y - p.Vy * 0.03f);
        }
    }
}

// 10. Ataque orbital.
internal sealed class StrikeEffect : ClickEffectBase
{
    private float _beamT, _beamX;
    public override string Name => "Ataque Orbital";
    public override void Trigger(float x, float y, Color[] palette)
    {
        _beamT = 0.35f;
        _beamX = x;
        for (int i = 0; i < 18; i++)
        {
            float a = Random.Shared.NextSingle() * MathF.PI * 2;
            float sp = 120 + Random.Shared.NextSingle() * 300;
            Add(x, y, MathF.Cos(a) * sp, MathF.Sin(a) * sp, 0.5f, 3,
                Pick(palette, i, Color.FromArgb(255, 240, 200)));
        }
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        _beamT = Math.Max(0, _beamT - dt);
    }
    public override bool HasActivity => base.HasActivity || _beamT > 0;
    public override void Draw(Graphics g)
    {
        if (_beamT > 0)
        {
            float t = _beamT / 0.35f;
            using var beam = new Pen(Color.FromArgb((int)(230 * t), Color.FromArgb(160, 220, 255)), 9 * BurstScale);
            g.DrawLine(beam, _beamX, -20, _beamX, 2000);
        }
        foreach (var p in Parts)
        {
            float t = p.Life / p.MaxLife;
            using var b = new SolidBrush(Color.FromArgb((int)(240 * t), p.Col));
            g.FillEllipse(b, p.X - 2, p.Y - 2, 4, 4);
        }
    }
    public override RectangleF GetBounds()
    {
        var b = base.GetBounds();
        return b; // el rayo es vertical infinito: no expande la caja (se dibuja igual)
    }
}

// 11. Burbuja iridiscente.
internal sealed class BubblePopEffect : ClickEffectBase
{
    private float _x, _y, _r, _life;
    private bool _popped;
    public override string Name => "Burbuja";
    public override void Trigger(float x, float y, Color[] palette)
    {
        _x = x; _y = y; _r = 8 * BurstScale; _life = 0.9f; _popped = false;
    }
    public override void Update(float dt)
    {
        if (_life <= 0) return;
        _life -= dt;
        if (!_popped)
        {
            _r += 60 * BurstScale * dt; // crece sutilmente
            if (_r > 34 || _life < 0.45f)
            {
                _popped = true; // estalla en puntos
                for (int i = 0; i < 14; i++)
                {
                    float a = Random.Shared.NextSingle() * MathF.PI * 2;
                    float sp = 60 + Random.Shared.NextSingle() * 180;
                    Add(_x, _y, MathF.Cos(a) * sp, MathF.Sin(a) * sp, 0.45f, 2.5f,
                        Color.FromArgb(170, 220, 255));
                }
            }
        }
        base.Update(dt);
    }
    public override bool HasActivity => base.HasActivity || _life > 0;
    public override void Draw(Graphics g)
    {
        if (_life > 0 && !_popped)
        {
            float t = _life / 0.9f;
            using var pen = new Pen(Color.FromArgb((int)(220 * t), Color.FromArgb(170, 220, 255)), 2);
            g.DrawEllipse(pen, _x - _r, _y - _r, _r * 2, _r * 2);
            using var glint = new SolidBrush(Color.FromArgb((int)(200 * t), Color.White));
            g.FillEllipse(glint, _x - _r * 0.5f, _y - _r * 0.6f, 6, 4);
        }
        foreach (var p in Parts)
        {
            float t = p.Life / p.MaxLife;
            using var b = new SolidBrush(Color.FromArgb((int)(230 * t), p.Col));
            g.FillEllipse(b, p.X - 2, p.Y - 2, 4, 4);
        }
    }
}

// 12. Supernova.
internal sealed class SupernovaEffect : ClickEffectBase
{
    public override string Name => "Supernova";
    public override void Trigger(float x, float y, Color[] palette)
    {
        Friction = 1.2f;
        for (int i = 0; i < 46; i++)
        {
            float a = Random.Shared.NextSingle() * MathF.PI * 2;
            float sp = 150 + Random.Shared.NextSingle() * 650;
            Add(x, y, MathF.Cos(a) * sp, MathF.Sin(a) * sp, 0.6f,
                2.5f + Random.Shared.NextSingle() * 3,
                Pick(palette, i, i % 3 == 0 ? Color.White : Color.FromArgb(255, 170, 60)));
        }
    }
    public override void Draw(Graphics g)
    {
        float coreT = Parts.Count > 0 ? Parts[0].Life / Parts[0].MaxLife : 0;
        if (coreT > 0)
        {
            using var core = new SolidBrush(Color.FromArgb((int)(240 * coreT), Color.White));
            g.FillEllipse(core, Parts[0].X - 9, Parts[0].Y - 9, 18, 18);
        }
        foreach (var p in Parts)
        {
            float t = p.Life / p.MaxLife;
            float r = Math.Max(1, p.Size * t);
            using var b = new SolidBrush(Color.FromArgb((int)(240 * t), p.Col));
            g.FillEllipse(b, p.X - r, p.Y - r, r * 2, r * 2);
        }
    }
}

// 13. Mandala.
internal sealed class MandalaEffect : ClickEffectBase
{
    private float _x, _y, _rot, _life;
    public override string Name => "Mandala";
    public override void Trigger(float x, float y, Color[] palette)
    {
        _x = x; _y = y; _rot = 0; _life = 1.1f;
    }
    public override void Update(float dt)
    {
        _life = Math.Max(0, _life - dt);
        _rot += dt * 2.4f;
        base.Update(dt);
    }
    public override bool HasActivity => base.HasActivity || _life > 0;
    public override void Draw(Graphics g)
    {
        if (_life <= 0) return;
        float t = _life / 1.1f;
        float r = (20 + (1 - t) * 70) * BurstScale;
        using var pen = new Pen(Color.FromArgb((int)(200 * t), Color.FromArgb(230, 180, 255)), 1.5f * BurstScale);
        for (int k = 0; k < 8; k++)
        {
            float a = _rot + k * MathF.PI / 4;
            float ex = _x + MathF.Cos(a) * r, ey = _y + MathF.Sin(a) * r;
            g.DrawLine(pen, _x, _y, ex, ey);
            g.DrawEllipse(pen, ex - 6 * BurstScale, ey - 6 * BurstScale, 12 * BurstScale, 12 * BurstScale);
        }
        using var ring = new Pen(Color.FromArgb((int)(160 * t), Color.White), 1 * BurstScale);
        g.DrawEllipse(ring, _x - r, _y - r, r * 2, r * 2);
    }
    public override RectangleF GetBounds()
    {
        if (_life <= 0) return base.GetBounds();
        var b = base.GetBounds();
        var m = RectangleF.FromLTRB(_x - 110 * BurstScale, _y - 110 * BurstScale, _x + 110 * BurstScale, _y + 110 * BurstScale);
        return b.IsEmpty ? m : RectangleF.Union(b, m);
    }
}

// 14. Shuriken.
internal sealed class ShurikenEffect : ClickEffectBase
{
    private struct Star { public float X, Y, Vx, Vy, Rot, Life, MaxLife, R; public Color Col; }
    private readonly List<Star> _st = new();
    public override string Name => "Shuriken";
    public override void Trigger(float x, float y, Color[] palette)
    {
        Friction = 1f;
        for (int i = 0; i < 5; i++)
        {
            float a = Random.Shared.NextSingle() * MathF.PI * 2;
            float sp = 200 + Random.Shared.NextSingle() * 350;
            _st.Add(new Star
            {
                X = x, Y = y, Vx = MathF.Cos(a) * sp, Vy = MathF.Sin(a) * sp,
                Rot = 0, Life = 0.7f, MaxLife = 0.7f,
                R = (9 + Random.Shared.NextSingle() * 6) * BurstScale,
                Col = Pick(palette, i, Color.FromArgb(200, 220, 235)),
            });
        }
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        for (int i = _st.Count - 1; i >= 0; i--)
        {
            var s = _st[i];
            s.Life -= dt;
            if (s.Life <= 0) { _st.RemoveAt(i); continue; }
            s.Rot += dt * 22; // giro veloz
            s.X += s.Vx * dt;
            s.Y += s.Vy * dt;
            s.Vx *= MathF.Max(0, 1 - 2 * dt);
            s.Vy *= MathF.Max(0, 1 - 2 * dt);
            _st[i] = s;
        }
    }
    public override bool HasActivity => base.HasActivity || _st.Count > 0;
    public override void Draw(Graphics g)
    {
        foreach (var s in _st)
        {
            float t = s.Life / s.MaxLife;
            var pts = new PointF[8];
            for (int k = 0; k < 8; k++)
            {
                float rr = (k % 2 == 0 ? s.R : s.R * 0.42f) * (0.4f + 0.6f * t);
                float a = s.Rot + k * MathF.PI / 4;
                pts[k] = new PointF(s.X + MathF.Cos(a) * rr, s.Y + MathF.Sin(a) * rr);
            }
            using var b = new SolidBrush(Color.FromArgb((int)(235 * t), s.Col));
            g.FillPolygon(b, pts);
        }
    }
    public override RectangleF GetBounds()
    {
        var b = base.GetBounds();
        if (_st.Count == 0) return b;
        float x0 = float.MaxValue, y0 = float.MaxValue;
        float x1 = float.MinValue, y1 = float.MinValue;
        foreach (var s in _st)
        {
            if (s.X - 20 < x0) x0 = s.X - 20;
            if (s.Y - 20 < y0) y0 = s.Y - 20;
            if (s.X + 20 > x1) x1 = s.X + 20;
            if (s.Y + 20 > y1) y1 = s.Y + 20;
        }
        var m = RectangleF.FromLTRB(x0, y0, x1, y1);
        return b.IsEmpty ? m : RectangleF.Union(b, m);
    }
}

// 15. Eco / fantasma.
internal sealed class GhostEffect : ClickEffectBase
{
    private float _x, _y, _life, _r;
    public override string Name => "Eco/Fantasma";
    public override void Trigger(float x, float y, Color[] palette)
    {
        _x = x; _y = y; _life = 0.8f; _r = 10 * BurstScale;
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        if (_life > 0)
        {
            _life -= dt;
            _r += 55 * BurstScale * dt;
        }
    }
    public override bool HasActivity => base.HasActivity || _life > 0;
    public override void Draw(Graphics g)
    {
        if (_life <= 0) return;
        float t = _life / 0.8f;
        // Réplica: cruz + cuadrado en expansión, in situ.
        using var pen = new Pen(Color.FromArgb((int)(200 * t), Color.FromArgb(180, 220, 255)), 2);
        g.DrawRectangle(pen, _x - _r, _y - _r, _r * 2, _r * 2);
        g.DrawLine(pen, _x - _r - 8 * BurstScale, _y, _x + _r + 8 * BurstScale, _y);
        g.DrawLine(pen, _x, _y - _r - 8 * BurstScale, _x, _y + _r + 8 * BurstScale);
    }
    public override RectangleF GetBounds()
    {
        var b = base.GetBounds();
        if (_life <= 0) return b;
        var m = RectangleF.FromLTRB(_x - _r - 10, _y - _r - 10, _x + _r + 10, _y + _r + 10);
        return b.IsEmpty ? m : RectangleF.Union(b, m);
    }
}

// 16. Copos de nieve.
internal sealed class SnowEffect : ClickEffectBase
{
    public override string Name => "Copos de Nieve";
    public override void Trigger(float x, float y, Color[] palette)
    {
        Friction = 0.5f; Gravity = 60;
        for (int i = 0; i < 20; i++)
        {
            float a = Random.Shared.NextSingle() * MathF.PI * 2;
            float sp = 40 + Random.Shared.NextSingle() * 140;
            Add(x, y, MathF.Cos(a) * sp, MathF.Sin(a) * sp - 60, 1.4f,
                4 + Random.Shared.NextSingle() * 4,
                Pick(palette, i, Color.FromArgb(230, 240, 255)),
                spin: (Random.Shared.NextSingle() * 2 - 1) * 3);
        }
    }
    public override void Draw(Graphics g)
    {
        foreach (var p in Parts)
        {
            float t = p.Life / p.MaxLife;
            float r = Math.Max(2, p.Size * (0.5f + 0.5f * t));
            // Hexágono con sway + rotación lenta.
            float sway = MathF.Sin(p.Rot * 2) * 6;
            var pts = new PointF[6];
            for (int k = 0; k < 6; k++)
            {
                float a = p.Rot + k * MathF.PI / 3;
                pts[k] = new PointF(p.X + sway * 0.3f + MathF.Cos(a) * r, p.Y + MathF.Sin(a) * r);
            }
            using var b = new SolidBrush(Color.FromArgb((int)(225 * t), p.Col));
            g.FillPolygon(b, pts);
        }
    }
}

// 17. Agujero de gusano.
internal sealed class WormholeEffect : ClickEffectBase
{
    private float _x, _y, _life, _rot;
    public override string Name => "Agujero de Gusano";
    public override void Trigger(float x, float y, Color[] palette)
    {
        _x = x; _y = y; _life = 1f; _rot = 0;
        for (int i = 0; i < 24; i++) // partículas succionadas
        {
            float a = Random.Shared.NextSingle() * MathF.PI * 2;
            float r = 60 + Random.Shared.NextSingle() * 90;
            Add(x + MathF.Cos(a) * r, y + MathF.Sin(a) * r, 0, 0, 0.9f, 2.5f,
                Pick(palette, i, Color.FromArgb(200, 160, 255)));
        }
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        _life = Math.Max(0, _life - dt);
        _rot += dt * 5;
        // Succión: todo tiende al centro.
        for (int i = 0; i < Parts.Count; i++)
        {
            var p = Parts[i];
            float dx = _x - p.X, dy = _y - p.Y;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > 4)
            {
                p.Vx += dx / d * 700 * dt;
                p.Vy += dy / d * 700 * dt;
                Parts[i] = p;
            }
        }
    }
    public override bool HasActivity => base.HasActivity || _life > 0;
    public override void Draw(Graphics g)
    {
        // Espiral logarítmica con DrawCurve.
        if (_life > 0)
        {
            float t = _life;
            var pts = new List<PointF>();
            for (int k = 0; k <= 40; k++)
            {
                float a = _rot + k * 0.35f;
                float r = (4 + k * 3.2f) * BurstScale;
                pts.Add(new PointF(_x + MathF.Cos(a) * r, _y + MathF.Sin(a) * r));
            }
            using var pen = new Pen(Color.FromArgb((int)(200 * t), Color.FromArgb(190, 140, 255)), 2);
            g.DrawCurve(pen, pts.ToArray(), 0.5f);
        }
        foreach (var p in Parts)
        {
            float tt = p.Life / p.MaxLife;
            using var b = new SolidBrush(Color.FromArgb((int)(230 * tt), p.Col));
            g.FillEllipse(b, p.X - 2, p.Y - 2, 4, 4);
        }
    }
    public override RectangleF GetBounds()
    {
        var b = base.GetBounds();
        if (_life <= 0) return b;
        var m = RectangleF.FromLTRB(_x - 180 * BurstScale, _y - 180 * BurstScale, _x + 180 * BurstScale, _y + 180 * BurstScale);
        return b.IsEmpty ? m : RectangleF.Union(b, m);
    }
}

// 18. Confeti explosivo.
internal sealed class BoomConfettiEffect : ClickEffectBase
{
    public override string Name => "Confeti Explosivo";
    public override void Trigger(float x, float y, Color[] palette)
    {
        Friction = 0.9f; Gravity = 700;
        for (int i = 0; i < 34; i++)
        {
            float a = -MathF.PI / 2 + (Random.Shared.NextSingle() * 2 - 1) * 1.2f;
            float sp = 200 + Random.Shared.NextSingle() * 420;
            Add(x, y, MathF.Cos(a) * sp, MathF.Sin(a) * sp, 1.1f,
                4 + Random.Shared.NextSingle() * 5,
                Pick(palette, i, TrailContext.FromHsv(Random.Shared.Next(360), 0.9f, 1f)),
                spin: (Random.Shared.NextSingle() * 2 - 1) * 10);
        }
    }
    public override void Draw(Graphics g)
    {
        foreach (var p in Parts)
        {
            float t = p.Life / p.MaxLife;
            float w = Math.Max(1.5f, p.Size * Math.Abs(MathF.Cos(p.Rot)));
            float h = Math.Max(1.5f, p.Size * Math.Abs(MathF.Sin(p.Rot * 0.7f)));
            using var b = new SolidBrush(Color.FromArgb((int)(240 * t), p.Col));
            g.FillRectangle(b, p.X - w / 2, p.Y - w / 2, w, h);
        }
    }
}

// 19. Anomalía magnética.
internal sealed class MagneticEffect : ClickEffectBase
{
    private float _x, _y, _life;
    public override string Name => "Anomalía Magnética";
    public override void Trigger(float x, float y, Color[] palette)
    {
        _x = x; _y = y; _life = 0.9f;
        for (int i = 0; i < 20; i++)
        {
            float a = Random.Shared.NextSingle() * MathF.PI * 2;
            float r = 50 + Random.Shared.NextSingle() * 90;
            Add(x + MathF.Cos(a) * r, y + MathF.Sin(a) * r, 0, 0, 0.85f, 2,
                Pick(palette, i, Color.FromArgb(150, 210, 255)));
        }
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        _life = Math.Max(0, _life - dt);
    }
    public override bool HasActivity => base.HasActivity || _life > 0;
    public override void Draw(Graphics g)
    {
        if (_life > 0)
        {
            float t = _life / 0.9f;
            // Líneas de campo: arcos que se curvan hacia el centro.
            using var pen = new Pen(Color.FromArgb((int)(170 * t), Color.FromArgb(150, 200, 255)), 1.5f);
            for (int k = 0; k < 10; k++)
            {
                float a0 = k / 10f * MathF.PI * 2;
                float r0 = 110 * BurstScale * (1.1f - t * 0.5f);
                var pts = new List<PointF>();
                for (int s = 0; s <= 12; s++)
                {
                    float f = s / 12f;
                    float ang = a0 + f * 1.1f;
                    float rr = r0 * (1 - f * 0.85f);
                    pts.Add(new PointF(_x + MathF.Cos(ang) * rr, _y + MathF.Sin(ang) * rr));
                }
                g.DrawCurve(pen, pts.ToArray(), 0.5f);
            }
        }
        foreach (var p in Parts)
        {
            float t = p.Life / p.MaxLife;
            using var b = new SolidBrush(Color.FromArgb((int)(220 * t), p.Col));
            g.FillEllipse(b, p.X - 2, p.Y - 2, 4, 4);
        }
    }
    public override RectangleF GetBounds()
    {
        var b = base.GetBounds();
        if (_life <= 0) return b;
        var m = RectangleF.FromLTRB(_x - 130 * BurstScale, _y - 130 * BurstScale, _x + 130 * BurstScale, _y + 130 * BurstScale);
        return b.IsEmpty ? m : RectangleF.Union(b, m);
    }
}

// 20. Radar.
internal sealed class RadarEffect : ClickEffectBase
{
    private float _x, _y, _life, _sweep;
    public override string Name => "Radar";
    public override void Trigger(float x, float y, Color[] palette)
    {
        _x = x; _y = y; _life = 1.2f; _sweep = 0;
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        if (_life > 0)
        {
            _life -= dt;
            _sweep += dt * MathF.PI * 2 / 1.2f; // una vuelta completa
        }
    }
    public override bool HasActivity => base.HasActivity || _life > 0;
    public override void Draw(Graphics g)
    {
        if (_life <= 0) return;
        float t = _life / 1.2f;
        float r = 70 * BurstScale;
        Color col = Color.FromArgb(80, 255, 160);
        using var ring = new Pen(Color.FromArgb((int)(200 * t), col), 2 * BurstScale);
        g.DrawEllipse(ring, _x - r, _y - r, r * 2, r * 2);
        using var ring2 = new Pen(Color.FromArgb((int)(120 * t), col), 1);
        g.DrawEllipse(ring2, _x - r / 2, _y - r / 2, r, r);
        float ex = _x + MathF.Cos(_sweep) * r, ey = _y + MathF.Sin(_sweep) * r;
        using var sweep = new Pen(Color.FromArgb((int)(240 * t), Color.FromArgb(160, 255, 190)), 2.5f * BurstScale);
        g.DrawLine(sweep, _x, _y, ex, ey);
        using var dot = new SolidBrush(Color.FromArgb((int)(220 * t), col));
        g.FillEllipse(dot, _x - 3, _y - 3, 6, 6);
    }
    public override RectangleF GetBounds()
    {
        var b = base.GetBounds();
        if (_life <= 0) return b;
        var m = RectangleF.FromLTRB(_x - 80 * BurstScale, _y - 80 * BurstScale, _x + 80 * BurstScale, _y + 80 * BurstScale);
        return b.IsEmpty ? m : RectangleF.Union(b, m);
    }
}

/// <summary>Motor de ráfagas: un efecto activo, conmutación en vivo.</summary>
public sealed class ClickEngine : IDisposable
{
    private IClickEffect _active = ClickEffectFactory.Create(0);
    private int _index;
    private bool _disposed;

    public int SelectedIndex
    {
        get => _index;
        set
        {
            value = Math.Clamp(value, 0, ClickEffectFactory.Names.Length - 1);
            if (value == _index && _active is not null) return;
            _index = value;
            try { _active?.Dispose(); } catch { }
            _active = ClickEffectFactory.Create(value);
        }
    }

    public bool Enabled { get; set; }
    public string CurrentName => ClickEffectFactory.Names[_index];
    /// <summary>Multiplicador de tamaño (slider de clic). Se inyecta al efecto.</summary>
    public float SizeScale { get; set; } = 1f;
    /// <summary>Si true, el estallido usa TrailPalette; si no, nativos.</summary>
    public bool UseTrailPalette { get; set; } = true;
    public Color[] TrailPalette { get; set; }

    public void Trigger(float x, float y)
    {
        Trigger(x, y, UseTrailPalette ? TrailPalette : null);
    }

    public void Trigger(float x, float y, Color[] palette)
    {
        if (!Enabled || _disposed) return;
        try
        {
            _active.BurstScale = SizeScale;
            _active.Trigger(x, y, palette);
        }
        catch { }
    }

    public void Update(float dt)
    {
        if (!Enabled) return;
        try { _active.Update(dt); }
        catch { }
    }

    public void Draw(Graphics g)
    {
        if (!Enabled) return;
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        try { _active.Draw(g); }
        catch { }
        finally { g.SmoothingMode = old; }
    }

    public bool HasActivity => Enabled && !_disposed && _active.HasActivity;

    public RectangleF GetBounds() =>
        Enabled && !_disposed ? _active.GetBounds() : RectangleF.Empty;

    public void Clear()
    {
        try
        {
            _active?.Dispose();
            _active = ClickEffectFactory.Create(_index);
        }
        catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _active?.Dispose(); } catch { }
    }
}
