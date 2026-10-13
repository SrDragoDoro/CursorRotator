using System.Drawing.Drawing2D;

namespace CursorRotator.Core.Trails;

/// <summary>Estilos avanzados: física y matemáticas vectoriales sobre el historial.</summary>
internal static class AdvancedStyleFactory
{
    public static ITrailStyle Create(TrailPreset preset) => preset switch
    {
        TrailPreset.Orbital => new OrbitalStyle(),
        TrailPreset.Dna => new DnaStyle(),
        TrailPreset.Tesla => new TeslaStyle(),
        TrailPreset.Swarm => new SwarmStyle(),
        TrailPreset.Ripples => new RippleStyle(),
        TrailPreset.HackerRain => new HackerRainStyle(),
        TrailPreset.Metaballs => new MetaballsStyle(),
        TrailPreset.LightRibbon => new RibbonStyle(),
        TrailPreset.Confetti => new ConfettiStyle(),
        TrailPreset.MatrixGrid => new MatrixGridStyle(),
        TrailPreset.Singularity => new SingularityStyle(),
        TrailPreset.DoublePendulum => new PendulumStyle(),
        TrailPreset.Wisp => new WispStyle(),
        TrailPreset.Web => new WebStyle(),
        TrailPreset.Lorenz => new LorenzStyle(),
        TrailPreset.Ferrofluid => new FerrofluidStyle(),
        TrailPreset.Moebius => new MoebiusStyle(),
        TrailPreset.GoldenSpiral => new GoldenSpiralStyle(),
        TrailPreset.Constellation => new ConstellationStyle(),
        TrailPreset.DragonBreath => new DragonBreathStyle(),
        TrailPreset.TacticalLaser => new TacticalLaserStyle(),
        TrailPreset.HexDump => new HexDumpStyle(),
        TrailPreset.HologramRgb => new HologramStyle(),
        TrailPreset.Monarchs => new MonarchStyle(),
        TrailPreset.DarkFog => new DarkFogStyle(),
        TrailPreset.Kaleidoscope => new KaleidoscopeStyle(),
        TrailPreset.Retro8Bit => new RetroStyle(),
        TrailPreset.Blockchain => new BlockchainStyle(),
        TrailPreset.Blizzard => new BlizzardStyle(),
        TrailPreset.VibratingString => new StringStyle(),
        TrailPreset.Bioluminescence => new BioStyle(),
        TrailPreset.WhiteHoles => new WhiteHolesStyle(),
        _ => new OrbitalStyle(),
    };
}

// ---------- 9. Órbita gravitacional ----------
internal sealed class OrbitalStyle : TrailStyleBase
{
    private struct Electron { public float Angle, Radius, Speed, Size, Life, MaxLife, Hue; }
    private readonly List<Electron> _e = new();

    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        int n = ctx.ConsumeEmission(dist > 30 ? 3 : 2);
        for (int i = 0; i < n; i++)
        {
            // Refresco circular: al llenar, reemplazar al más viejo para
            // mantener la población joven (evita envejecimiento sincronizado).
            if (_e.Count >= ctx.ScaledCap(24)) _e.RemoveAt(0);
            float maxLife = 1.1f * ctx.Options.LifeScale;
            _e.Add(new Electron
            {
                Angle = Random.Shared.NextSingle() * MathF.PI * 2,
                Radius = (16 + Random.Shared.NextSingle() * 30) * ctx.Options.SizeScale,
                Speed = (2 + Random.Shared.NextSingle() * 4) * (Random.Shared.Next(2) == 0 ? 1 : -1),
                Size = (4 + Random.Shared.NextSingle() * 3) * ctx.Options.SizeScale,
                Life = maxLife, MaxLife = maxLife,
                Hue = (ctx.Time * 60 + Random.Shared.Next(360)) % 360,
            });
        }
    }

    public override void Update(TrailContext ctx, float dt)
    {
        for (int i = _e.Count - 1; i >= 0; i--)
        {
            var e = _e[i];
            e.Life -= dt;
            if (e.Life <= 0) { _e.RemoveAt(i); continue; }
            e.Angle += e.Speed * dt;
            _e[i] = e;
        }
    }

    public override bool HasActivity(TrailContext ctx) => _e.Count > 0;
    public override void Reset() => _e.Clear();

    public override RectangleF GetBounds(TrailContext ctx)
    {
        bool any = false;
        float x0 = float.MaxValue, y0 = float.MaxValue;
        float x1 = float.MinValue, y1 = float.MinValue;
        foreach (var e in _e)
        {
            float ex = ctx.CursorX + MathF.Cos(e.Angle) * e.Radius;
            float ey = ctx.CursorY + MathF.Sin(e.Angle) * e.Radius;
            float r = Math.Max(6, e.Size * 2);
            if (ex - r < x0) x0 = ex - r;
            if (ey - r < y0) y0 = ey - r;
            if (ex + r > x1) x1 = ex + r;
            if (ey + r > y1) y1 = ey + r;
            any = true;
        }
        return any ? RectangleF.FromLTRB(x0, y0, x1, y1) : RectangleF.Empty;
    }

    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        foreach (var e in _e)
        {
            float t = e.Life / e.MaxLife;
            float ex = ctx.CursorX + MathF.Cos(e.Angle) * e.Radius;
            float ey = ctx.CursorY + MathF.Sin(e.Angle) * e.Radius;
            Color col = ctx.PalettePick((int)(e.Angle * 10), TrailContext.FromHsv(e.Hue, 0.8f, 1f));
            using var ring = new Pen(Color.FromArgb((int)(28 * t), col), 1);
            g.DrawEllipse(ring, ctx.CursorX - e.Radius, ctx.CursorY - e.Radius, e.Radius * 2, e.Radius * 2);
            float r = Math.Max(1.5f, e.Size * t);
            using var b = new SolidBrush(Color.FromArgb((int)(255 * t), Color.White));
            g.FillEllipse(b, ex - r, ey - r, r * 2, r * 2);
            using var glow = new SolidBrush(Color.FromArgb((int)(110 * t), col));
            g.FillEllipse(glow, ex - r * 2, ey - r * 2, r * 4, r * 4);
        }
    });
}

// ---------- 10. ADN / doble hélice ----------
internal sealed class DnaStyle : TrailStyleBase
{
    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist) { }
    public override void Update(TrailContext ctx, float dt) { }
    public override bool HasActivity(TrailContext ctx) =>
        ctx.History.Count >= 4 && ctx.Time - ctx.History[^1].T < 0.6f * ctx.Options.LifeScale;

    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        var h = ctx.History;
        if (h.Count < 4) return;
        float window = 0.55f * ctx.Options.LifeScale;
        float amp = 13 * ctx.Options.SizeScale;
        Color cA = ctx.BaseColor(Color.FromArgb(110, 230, 255));
        Color cB = ctx.Options.UseCursorColor && !ctx.Options.CursorColor.IsEmpty
            ? TrailContext.FromHsv(TrailContext.HueOf(ctx.Options.CursorColor) + 180, 0.85f, 1f)
            : Color.FromArgb(240, 130, 255);
        using var penA = new Pen(Color.Empty, Math.Max(1.5f, 2.6f * ctx.Options.SizeScale))
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var penB = new Pen(Color.Empty, penA.Width)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var rung = new Pen(Color.Empty, 1);
        PointF prevA = PointF.Empty, prevB = PointF.Empty;
        for (int i = 2; i < h.Count; i++)
        {
            float age = 1 - (ctx.Time - h[i].T) / window;
            if (age <= 0) { prevA = PointF.Empty; continue; }
            float dx = h[i].X - h[i - 2].X, dy = h[i].Y - h[i - 2].Y;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 1e-3) continue;
            float nx = -dy / len, ny = dx / len;
            float off = MathF.Sin(ctx.Time * 7 + i * 0.55f) * amp;
            var a = new PointF(h[i].X + nx * off, h[i].Y + ny * off);
            var b = new PointF(h[i].X - nx * off, h[i].Y - ny * off);
            if (!prevA.IsEmpty)
            {
                int al = (int)(230 * age);
                // Hebras con colores secuenciales de la paleta (o cian/magenta).
                penA.Color = Color.FromArgb(al, ctx.PalettePick(i, cA));
                penB.Color = Color.FromArgb(al, ctx.PalettePick(i + 1, cB));
                g.DrawLine(penA, prevA, a);
                g.DrawLine(penB, prevB, b);
                if (i % 3 == 0)
                {
                    rung.Color = Color.FromArgb((int)(110 * age), Color.White);
                    g.DrawLine(rung, a, b);
                }
            }
            prevA = a; prevB = b;
        }
    });
}

// ---------- 11. Tesla / relámpago ----------
internal sealed class TeslaStyle : TrailStyleBase
{
    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist) { }
    public override void Update(TrailContext ctx, float dt) { }
    public override bool HasActivity(TrailContext ctx) =>
        ctx.History.Count >= 2 && ctx.Time - ctx.History[^1].T < 0.6f * ctx.Options.LifeScale;

    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        var h = ctx.History;
        if (h.Count < 2) return;
        int from = Math.Max(0, h.Count - 9);
        var pts = new List<PointF>();
        for (int i = from; i < h.Count; i++)
            pts.Add(new PointF(h[i].X, h[i].Y));
        // Subdivisión de puntos medios con jitter (2 iteraciones).
        float amp = 9 * ctx.Options.SizeScale;
        for (int it = 0; it < 2; it++)
        {
            var sub = new List<PointF> { pts[0] };
            for (int i = 1; i < pts.Count; i++)
            {
                float mx = (pts[i - 1].X + pts[i].X) / 2;
                float my = (pts[i - 1].Y + pts[i].Y) / 2;
                float dx = pts[i].X - pts[i - 1].X, dy = pts[i].Y - pts[i - 1].Y;
                float len = MathF.Sqrt(dx * dx + dy * dy);
                if (len > 1e-3)
                {
                    float j = (Random.Shared.NextSingle() * 2 - 1) * amp;
                    mx += -dy / len * j;
                    my += dx / len * j;
                }
                sub.Add(new PointF(mx, my));
                sub.Add(pts[i]);
            }
            pts = sub;
            amp *= 0.5f;
        }
        var arr = pts.ToArray();
        float ageAll = ctx.History.Count > 0
            ? Math.Clamp(1 - (ctx.Time - ctx.History[^1].T) / (0.55f * ctx.Options.LifeScale), 0, 1)
            : 0;
        Color halo = ctx.BaseColor(Color.FromArgb(140, 210, 255));
        // Tronco principal con grosor decreciente hacia la cola.
        for (int i = 1; i < arr.Length; i++)
        {
            float fTail = 1 - i / (float)arr.Length; // 1 en cabeza → 0 en cola
            float wCore = Math.Max(0.8f, 3.2f * ctx.Options.SizeScale * (0.25f + 0.75f * fTail));
            using var pHalo = new Pen(Color.FromArgb((int)(70 * ageAll), halo), wCore * 2.6f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var pCore = new Pen(Color.FromArgb((int)(235 * ageAll), Color.White), wCore)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pHalo, arr[i - 1], arr[i]);
            g.DrawLine(pCore, arr[i - 1], arr[i]);
        }
        // Ramificaciones: arcos cortos que nacen de puntos medios del tronco.
        int branches = Math.Min(3, arr.Length / 6);
        for (int bIdx = 0; bIdx < branches; bIdx++)
        {
            int start = arr.Length / 2 + bIdx * 3;
            if (start >= arr.Length - 2) break;
            var bp = new List<PointF> { arr[start] };
            float bx = arr[start].X, by = arr[start].Y;
            float ang = Random.Shared.NextSingle() * MathF.PI * 2;
            float step = 9 * ctx.Options.SizeScale;
            for (int k = 0; k < 4; k++)
            {
                ang += (Random.Shared.NextSingle() * 2 - 1) * 0.9f;
                bx += MathF.Cos(ang) * step;
                by += MathF.Sin(ang) * step;
                bp.Add(new PointF(bx, by));
            }
            using var pBr = new Pen(Color.FromArgb((int)(170 * ageAll), halo),
                Math.Max(1, 1.4f * ctx.Options.SizeScale))
                { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(pBr, bp.ToArray());
        }
    });
}

// ---------- 12. Enjambre (Boids) ----------
internal sealed class SwarmStyle : TrailStyleBase
{
    private struct Boid { public float X, Y, Vx, Vy, Life, MaxLife; }
    private readonly List<Boid> _b = new();

    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        // Enjambre compacto: 1-2 por captura, tope 24 (~10-15 vivos típicos).
        int n = ctx.ConsumeEmission(dist > 30 ? 2 : 1);
        for (int i = 0; i < n; i++)
        {
            if (_b.Count >= ctx.ScaledCap(24)) _b.RemoveAt(0); // refresco circular
            float maxLife = 1.3f * ctx.Options.LifeScale;
            _b.Add(new Boid
            {
                X = x + (Random.Shared.NextSingle() * 2 - 1) * 20,
                Y = y + (Random.Shared.NextSingle() * 2 - 1) * 20,
                Vx = (Random.Shared.NextSingle() * 2 - 1) * 120,
                Vy = (Random.Shared.NextSingle() * 2 - 1) * 120,
                Life = maxLife, MaxLife = maxLife,
            });
        }
    }

    public override void Update(TrailContext ctx, float dt)
    {
        // Alineación media del enjambre.
        float avgVx = 0, avgVy = 0;
        foreach (var b in _b) { avgVx += b.Vx; avgVy += b.Vy; }
        if (_b.Count > 0) { avgVx /= _b.Count; avgVy /= _b.Count; }

        for (int i = _b.Count - 1; i >= 0; i--)
        {
            var b = _b[i];
            b.Life -= dt;
            if (b.Life <= 0) { _b.RemoveAt(i); continue; }
            // Cohesión hacia el cursor.
            float dx = ctx.CursorX - b.X, dy = ctx.CursorY - b.Y;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > 1)
            {
                float pull = Math.Min(900, 140 + d * 8);
                b.Vx += dx / d * pull * dt;
                b.Vy += dy / d * pull * dt;
            }
            // Separación entre vecinos.
            for (int j = 0; j < _b.Count; j++)
            {
                if (j == i) continue;
                var o = _b[j];
                float sx = b.X - o.X, sy = b.Y - o.Y;
                float sd = MathF.Sqrt(sx * sx + sy * sy);
                if (sd > 0.5f && sd < 26)
                {
                    b.Vx += sx / sd * 260 * dt;
                    b.Vy += sy / sd * 260 * dt;
                }
            }
            // Alineación.
            b.Vx += (avgVx - b.Vx) * 0.8f * dt;
            b.Vy += (avgVy - b.Vy) * 0.8f * dt;
            // Tope de velocidad.
            float sp = MathF.Sqrt(b.Vx * b.Vx + b.Vy * b.Vy);
            if (sp > 520) { b.Vx *= 520 / sp; b.Vy *= 520 / sp; }
            b.X += b.Vx * dt;
            b.Y += b.Vy * dt;
            _b[i] = b;
        }
    }

    public override bool HasActivity(TrailContext ctx) => _b.Count > 0;
    public override void Reset() => _b.Clear();

    public override RectangleF GetBounds(TrailContext ctx)
    {
        if (_b.Count == 0) return RectangleF.Empty;
        float x0 = float.MaxValue, y0 = float.MaxValue;
        float x1 = float.MinValue, y1 = float.MinValue;
        foreach (var b in _b)
        {
            if (b.X - 10 < x0) x0 = b.X - 10;
            if (b.Y - 10 < y0) y0 = b.Y - 10;
            if (b.X + 10 > x1) x1 = b.X + 10;
            if (b.Y + 10 > y1) y1 = b.Y + 10;
        }
        return RectangleF.FromLTRB(x0, y0, x1, y1);
    }

    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        foreach (var b in _b)
        {
            float t = b.Life / b.MaxLife;
            float ang = MathF.Atan2(b.Vy, b.Vx);
            float s = (4 + 3 * t) * ctx.Options.SizeScale;
            float ca = MathF.Cos(ang), sa = MathF.Sin(ang);
            var pts = new PointF[]
            {
                new(b.X + ca * s, b.Y + sa * s),
                new(b.X - ca * s * 0.7f - sa * s * 0.55f, b.Y - sa * s * 0.7f + ca * s * 0.55f),
                new(b.X - ca * s * 0.7f + sa * s * 0.55f, b.Y - sa * s * 0.7f - ca * s * 0.55f),
            };
            Color col = ctx.PalettePick((int)(b.X + b.Y * 2), Color.FromArgb(180, 255, 150));
            using var br = new SolidBrush(Color.FromArgb((int)(240 * t), col));
            g.FillPolygon(br, pts);
        }
    });
}

// ---------- 13. Ondas de choque (Ripples) ----------
internal sealed class RippleStyle : TrailStyleBase
{
    private struct Ripple { public float X, Y, R, Life, MaxLife; }
    private readonly List<Ripple> _r = new();
    private float _prevSpeed, _prevAng;
    private bool _hasPrev, _idleFired;

    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist) { }

    public override void Update(TrailContext ctx, float dt)
    {
        // Velocidad y giro desde el historial reciente; la parada brusca se
        // detecta por tiempo sin puntos nuevos (el historial se congela).
        var h = ctx.History;
        if (h.Count >= 3)
        {
            var b = h[^1];
            float idle = ctx.Time - b.T;
            if (idle > 0.12f)
            {
                // Quietud tras velocidad alta → anillo (una sola vez).
                if (!_idleFired && _prevSpeed > 350)
                    Emit(ctx);
                _idleFired = true;
            }
            else
            {
                _idleFired = false;
                var a = h[^3];
                float span = Math.Max(1e-3f, b.T - a.T);
                float vx = (b.X - a.X) / span, vy = (b.Y - a.Y) / span;
                float speed = MathF.Sqrt(vx * vx + vy * vy);
                float ang = MathF.Atan2(vy, vx);
                if (_hasPrev)
                {
                    float dAng = Math.Abs(ang - _prevAng);
                    if (dAng > MathF.PI) dAng = 2 * MathF.PI - dAng;
                    // Giro cerrado en marcha → anillo.
                    if (dAng > 1.7f && speed > 250)
                        Emit(ctx);
                }
                _prevSpeed = speed; _prevAng = ang; _hasPrev = true;
            }
        }
        for (int i = _r.Count - 1; i >= 0; i--)
        {
            var r = _r[i];
            r.Life -= dt;
            if (r.Life <= 0) { _r.RemoveAt(i); continue; }
            // Crecimiento exponencial amortiguado + alfa a cero.
            r.R += (r.R * 2.4f + 40 * ctx.Options.SizeScale) * dt;
            if (r.R > 170 * ctx.Options.SizeScale) r.Life = Math.Min(r.Life, 0.15f);
            _r[i] = r;
        }
    }

    private void Emit(TrailContext ctx)
    {
        if (_r.Count >= 8) return;
        _r.Add(new Ripple { X = ctx.CursorX, Y = ctx.CursorY, R = 5, Life = 0.9f, MaxLife = 0.9f });
    }

    public override bool HasActivity(TrailContext ctx) => _r.Count > 0;
    public override void Reset() { _r.Clear(); _hasPrev = false; _idleFired = false; }

    public override RectangleF GetBounds(TrailContext ctx)
    {
        if (_r.Count == 0) return RectangleF.Empty;
        float x0 = float.MaxValue, y0 = float.MaxValue;
        float x1 = float.MinValue, y1 = float.MinValue;
        foreach (var r in _r)
        {
            if (r.X - r.R < x0) x0 = r.X - r.R;
            if (r.Y - r.R < y0) y0 = r.Y - r.R;
            if (r.X + r.R > x1) x1 = r.X + r.R;
            if (r.Y + r.R > y1) y1 = r.Y + r.R;
        }
        return RectangleF.FromLTRB(x0, y0, x1, y1);
    }

    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        Color col = ctx.BaseColor(Color.FromArgb(150, 225, 255));
        foreach (var r in _r)
        {
            float t = r.Life / r.MaxLife;
            using var pen = new Pen(Color.FromArgb((int)(200 * t), col),
                Math.Max(1, 5 * t * ctx.Options.SizeScale));
            g.DrawEllipse(pen, r.X - r.R, r.Y - r.R, r.R * 2, r.R * 2);
        }
    });
}

// ---------- 14. Matriz de datos (Hacker Rain) ----------
internal sealed class HackerRainStyle : TrailStyleBase
{
    private const string Glyphs = "01ABCDEF$#@%&XYZ01アイ";
    private Font _font;
    private float _fontScale = -1;

    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        int n = ctx.ConsumeEmission(dist > 30 ? 3 : 2);
        for (int i = 0; i < n && ctx.Particles.Count < 60; i++)
        {
            float maxLife = 0.9f * ctx.Options.LifeScale;
            ctx.Spawn(x + (Random.Shared.NextSingle() * 2 - 1) * 14,
                y + (Random.Shared.NextSingle() * 2 - 1) * 6,
                (Random.Shared.NextSingle() * 2 - 1) * 30,
                130 + Random.Shared.NextSingle() * 170, maxLife,
                (7 + Random.Shared.NextSingle() * 4) * ctx.Options.SizeScale, 0,
                Glyphs[Random.Shared.Next(Glyphs.Length)]);
        }
    }

    public override void Update(TrailContext ctx, float dt) => Integrate(ctx, dt, extraGravityY: 300f);

    public override void Draw(TrailContext ctx, Graphics g)
    {
        float want = 9 * ctx.Options.SizeScale;
        if (_font is null || Math.Abs(_fontScale - want) > 0.5f)
        {
            _font?.Dispose();
            _font = new Font("Consolas", want, FontStyle.Bold, GraphicsUnit.Pixel);
            _fontScale = want;
        }
        Color col = ctx.BaseColor(Color.FromArgb(60, 255, 120));
        foreach (var p in ctx.Particles)
        {
            float t = p.Life / p.MaxLife;
            Color tail = ctx.PalettePick(p.Seed, col);
            using var b = new SolidBrush(Color.FromArgb((int)(235 * t), tail));
            g.DrawString(p.Glyph.ToString(), _font, b, p.X, p.Y);
        }
    }

    public override void Dispose() { _font?.Dispose(); _font = null; }
}

// ---------- 15. Metaballs (goo) ----------
internal sealed class MetaballsStyle : TrailStyleBase
{
    private struct Blob { public float X, Y, Vx, Vy, R, Life, MaxLife; }
    private readonly List<Blob> _b = new();

    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        int n = ctx.ConsumeEmission(dist > 30 ? 3 : 2);
        for (int i = 0; i < n; i++)
        {
            if (_b.Count >= ctx.ScaledCap(30)) _b.RemoveAt(0); // refresco circular
            float maxLife = 1.0f * ctx.Options.LifeScale;
            _b.Add(new Blob
            {
                X = x + (Random.Shared.NextSingle() * 2 - 1) * 8,
                Y = y + (Random.Shared.NextSingle() * 2 - 1) * 8,
                Vx = (Random.Shared.NextSingle() * 2 - 1) * 60,
                Vy = (Random.Shared.NextSingle() * 2 - 1) * 60,
                R = (10 + Random.Shared.NextSingle() * 14) * ctx.Options.SizeScale,
                Life = maxLife, MaxLife = maxLife,
            });
        }
    }

    public override void Update(TrailContext ctx, float dt)
    {
        for (int i = _b.Count - 1; i >= 0; i--)
        {
            var b = _b[i];
            b.Life -= dt;
            if (b.Life <= 0) { _b.RemoveAt(i); continue; }
            b.Vx *= MathF.Max(0, 1 - 1.5f * dt);
            b.Vy *= MathF.Max(0, 1 - 1.5f * dt);
            b.Vy += 120 * dt; // goteo
            b.X += b.Vx * dt;
            b.Y += b.Vy * dt;
            _b[i] = b;
        }
    }

    public override bool HasActivity(TrailContext ctx) => _b.Count > 0;
    public override void Reset() => _b.Clear();

    public override RectangleF GetBounds(TrailContext ctx)
    {
        if (_b.Count == 0) return RectangleF.Empty;
        float x0 = float.MaxValue, y0 = float.MaxValue;
        float x1 = float.MinValue, y1 = float.MinValue;
        foreach (var b in _b)
        {
            float r = b.R * 1.9f;
            if (b.X - r < x0) x0 = b.X - r;
            if (b.Y - r < y0) y0 = b.Y - r;
            if (b.X + r > x1) x1 = b.X + r;
            if (b.Y + r > y1) y1 = b.Y + r;
        }
        return RectangleF.FromLTRB(x0, y0, x1, y1);
    }

    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        foreach (var b in _b)
        {
            float t = b.Life / b.MaxLife;
            Color col = ctx.PalettePick((int)(b.X - b.Y), Color.FromArgb(90, 230, 200));
            using var outer = new SolidBrush(Color.FromArgb((int)(50 * t), col));
            g.FillEllipse(outer, b.X - b.R * 1.9f, b.Y - b.R * 1.9f, b.R * 3.8f, b.R * 3.8f);
            using var inner = new SolidBrush(Color.FromArgb((int)(225 * t), col));
            g.FillEllipse(inner, b.X - b.R, b.Y - b.R, b.R * 2, b.R * 2);
        }
    });
}

// ---------- 16. Sable de luz (Light Ribbon) ----------
internal sealed class RibbonStyle : TrailStyleBase
{
    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist) { }
    public override void Update(TrailContext ctx, float dt) { }
    public override bool HasActivity(TrailContext ctx) =>
        ctx.History.Count >= 4 && ctx.Time - ctx.History[^1].T < 0.6f * ctx.Options.LifeScale;

    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        var h = ctx.History;
        if (h.Count < 4) return;
        float window = 0.55f * ctx.Options.LifeScale;
        float maxW = 11 * ctx.Options.SizeScale;
        Color col = ctx.BaseColor(Color.FromArgb(200, 240, 255));
        // Halo ancho + núcleo: dos pasadas.
        for (int pass = 0; pass < 2; pass++)
        {
            float wMul = pass == 0 ? 2.2f : 1f;
            for (int i = 1; i < h.Count; i++)
            {
                float age = 1 - (ctx.Time - h[i].T) / window;
                if (age <= 0) continue;
                // La cabeza (reciente) es ancha, la cola estrecha hasta un punto.
                float fNew = i / (float)(h.Count - 1);
                float wdt = maxW * wMul * MathF.Pow(fNew, 1.6f);
                float dx = h[i].X - h[i - 1].X, dy = h[i].Y - h[i - 1].Y;
                float len = MathF.Sqrt(dx * dx + dy * dy);
                if (len < 1e-3) continue;
                float nx = -dy / len * wdt / 2, ny = dx / len * wdt / 2;
                var quad = new PointF[]
                {
                    new(h[i-1].X + nx, h[i-1].Y + ny), new(h[i-1].X - nx, h[i-1].Y - ny),
                    new(h[i].X - nx, h[i].Y - ny), new(h[i].X + nx, h[i].Y + ny),
                };
                // El ancho del borde previo se aproxima con el actual (continuidad visual).
                using var br = new SolidBrush(Color.FromArgb(
                    (int)(230 * age * (pass == 0 ? 0.30f : 1f)),
                    pass == 0 ? col : Color.White));
                g.FillPolygon(br, quad);
            }
        }
    });
}

// ---------- 17. Confeti dinámico ----------
internal sealed class ConfettiStyle : TrailStyleBase
{
    private struct Piece
    {
        public float X, Y, Vx, Vy, Rot, Spin, W, H, Life, MaxLife, Hue;
        public int Kind; // 0 rect, 1 triángulo
    }
    private readonly List<Piece> _p = new();

    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        int n = ctx.ConsumeEmission(dist > 30 ? 3 : 2);
        for (int i = 0; i < n; i++)
        {
            if (_p.Count >= ctx.ScaledCap(44)) _p.RemoveAt(0); // refresco circular
            float maxLife = 1.1f * ctx.Options.LifeScale;
            _p.Add(new Piece
            {
                X = x + (Random.Shared.NextSingle() * 2 - 1) * 10,
                Y = y + (Random.Shared.NextSingle() * 2 - 1) * 10,
                Vx = vx * 0.25f + (Random.Shared.NextSingle() * 2 - 1) * 90,
                Vy = vy * 0.25f - Random.Shared.NextSingle() * 60,
                Rot = Random.Shared.NextSingle() * MathF.PI * 2,
                Spin = (Random.Shared.NextSingle() * 2 - 1) * 9,
                W = (5 + Random.Shared.NextSingle() * 6) * ctx.Options.SizeScale,
                H = (4 + Random.Shared.NextSingle() * 5) * ctx.Options.SizeScale,
                Life = maxLife, MaxLife = maxLife,
                Hue = Random.Shared.Next(360),
                Kind = Random.Shared.Next(2),
            });
        }
    }

    public override void Update(TrailContext ctx, float dt)
    {
        for (int i = _p.Count - 1; i >= 0; i--)
        {
            var p = _p[i];
            p.Life -= dt;
            if (p.Life <= 0) { _p.RemoveAt(i); continue; }
            p.Vy += 620 * dt; // gravedad
            p.Vx *= MathF.Max(0, 1 - 1.2f * dt);
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            p.Rot += p.Spin * dt;
            _p[i] = p;
        }
    }

    public override bool HasActivity(TrailContext ctx) => _p.Count > 0;
    public override void Reset() => _p.Clear();

    public override RectangleF GetBounds(TrailContext ctx)
    {
        if (_p.Count == 0) return RectangleF.Empty;
        float x0 = float.MaxValue, y0 = float.MaxValue;
        float x1 = float.MinValue, y1 = float.MinValue;
        foreach (var p in _p)
        {
            float r = Math.Max(p.W, p.H);
            if (p.X - r < x0) x0 = p.X - r;
            if (p.Y - r < y0) y0 = p.Y - r;
            if (p.X + r > x1) x1 = p.X + r;
            if (p.Y + r > y1) y1 = p.Y + r;
        }
        return RectangleF.FromLTRB(x0, y0, x1, y1);
    }

    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        foreach (var p in _p)
        {
            float t = p.Life / p.MaxLife;
            // Rotación 3D simulada: escala oscilante en X e Y (tumbling).
            float sx = Math.Abs(MathF.Cos(p.Rot));
            float sy = Math.Abs(MathF.Sin(p.Rot * 0.7f));
            float w = Math.Max(1, p.W * (0.25f + 0.75f * sx));
            float h = Math.Max(1, p.H * (0.25f + 0.75f * sy));
            float ca = MathF.Cos(p.Rot), sa = MathF.Sin(p.Rot);
            PointF Rot(float lx, float ly) => new(p.X + lx * ca - ly * sa, p.Y + lx * sa + ly * ca);
            Color col;
            if (ctx.Options.CustomPalette is { Length: > 0 })
                col = ctx.PalettePick((int)(p.X + p.Y * 3), TrailContext.FromHsv(p.Hue, 0.9f, 1f));
            else if (ctx.Options.UseCursorColor && !ctx.Options.CursorColor.IsEmpty)
                col = TrailContext.FromHsv(TrailContext.HueOf(ctx.Options.CursorColor), 0.9f, 0.45f + 0.55f * t);
            else
                col = TrailContext.FromHsv(p.Hue, 0.9f, 1f);
            using var br = new SolidBrush(Color.FromArgb((int)(240 * t), col));
            if (p.Kind == 0)
                g.FillPolygon(br, [Rot(-w / 2, -h / 2), Rot(w / 2, -h / 2), Rot(w / 2, h / 2), Rot(-w / 2, h / 2)]);
            else
                g.FillPolygon(br, [Rot(0, -h / 2), Rot(w / 2, h / 2), Rot(-w / 2, h / 2)]);
        }
    });
}

// ---------- 18. Matriz cuadriculada ----------
internal sealed class MatrixGridStyle : TrailStyleBase
{
    private const string Glyphs = "0123456789ABCDEF$#@%&";
    private const float ColW = 14f;
    private struct Drop { public float X, Y, Speed, Life, MaxLife; public char Glyph; }
    private readonly List<Drop> _d = new();
    private Font _font;
    private float _fontScale = -1;

    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        // Columna bloqueada a la cuadrícula virtual de 14 px.
        if (ctx.ConsumeEmission(1) == 0) return;
        float colX = MathF.Round(x / ColW) * ColW;
        if (_d.Count >= ctx.ScaledCap(60)) _d.RemoveAt(0); // refresco circular
        float maxLife = 1.1f * ctx.Options.LifeScale;
        _d.Add(new Drop
        {
            X = colX, Y = y,
            Speed = 150 + Random.Shared.NextSingle() * 200,
            Life = maxLife, MaxLife = maxLife,
            Glyph = Glyphs[Random.Shared.Next(Glyphs.Length)],
        });
    }

    public override void Update(TrailContext ctx, float dt)
    {
        for (int i = _d.Count - 1; i >= 0; i--)
        {
            var d = _d[i];
            d.Life -= dt;
            if (d.Life <= 0) { _d.RemoveAt(i); continue; }
            d.Y += d.Speed * dt;
            _d[i] = d;
        }
    }

    public override bool HasActivity(TrailContext ctx) => _d.Count > 0;
    public override void Reset() => _d.Clear();

    public override RectangleF GetBounds(TrailContext ctx)
    {
        if (_d.Count == 0) return RectangleF.Empty;
        float x0 = float.MaxValue, y0 = float.MaxValue;
        float x1 = float.MinValue, y1 = float.MinValue;
        foreach (var d in _d)
        {
            if (d.X - 14 < x0) x0 = d.X - 14;
            if (d.Y - 14 < y0) y0 = d.Y - 14;
            if (d.X + 14 > x1) x1 = d.X + 14;
            if (d.Y + 64 > y1) y1 = d.Y + 64; // incluye el rastro
        }
        return RectangleF.FromLTRB(x0, y0, x1, y1);
    }

    public override void Draw(TrailContext ctx, Graphics g)
    {
        float want = 11 * ctx.Options.SizeScale;
        if (_font is null || Math.Abs(_fontScale - want) > 0.5f)
        {
            _font?.Dispose();
            _font = new Font("Consolas", want, FontStyle.Bold, GraphicsUnit.Pixel);
            _fontScale = want;
        }
        Color col = ctx.BaseColor(Color.FromArgb(70, 255, 130));
        float lineH = want * 1.15f;
        foreach (var d in _d)
        {
            float t = d.Life / d.MaxLife;
            // Cabeza brillante + rastro degradado de 3 glifos.
            using var head = new SolidBrush(Color.FromArgb((int)(245 * t), Color.White));
            g.DrawString(d.Glyph.ToString(), _font, head, d.X, d.Y);
            for (int k = 1; k <= 3; k++)
            {
                float tt = t * (1 - k / 4f);
                if (tt <= 0.02f) continue;
                using var tail = new SolidBrush(Color.FromArgb((int)(200 * tt), col));
                g.DrawString(d.Glyph.ToString(), _font, tail, d.X, d.Y - k * lineH);
            }
        }
    }

    public override void Dispose() { _font?.Dispose(); _font = null; }
}

// ---------- 19. Agujero negro (Singularity) ----------
internal sealed class SingularityStyle : TrailStyleBase
{
    private struct Mote { public float X, Y, Vx, Vy, Life, MaxLife, Size; public int Seed; }
    private readonly List<Mote> _m = new();

    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        int n = ctx.ConsumeEmission(dist > 30 ? 3 : 2);
        for (int i = 0; i < n; i++)
        {
            if (_m.Count >= ctx.ScaledCap(40)) _m.RemoveAt(0);
            float a = Random.Shared.NextSingle() * MathF.PI * 2;
            float r = (120 + Random.Shared.NextSingle() * 110) * ctx.Options.SizeScale;
            float maxLife = 0.9f * ctx.Options.LifeScale;
            _m.Add(new Mote
            {
                X = x + MathF.Cos(a) * r, Y = y + MathF.Sin(a) * r,
                Vx = 0, Vy = 0, Life = maxLife, MaxLife = maxLife,
                Size = (4 + Random.Shared.NextSingle() * 4) * ctx.Options.SizeScale,
                Seed = Random.Shared.Next(1000),
            });
        }
    }

    public override void Update(TrailContext ctx, float dt)
    {
        for (int i = _m.Count - 1; i >= 0; i--)
        {
            var m = _m[i];
            m.Life -= dt;
            float dx = ctx.CursorX - m.X, dy = ctx.CursorY - m.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (m.Life <= 0 || dist < 9)
            {
                // Absorbida: fundido rápido en lugar de corte.
                m.Life -= dt * 8;
                if (m.Life <= 0 || dist < 4) { _m.RemoveAt(i); continue; }
            }
            if (dist > 0.5f)
            {
                // Aceleración exponencial hacia el centro (gravedad del pozo).
                float acc = 900 + 5200 / (1 + dist / 22);
                m.Vx += dx / dist * acc * dt;
                m.Vy += dy / dist * acc * dt;
                float sp = MathF.Sqrt(m.Vx * m.Vx + m.Vy * m.Vy);
                if (sp > 4000) { m.Vx *= 4000 / sp; m.Vy *= 4000 / sp; }
            }
            m.X += m.Vx * dt;
            m.Y += m.Vy * dt;
            _m[i] = m;
        }
    }

    public override bool HasActivity(TrailContext ctx) => _m.Count > 0;
    public override void Reset() => _m.Clear();

    public override RectangleF GetBounds(TrailContext ctx)
    {
        if (_m.Count == 0) return RectangleF.Empty;
        float x0 = float.MaxValue, y0 = float.MaxValue;
        float x1 = float.MinValue, y1 = float.MinValue;
        foreach (var m in _m)
        {
            float r = m.Size * 3;
            if (m.X - r < x0) x0 = m.X - r;
            if (m.Y - r < y0) y0 = m.Y - r;
            if (m.X + r > x1) x1 = m.X + r;
            if (m.Y + r > y1) y1 = m.Y + r;
        }
        if (ctx.CursorX - 12 < x0) x0 = ctx.CursorX - 12;
        if (ctx.CursorY - 12 < y0) y0 = ctx.CursorY - 12;
        if (ctx.CursorX + 12 > x1) x1 = ctx.CursorX + 12;
        if (ctx.CursorY + 12 > y1) y1 = ctx.CursorY + 12;
        return RectangleF.FromLTRB(x0, y0, x1, y1);
    }

    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        // Núcleo solo mientras haya motas: apagado total en reposo.
        if (_m.Count == 0) return;
        foreach (var m in _m)
        {
            float t = Math.Clamp(m.Life / m.MaxLife, 0, 1);
            Color col = ctx.PalettePick(m.Seed, Color.FromArgb(190, 120, 255));
            float tx = m.X - m.Vx * 0.035f, ty = m.Y - m.Vy * 0.035f;
            using var streak = new Pen(Color.FromArgb((int)(140 * t), col),
                Math.Max(1, m.Size * 0.5f))
                { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.DrawLine(streak, tx, ty, m.X, m.Y);
            float r = Math.Max(1.2f, m.Size * 0.6f * t + 0.8f);
            using var head = new SolidBrush(Color.FromArgb((int)(240 * t), Color.White));
            g.FillEllipse(head, m.X - r, m.Y - r, r * 2, r * 2);
        }
        using var ring = new Pen(Color.FromArgb(150, Color.FromArgb(220, 150, 255)), 2);
        g.DrawEllipse(ring, ctx.CursorX - 10, ctx.CursorY - 10, 20, 20);
        using var hole = new SolidBrush(Color.FromArgb(220, Color.Black));
        g.FillEllipse(hole, ctx.CursorX - 7, ctx.CursorY - 7, 14, 14);
    });
}

// ---------- 20. Péndulo doble (Caos) ----------
internal sealed class PendulumStyle : TrailStyleBase
{
    private float _th1 = 2.2f, _th2 = 2.6f, _om1, _om2;
    private readonly List<(float X, float Y, float T)> _trail = new();

    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist) { }

    public override void Update(TrailContext ctx, float dt)
    {
        float L1 = 42 * ctx.Options.SizeScale, L2 = 58 * ctx.Options.SizeScale;
        const float g = 1400f;
        bool fresh = ctx.History.Count > 0 && ctx.Time - ctx.History[^1].T < 2f;
        float step = Math.Min(dt, 0.033f) / 2;
        for (int s = 0; s < 2; s++)
        {
            float d = _th1 - _th2;
            float den = 2 - MathF.Cos(2 * _th1 - 2 * _th2);
            if (Math.Abs(den) < 1e-4) den = 1e-4f;
            float a1 = (-g * 2 * MathF.Sin(_th1) - g * MathF.Sin(_th1 - 2 * _th2)
                - 2 * MathF.Sin(d) * (_om2 * _om2 * L2 + _om1 * _om1 * L1 * MathF.Cos(d)))
                / (L1 * den);
            float a2 = (2 * MathF.Sin(d)
                * (_om1 * _om1 * L1 * 2 + g * 2 * MathF.Cos(_th1) + _om2 * _om2 * L2 * MathF.Cos(d)))
                / (L2 * den);
            _om1 += a1 * step;
            _om2 += a2 * step;
            _om1 = Math.Clamp(_om1, -60, 60); // tope anti-divergencia
            _om2 = Math.Clamp(_om2, -60, 60);
            float damp = fresh ? 0.12f : 6f;
            _om1 *= MathF.Max(0, 1 - damp * step);
            _om2 *= MathF.Max(0, 1 - damp * step);
            _th1 += _om1 * step;
            _th2 += _om2 * step;
        }
        float b2x = ctx.CursorX + L1 * MathF.Sin(_th1) + L2 * MathF.Sin(_th2);
        float b2y = ctx.CursorY + L1 * MathF.Cos(_th1) + L2 * MathF.Cos(_th2);
        // La estela solo registra con movimiento fresco: si no, se apaga en reposo.
        if (fresh)
        {
            _trail.Add((b2x, b2y, ctx.Time));
            while (_trail.Count > 70) _trail.RemoveAt(0);
        }
        while (_trail.Count > 0 && ctx.Time - _trail[0].T > 1.2f * ctx.Options.LifeScale)
            _trail.RemoveAt(0);
    }

    public override bool HasActivity(TrailContext ctx) =>
        _trail.Count >= 2 && ctx.Time - _trail[^1].T < 0.8f * ctx.Options.LifeScale;

    public override void Reset()
    {
        _trail.Clear();
        _th1 = 2.2f; _th2 = 2.6f; _om1 = _om2 = 0;
    }

    public override RectangleF GetBounds(TrailContext ctx)
    {
        float reach = 110 * ctx.Options.SizeScale;
        return RectangleF.FromLTRB(ctx.CursorX - reach, ctx.CursorY - reach,
            ctx.CursorX + reach, ctx.CursorY + reach);
    }

    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        float L1 = 42 * ctx.Options.SizeScale, L2 = 58 * ctx.Options.SizeScale;
        bool show = _trail.Count >= 2 &&
            ctx.Time - _trail[^1].T < 0.8f * ctx.Options.LifeScale;
        if (!show) return; // en reposo no hay varillas ni bola: apagado total
        float b1x = ctx.CursorX + L1 * MathF.Sin(_th1);
        float b1y = ctx.CursorY + L1 * MathF.Cos(_th1);
        float b2x = ctx.CursorX + L1 * MathF.Sin(_th1) + L2 * MathF.Sin(_th2);
        float b2y = ctx.CursorY + L1 * MathF.Cos(_th1) + L2 * MathF.Cos(_th2);
        Color col = ctx.PalettePick(0, Color.FromArgb(120, 220, 255));
        using var rod = new Pen(Color.FromArgb(140, col), 2);
        g.DrawLine(rod, ctx.CursorX, ctx.CursorY, b1x, b1y);
        g.DrawLine(rod, b1x, b1y, b2x, b2y);
        float window = 1.2f * ctx.Options.LifeScale;
        using var pen = new Pen(Color.Empty, Math.Max(1.5f, 2.6f * ctx.Options.SizeScale))
            { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        for (int i = 1; i < _trail.Count; i++)
        {
            float age = 1 - (ctx.Time - _trail[i].T) / window;
            if (age <= 0) continue;
            float ax = _trail[i - 1].X, ay = _trail[i - 1].Y;
            float bx = _trail[i].X, by = _trail[i].Y;
            if (!float.IsFinite(ax + ay + bx + by)) continue; // anti-NaN/Infinity
            pen.Color = Color.FromArgb((int)(230 * age), col);
            g.DrawLine(pen, ax, ay, bx, by);
        }
        using var bob = new SolidBrush(Color.FromArgb(240, Color.White));
        g.FillEllipse(bob, b2x - 4, b2y - 4, 8, 8);
    });
}

// ---------- 21. Fuego fatuo (Wisp) ----------
internal sealed class WispStyle : TrailStyleBase
{
    private struct Wisp { public float X, Y, Vx, Vy, Phase; }
    private struct Smoke { public float X, Y, Life, MaxLife, R; }
    private readonly List<Wisp> _w = new() { new(), new(), new() };
    private readonly List<Smoke> _s = new();
    private bool _seeded;

    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        if (!_seeded)
        {
            for (int i = 0; i < _w.Count; i++)
            {
                var w = _w[i];
                w.X = x; w.Y = y;
                _w[i] = w;
            }
            _seeded = true;
        }
        int n = ctx.ConsumeEmission(2);
        for (int i = 0; i < n && _s.Count < ctx.ScaledCap(40); i++)
        {
            float maxLife = 0.8f * ctx.Options.LifeScale;
            _s.Add(new Smoke
            {
                X = x + (Random.Shared.NextSingle() * 2 - 1) * 8,
                Y = y + (Random.Shared.NextSingle() * 2 - 1) * 8,
                Life = maxLife, MaxLife = maxLife,
                R = (5 + Random.Shared.NextSingle() * 6) * ctx.Options.SizeScale,
            });
        }
    }

    public override void Update(TrailContext ctx, float dt)
    {
        bool fresh = ctx.History.Count > 0 && ctx.Time - ctx.History[^1].T < 2f;
        for (int i = 0; i < _w.Count; i++)
        {
            var w = _w[i];
            w.Vx += ((ctx.CursorX - w.X) * 28 - w.Vx * 6.5f) * dt;
            w.Vy += ((ctx.CursorY - w.Y) * 28 - w.Vy * 6.5f) * dt;
            if (!fresh) { w.Vx *= 0.9f; w.Vy *= 0.9f; }
            w.X += w.Vx * dt;
            w.Y += w.Vy * dt;
            w.Phase += dt * 3;
            _w[i] = w;
        }
        for (int i = _s.Count - 1; i >= 0; i--)
        {
            var s = _s[i];
            s.Life -= dt;
            if (s.Life <= 0) { _s.RemoveAt(i); continue; }
            s.Y -= 24 * dt;
            s.R += 14 * ctx.Options.SizeScale * dt;
            _s[i] = s;
        }
    }

    public override bool HasActivity(TrailContext ctx) =>
        _s.Count > 0 || (ctx.History.Count > 0 && ctx.Time - ctx.History[^1].T < 1f);

    public override void Reset() { _s.Clear(); _seeded = false; }

    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        // Sin humo ni movimiento fresco no hay nada que dibujar.
        if (_s.Count == 0 && !(ctx.History.Count > 0 && ctx.Time - ctx.History[^1].T < 1f))
            return;
        foreach (var s in _s)
        {
            float t = s.Life / s.MaxLife;
            Color col = ctx.PalettePick((int)(s.X + s.Y), Color.FromArgb(150, 200, 220));
            using var b = new SolidBrush(Color.FromArgb((int)(70 * t), col));
            g.FillEllipse(b, s.X - s.R, s.Y - s.R, s.R * 2, s.R * 2);
        }
        for (int i = 0; i < _w.Count; i++)
        {
            var w = _w[i];
            float r = 26 * ctx.Options.SizeScale;
            Color col = ctx.PalettePick(i * 2, Color.FromArgb(170, 220, 255));
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(w.X - r, w.Y - r, r * 2, r * 2);
            using var glow = new System.Drawing.Drawing2D.PathGradientBrush(path);
            glow.CenterColor = Color.FromArgb(190, Color.White);
            glow.SurroundColors = [Color.FromArgb(0, col)];
            g.FillEllipse(glow, w.X - r, w.Y - r, r * 2, r * 2);
        }
    });
}

// ---------- 22. Telaraña dinámica ----------
internal sealed class WebStyle : TrailStyleBase
{
    private struct Node { public float X, Y, Life, MaxLife; }
    private readonly List<Node> _n = new();

    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        if (ctx.ConsumeEmission(1) == 0) return;
        if (_n.Count >= ctx.ScaledCap(40)) _n.RemoveAt(0);
        float maxLife = 2.5f * ctx.Options.LifeScale;
        _n.Add(new Node { X = x, Y = y, Life = maxLife, MaxLife = maxLife });
    }

    public override void Update(TrailContext ctx, float dt)
    {
        for (int i = _n.Count - 1; i >= 0; i--)
        {
            var n = _n[i];
            n.Life -= dt;
            if (n.Life <= 0) _n.RemoveAt(i);
            else _n[i] = n;
        }
    }

    public override bool HasActivity(TrailContext ctx) => _n.Count > 0;
    public override void Reset() => _n.Clear();

    public override RectangleF GetBounds(TrailContext ctx)
    {
        var b = base.GetBounds(ctx);
        if (_n.Count == 0) return b;
        float x0 = b.IsEmpty ? float.MaxValue : b.Left, y0 = b.IsEmpty ? float.MaxValue : b.Top;
        float x1 = b.IsEmpty ? float.MinValue : b.Right, y1 = b.IsEmpty ? float.MinValue : b.Bottom;
        foreach (var n in _n)
        {
            if (n.X < x0) x0 = n.X; if (n.Y < y0) y0 = n.Y;
            if (n.X > x1) x1 = n.X; if (n.Y > y1) y1 = n.Y;
        }
        return RectangleF.FromLTRB(x0, y0, x1, y1);
    }

    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        Color col = ctx.PalettePick(0, Color.FromArgb(160, 210, 255));
        for (int i = 0; i < _n.Count; i++)
        {
            var ordered = new List<(float D, int J)>();
            for (int j = 0; j < _n.Count; j++)
            {
                if (j == i) continue;
                float dx = _n[i].X - _n[j].X, dy = _n[i].Y - _n[j].Y;
                ordered.Add((dx * dx + dy * dy, j));
            }
            ordered.Sort((a, b) => a.D.CompareTo(b.D));
            float ti = _n[i].Life / _n[i].MaxLife;
            for (int k = 0; k < Math.Min(4, ordered.Count); k++)
            {
                var o = _n[ordered[k].J];
                float t = Math.Min(ti, o.Life / o.MaxLife);
                using var pen = new Pen(Color.FromArgb((int)(100 * t), col), 1);
                g.DrawLine(pen, _n[i].X, _n[i].Y, o.X, o.Y);
            }
            using var dot = new SolidBrush(Color.FromArgb((int)(220 * ti), Color.White));
            g.FillEllipse(dot, _n[i].X - 2, _n[i].Y - 2, 4, 4);
        }
    });
}

// ---------- 23. Ecuación de Lorenz (Mariposa) ----------
internal sealed class LorenzStyle : TrailStyleBase
{
    private float _lx = 0.1f, _ly, _lz;
    private readonly List<(float X, float Y, float T)> _trail = new();

    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist) { }

    public override void Update(TrailContext ctx, float dt)
    {
        bool fresh = ctx.History.Count > 0 && ctx.Time - ctx.History[^1].T < 2f;
        if (fresh)
        {
            // Euler explícito diverge con h grande: 16 subpasos (misma
            // velocidad visual ×8 que antes, pero estable). Cortafuegos anti-Infinity.
            int n = 16;
            float hh = Math.Min(dt, 0.033f) / 2;
            for (int s = 0; s < n; s++)
            {
                float dx = 10 * (_ly - _lx);
                float dy = _lx * (28 - _lz) - _ly;
                float dz = _lx * _ly - (8f / 3f) * _lz;
                _lx += dx * hh; _ly += dy * hh; _lz += dz * hh;
                if (!float.IsFinite(_lx + _ly + _lz) ||
                    Math.Abs(_lx) + Math.Abs(_ly) + Math.Abs(_lz) > 90)
                {
                    _lx = 0.1f; _ly = _lz = 0;
                    break;
                }
            }
            float sc = 7 * ctx.Options.SizeScale;
            _trail.Add((ctx.CursorX + _lx * sc, ctx.CursorY + _ly * sc, ctx.Time));
            while (_trail.Count > 70) _trail.RemoveAt(0);
        }
        while (_trail.Count > 0 && ctx.Time - _trail[0].T > 1.0f * ctx.Options.LifeScale)
            _trail.RemoveAt(0);
    }

    public override bool HasActivity(TrailContext ctx) => _trail.Count >= 2;
    public override void Reset() { _trail.Clear(); _lx = 0.1f; _ly = _lz = 0; }

    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        if (_trail.Count < 2) return;
        float window = 1.0f * ctx.Options.LifeScale;
        Color col = ctx.PalettePick(0, Color.FromArgb(255, 130, 220));
        using var pen = new Pen(Color.Empty, Math.Max(1.5f, 2.4f * ctx.Options.SizeScale))
            { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        for (int i = 1; i < _trail.Count; i++)
        {
            float age = 1 - (ctx.Time - _trail[i].T) / window;
            if (age <= 0) continue;
            float ax = _trail[i - 1].X, ay = _trail[i - 1].Y;
            float bx = _trail[i].X, by = _trail[i].Y;
            if (!float.IsFinite(ax + ay + bx + by)) continue; // anti-NaN/Infinity
            pen.Color = Color.FromArgb((int)(230 * age), col);
            g.DrawLine(pen, ax, ay, bx, by);
        }
        var head = _trail[^1];
        if (!float.IsFinite(head.X + head.Y)) return;
        using var dot = new SolidBrush(Color.FromArgb(240, Color.White));
        g.FillEllipse(dot, head.X - 3, head.Y - 3, 6, 6);
    });
}

// ---------- 24. Líquido magnético (Ferrofluid) ----------
internal sealed class FerrofluidStyle : TrailStyleBase
{
    private struct Drop { public float X, Y, Vx, Vy, R, Life, MaxLife; }
    private readonly List<Drop> _d = new();

    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        int n = ctx.ConsumeEmission(dist > 30 ? 3 : 2);
        for (int i = 0; i < n; i++)
        {
            if (_d.Count >= ctx.ScaledCap(26)) _d.RemoveAt(0);
            float maxLife = 1.0f * ctx.Options.LifeScale;
            _d.Add(new Drop
            {
                X = x + (Random.Shared.NextSingle() * 2 - 1) * 8,
                Y = y + (Random.Shared.NextSingle() * 2 - 1) * 8,
                Vx = vx * 0.3f, Vy = vy * 0.3f,
                R = (9 + Random.Shared.NextSingle() * 10) * ctx.Options.SizeScale,
                Life = maxLife, MaxLife = maxLife,
            });
        }
    }

    public override void Update(TrailContext ctx, float dt)
    {
        for (int i = _d.Count - 1; i >= 0; i--)
        {
            var d = _d[i];
            d.Life -= dt;
            if (d.Life <= 0) { _d.RemoveAt(i); continue; }
            d.Vx *= MathF.Max(0, 1 - 2.5f * dt);
            d.Vy *= MathF.Max(0, 1 - 2.5f * dt);
            d.Vy += 200 * dt;
            d.X += d.Vx * dt;
            d.Y += d.Vy * dt;
            _d[i] = d;
        }
    }

    public override bool HasActivity(TrailContext ctx) => _d.Count > 0;
    public override void Reset() => _d.Clear();

    public override RectangleF GetBounds(TrailContext ctx)
    {
        if (_d.Count == 0) return RectangleF.Empty;
        float x0 = float.MaxValue, y0 = float.MaxValue;
        float x1 = float.MinValue, y1 = float.MinValue;
        foreach (var d in _d)
        {
            float r = d.R * 2.6f; // incluye picos
            if (d.X - r < x0) x0 = d.X - r;
            if (d.Y - r < y0) y0 = d.Y - r;
            if (d.X + r > x1) x1 = d.X + r;
            if (d.Y + r > y1) y1 = d.Y + r;
        }
        return RectangleF.FromLTRB(x0, y0, x1, y1);
    }

    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        float speed = 0;
        if (ctx.History.Count >= 2)
        {
            var a = ctx.History[^2]; var b = ctx.History[^1];
            float span = Math.Max(1e-3f, b.T - a.T);
            float dx = b.X - a.X, dy = b.Y - a.Y;
            if (span < 0.5f) speed = MathF.Sqrt(dx * dx + dy * dy) / span;
        }
        float f = Math.Min(1, speed / 900);
        float angBase = MathF.Atan2(ctx.CursorVy, ctx.CursorVx);
        Color col = ctx.PalettePick(0, Color.FromArgb(30, 22, 44));
        foreach (var d in _d)
        {
            float t = d.Life / d.MaxLife;
            using var body = new SolidBrush(Color.FromArgb((int)(225 * t), col));
            g.FillEllipse(body, d.X - d.R, d.Y - d.R, d.R * 2, d.R * 2);
            float spikeLen = d.R * (0.25f + 1.7f * f);
            for (int k = 0; k < 7; k++)
            {
                float a = angBase + (k - 3) * 0.35f;
                float ex = d.X + MathF.Cos(a) * (d.R + spikeLen);
                float ey = d.Y + MathF.Sin(a) * (d.R + spikeLen);
                using var spike = new Pen(Color.FromArgb((int)(210 * t), col),
                    Math.Max(1.2f, d.R * 0.28f))
                    { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
                g.DrawLine(spike, d.X, d.Y, ex, ey);
            }
        }
    });
}

// ---------- 25. Cinta de Moebius (Ribbon 3D) ----------
internal sealed class MoebiusStyle : TrailStyleBase
{
    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist) { }
    public override void Update(TrailContext ctx, float dt) { }
    public override bool HasActivity(TrailContext ctx) =>
        ctx.History.Count >= 4 && ctx.Time - ctx.History[^1].T < 0.6f * ctx.Options.LifeScale;

    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        var h = ctx.History;
        if (h.Count < 4) return;
        float window = 0.55f * ctx.Options.LifeScale;
        float maxW = 12 * ctx.Options.SizeScale;
        Color light = ctx.PalettePick(0, Color.FromArgb(220, 240, 255));
        Color dark = ctx.Options.CustomPalette is { Length: > 1 }
            ? ctx.PalettePick(1, Color.FromArgb(90, 110, 160))
            : Color.FromArgb(90, 110, 160);
        for (int i = 1; i < h.Count; i++)
        {
            float age = 1 - (ctx.Time - h[i].T) / window;
            if (age <= 0) continue;
            float phase = i / (float)(h.Count - 1) * MathF.PI;
            float wdt = maxW * (0.25f + 0.75f * age) * (0.25f + 0.75f * Math.Abs(MathF.Cos(phase)));
            float dx = h[i].X - h[i - 1].X, dy = h[i].Y - h[i - 1].Y;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 1e-3) continue;
            float nx = -dy / len * wdt / 2, ny = dx / len * wdt / 2;
            var quad = new PointF[]
            {
                new(h[i-1].X + nx, h[i-1].Y + ny), new(h[i-1].X - nx, h[i-1].Y - ny),
                new(h[i].X - nx, h[i].Y - ny), new(h[i].X + nx, h[i].Y + ny),
            };
            Color shade = MathF.Sin(phase) >= 0 ? light : dark;
            using var br = new SolidBrush(Color.FromArgb((int)(225 * age), shade));
            g.FillPolygon(br, quad);
        }
    });
}

