using System.Drawing.Drawing2D;

namespace CursorRotator.Core.Trails;

/// <summary>Tercera oleada: 15 estilos matemáticos (26–40).</summary>

// ---------- 26. Espiral áurea (Fibonacci) ----------
internal sealed class GoldenSpiralStyle : TrailStyleBase
{
    private int _k;
    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        if (ctx.ConsumeEmission(2) == 0) return;
        // Ángulo áureo 137.5°, radio ∝ √k: empaquetado de Fibonacci.
        float a = _k * 2.39996f;
        float r = 3 * MathF.Sqrt(_k % 60 + 1) * ctx.Options.SizeScale;
        _k++;
        float maxLife = 0.8f * ctx.Options.LifeScale;
        ctx.Spawn(x + MathF.Cos(a) * r, y + MathF.Sin(a) * r, 0, 0,
            maxLife, (4 + Random.Shared.NextSingle() * 3) * ctx.Options.SizeScale,
            (_k * 37) % 360);
    }
    public override void Update(TrailContext ctx, float dt) => Integrate(ctx, dt, 0, damp: 0.5f);
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        foreach (var p in ctx.Particles)
        {
            float t = p.Life / p.MaxLife;
            float r = Math.Max(1, p.Size * t);
            using var b = new SolidBrush(Color.FromArgb((int)(235 * t),
                ctx.PalettePick(p.Seed, TrailContext.FromHsv(p.Hue, 0.85f, 1f))));
            g.FillEllipse(b, p.X - r, p.Y - r, r * 2, r * 2);
        }
    });
    public override void Reset() => _k = 0;
}

// ---------- 27. Constelación ----------
internal sealed class ConstellationStyle : TrailStyleBase
{
    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        int n = ctx.ConsumeEmission(2);
        for (int i = 0; i < n && ctx.Particles.Count < ctx.ScaledCap(50); i++)
        {
            float maxLife = 1.6f * ctx.Options.LifeScale;
            ctx.Spawn(x + (Random.Shared.NextSingle() * 2 - 1) * 26,
                y + (Random.Shared.NextSingle() * 2 - 1) * 26,
                (Random.Shared.NextSingle() * 2 - 1) * 20,
                (Random.Shared.NextSingle() * 2 - 1) * 20, maxLife,
                (3 + Random.Shared.NextSingle() * 2) * ctx.Options.SizeScale, 0);
        }
    }
    public override void Update(TrailContext ctx, float dt) => Integrate(ctx, dt, 0, damp: 1.2f);
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        var ps = ctx.Particles;
        Color col = ctx.PalettePick(0, Color.FromArgb(200, 230, 255));
        for (int i = 0; i < ps.Count; i++)
        {
            // 5 vecinos más cercanos por estrella.
            var near = new List<(float D, int J)>();
            for (int j = 0; j < ps.Count; j++)
            {
                if (j == i) continue;
                float dx = ps[i].X - ps[j].X, dy = ps[i].Y - ps[j].Y;
                near.Add((dx * dx + dy * dy, j));
            }
            near.Sort((a, b) => a.D.CompareTo(b.D));
            float ti = ps[i].Life / ps[i].MaxLife;
            for (int k = 0; k < Math.Min(5, near.Count); k++)
            {
                var o = ps[near[k].J];
                float t = Math.Min(ti, o.Life / o.MaxLife);
                if (t <= 0.03f) continue;
                using var pen = new Pen(Color.FromArgb((int)(130 * t), col), 1);
                g.DrawLine(pen, ps[i].X, ps[i].Y, o.X, o.Y);
            }
            using var dot = new SolidBrush(Color.FromArgb((int)(240 * ti), Color.White));
            g.FillEllipse(dot, ps[i].X - 2, ps[i].Y - 2, 4, 4);
        }
    });
}

// ---------- 28. Aliento de dragón ----------
internal sealed class DragonBreathStyle : TrailStyleBase
{
    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        int n = ctx.ConsumeEmission(3);
        for (int i = 0; i < n && ctx.Particles.Count < ctx.ScaledCap(60); i++)
        {
            bool spark = i % 3 != 0; // 2 chispas por 1 humo
            float maxLife = (spark ? 0.5f : 1.1f) * ctx.Options.LifeScale;
            float a = Random.Shared.NextSingle() * MathF.PI * 2;
            float sp = spark ? 120 + Random.Shared.NextSingle() * 260
                             : 20 + Random.Shared.NextSingle() * 40;
            ctx.Spawn(x, y, MathF.Cos(a) * sp + vx * 0.2f, MathF.Sin(a) * sp + vy * 0.2f - 40,
                maxLife, (spark ? 3 + Random.Shared.NextSingle() * 3 : 8 + Random.Shared.NextSingle() * 8)
                    * ctx.Options.SizeScale, spark ? 1 : 0);
        }
    }
    public override void Update(TrailContext ctx, float dt) => Integrate(ctx, dt, extraGravityY: -160f, damp: 2.2f);
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        foreach (var p in ctx.Particles)
        {
            float t = p.Life / p.MaxLife;
            bool spark = p.Hue > 0.5f;
            float r = Math.Max(1, p.Size * (0.4f + 0.6f * t));
            Color col = spark
                ? ctx.PalettePick(p.Seed, TrailContext.FromHsv(18 + p.Seed % 22, 1f, 1f))
                : Color.FromArgb(110, 110, 115);
            using var b = new SolidBrush(Color.FromArgb((int)((spark ? 245 : 90) * t), col));
            g.FillEllipse(b, p.X - r, p.Y - r, r * 2, r * 2);
        }
    });
}

// ---------- 29. Láser táctico ----------
internal sealed class TacticalLaserStyle : TrailStyleBase
{
    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist) { }
    public override void Update(TrailContext ctx, float dt) { }
    public override bool HasActivity(TrailContext ctx) =>
        ctx.History.Count >= 2 && ctx.Time - ctx.History[^1].T < 0.6f * ctx.Options.LifeScale;
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        var h = ctx.History;
        if (h.Count < 2) return;
        float window = 0.55f * ctx.Options.LifeScale;
        Color col = ctx.PalettePick(0, Color.FromArgb(255, 60, 60));
        for (int pass = 0; pass < 2; pass++)
        {
            float w = pass == 0 ? 7 * ctx.Options.SizeScale : 1.5f;
            using var pen = new Pen(Color.Empty, w)
                { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            for (int i = 1; i < h.Count; i++)
            {
                float age = 1 - (ctx.Time - h[i].T) / window;
                if (age <= 0) continue;
                pen.Color = Color.FromArgb((int)(255 * age * (pass == 0 ? 0.25f : 1f)),
                    pass == 0 ? col : Color.White);
                g.DrawLine(pen, h[i - 1].X, h[i - 1].Y, h[i].X, h[i].Y);
            }
        }
    });
}

// ---------- 30. Hex Dump ----------
internal sealed class HexDumpStyle : TrailStyleBase
{
    private const string Hex = "0123456789ABCDEF";
    private struct Cell { public float X, Y, Life, MaxLife; public char A, B; }
    private readonly List<Cell> _c = new();
    private Font _font;
    private float _fontScale = -1;

    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        if (ctx.ConsumeEmission(2) == 0) return;
        if (_c.Count >= ctx.ScaledCap(48)) _c.RemoveAt(0);
        float maxLife = 1.0f * ctx.Options.LifeScale;
        _c.Add(new Cell
        {
            X = x + (Random.Shared.NextSingle() * 2 - 1) * 10,
            Y = y + (Random.Shared.NextSingle() * 2 - 1) * 10,
            Life = maxLife, MaxLife = maxLife,
            A = Hex[Random.Shared.Next(16)], B = Hex[Random.Shared.Next(16)],
        });
    }
    public override void Update(TrailContext ctx, float dt)
    {
        for (int i = _c.Count - 1; i >= 0; i--)
        {
            var c = _c[i];
            c.Life -= dt;
            if (c.Life <= 0) _c.RemoveAt(i);
            else { c.Y += 90 * dt; c.X += 24 * dt; _c[i] = c; }
        }
    }
    public override bool HasActivity(TrailContext ctx) => _c.Count > 0;
    public override void Reset() => _c.Clear();
    public override RectangleF GetBounds(TrailContext ctx)
    {
        if (_c.Count == 0) return RectangleF.Empty;
        float x0 = float.MaxValue, y0 = float.MaxValue;
        float x1 = float.MinValue, y1 = float.MinValue;
        foreach (var c in _c)
        {
            if (c.X - 16 < x0) x0 = c.X - 16;
            if (c.Y - 10 < y0) y0 = c.Y - 10;
            if (c.X + 16 > x1) x1 = c.X + 16;
            if (c.Y + 10 > y1) y1 = c.Y + 10;
        }
        return RectangleF.FromLTRB(x0, y0, x1, y1);
    }
    public override void Draw(TrailContext ctx, Graphics g)
    {
        float want = 10 * ctx.Options.SizeScale;
        if (_font is null || Math.Abs(_fontScale - want) > 0.5f)
        {
            _font?.Dispose();
            _font = new Font("Consolas", want, FontStyle.Regular, GraphicsUnit.Pixel);
            _fontScale = want;
        }
        Color col = ctx.PalettePick(0, Color.FromArgb(90, 255, 150));
        foreach (var c in _c)
        {
            float t = c.Life / c.MaxLife;
            using var b = new SolidBrush(Color.FromArgb((int)(230 * t), col));
            g.DrawString($"{c.A}{c.B}", _font, b, c.X, c.Y);
        }
    }
    public override void Dispose() { _font?.Dispose(); _font = null; }
}

// ---------- 31. Holograma RGB ----------
internal sealed class HologramStyle : TrailStyleBase
{
    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist) { }
    public override void Update(TrailContext ctx, float dt) { }
    public override bool HasActivity(TrailContext ctx) =>
        ctx.History.Count >= 3 && ctx.Time - ctx.History[^1].T < 0.6f * ctx.Options.LifeScale;
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        var h = ctx.History;
        if (h.Count < 3) return;
        float window = 0.55f * ctx.Options.LifeScale;
        float speed = Math.Min(1, MathF.Sqrt(ctx.CursorVx * ctx.CursorVx + ctx.CursorVy * ctx.CursorVy) / 1500);
        float off = (2 + 7 * speed) * ctx.Options.SizeScale; // desfase con velocidad
        float flick = 0.75f + 0.25f * MathF.Sin(ctx.Time * 40);
        Color[] cols = ctx.Options.CustomPalette is { Length: >= 3 }
            ? [ctx.Options.CustomPalette[0], ctx.Options.CustomPalette[1], ctx.Options.CustomPalette[2]]
            : ctx.Options.UseCursorColor && !ctx.Options.CursorColor.IsEmpty
            ? [ctx.Options.CursorColor,
               TrailContext.Lerp(ctx.Options.CursorColor, Color.White, 0.5f),
               TrailContext.Lerp(ctx.Options.CursorColor, Color.Black, 0.35f)]
            : [Color.FromArgb(255, 70, 70), Color.FromArgb(70, 255, 70), Color.FromArgb(70, 140, 255)];
        float w = Math.Max(1.2f, 2.2f * ctx.Options.SizeScale);
        // Normal del tramo final para el desfase perpendicular.
        var a = h[^2]; var b = h[^1];
        float dx = b.X - a.X, dy = b.Y - a.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        float nx = 0, ny = 0;
        if (len > 1e-3) { nx = -dy / len; ny = dx / len; }
        for (int c = 0; c < 3; c++)
        {
            using var pen = new Pen(Color.Empty, w)
                { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            for (int i = 1; i < h.Count; i++)
            {
                float age = 1 - (ctx.Time - h[i].T) / window;
                if (age <= 0) continue;
                float ox = nx * off * (c - 1), oy = ny * off * (c - 1);
                pen.Color = Color.FromArgb((int)(200 * age * flick), cols[c]);
                g.DrawLine(pen, h[i - 1].X + ox, h[i - 1].Y + oy, h[i].X + ox, h[i].Y + oy);
            }
        }
    });
}

// ---------- 32. Mariposas monarca ----------
internal sealed class MonarchStyle : TrailStyleBase
{
    private struct Fly { public float X, Y, Vx, Vy, Phase, Life, MaxLife, Size; public int Seed; }
    private readonly List<Fly> _f = new();

    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        int n = ctx.ConsumeEmission(2);
        for (int i = 0; i < n && _f.Count < ctx.ScaledCap(24); i++)
        {
            float maxLife = 1.4f * ctx.Options.LifeScale;
            _f.Add(new Fly
            {
                X = x, Y = y,
                Vx = vx * 0.15f + (Random.Shared.NextSingle() * 2 - 1) * 70,
                Vy = vy * 0.15f - 40 - Random.Shared.NextSingle() * 60,
                Phase = Random.Shared.NextSingle() * MathF.PI * 2,
                Life = maxLife, MaxLife = maxLife,
                Size = (7 + Random.Shared.NextSingle() * 5) * ctx.Options.SizeScale,
                Seed = Random.Shared.Next(1000),
            });
        }
    }
    public override void Update(TrailContext ctx, float dt)
    {
        for (int i = _f.Count - 1; i >= 0; i--)
        {
            var f = _f[i];
            f.Life -= dt;
            if (f.Life <= 0) { _f.RemoveAt(i); continue; }
            // Curvas senoidales + aleteo.
            f.Vx += MathF.Sin(ctx.Time * 5 + f.Phase) * 160 * dt;
            f.Vy += -30 * dt;
            f.X += (f.Vx + MathF.Cos(ctx.Time * 3 + f.Phase) * 60) * dt;
            f.Y += f.Vy * dt;
            f.Phase += dt * 14; // aleteo rápido
            _f[i] = f;
        }
    }
    public override bool HasActivity(TrailContext ctx) => _f.Count > 0;
    public override void Reset() => _f.Clear();
    public override RectangleF GetBounds(TrailContext ctx)
    {
        if (_f.Count == 0) return RectangleF.Empty;
        float x0 = float.MaxValue, y0 = float.MaxValue;
        float x1 = float.MinValue, y1 = float.MinValue;
        foreach (var f in _f)
        {
            float r = f.Size;
            if (f.X - r < x0) x0 = f.X - r;
            if (f.Y - r < y0) y0 = f.Y - r;
            if (f.X + r > x1) x1 = f.X + r;
            if (f.Y + r > y1) y1 = f.Y + r;
        }
        return RectangleF.FromLTRB(x0, y0, x1, y1);
    }
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        foreach (var f in _f)
        {
            float t = f.Life / f.MaxLife;
            // Aleteo: escala X oscilante.
            float flap = 0.25f + 0.75f * Math.Abs(MathF.Cos(f.Phase));
            float w = f.Size * flap, h = f.Size * 0.8f;
            Color col = ctx.PalettePick(f.Seed, TrailContext.FromHsv(28, 0.95f, 1f));
            using var b = new SolidBrush(Color.FromArgb((int)(235 * t), col));
            g.FillPolygon(b, [
                new PointF(f.X - w, f.Y - h), new PointF(f.X, f.Y - h * 0.3f),
                new PointF(f.X - w * 0.7f, f.Y + h),
            ]);
            g.FillPolygon(b, [
                new PointF(f.X + w, f.Y - h), new PointF(f.X, f.Y - h * 0.3f),
                new PointF(f.X + w * 0.7f, f.Y + h),
            ]);
        }
    });
}

// ---------- 33. Niebla oscura ----------
internal sealed class DarkFogStyle : TrailStyleBase
{
    private struct Puff { public float X, Y, R, Life, MaxLife; }
    private readonly List<Puff> _p = new();

    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        int n = ctx.ConsumeEmission(2);
        for (int i = 0; i < n && _p.Count < ctx.ScaledCap(20); i++)
        {
            float maxLife = 1.6f * ctx.Options.LifeScale;
            _p.Add(new Puff
            {
                X = x + (Random.Shared.NextSingle() * 2 - 1) * 12,
                Y = y + (Random.Shared.NextSingle() * 2 - 1) * 12,
                R = (16 + Random.Shared.NextSingle() * 14) * ctx.Options.SizeScale,
                Life = maxLife, MaxLife = maxLife,
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
            p.R += 22 * ctx.Options.SizeScale * dt;
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
            if (p.X - p.R < x0) x0 = p.X - p.R;
            if (p.Y - p.R < y0) y0 = p.Y - p.R;
            if (p.X + p.R > x1) x1 = p.X + p.R;
            if (p.Y + p.R > y1) y1 = p.Y + p.R;
        }
        return RectangleF.FromLTRB(x0, y0, x1, y1);
    }
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        // Negro/obsidiana: ignora paleta a propósito (bloquea la luz).
        foreach (var p in _p)
        {
            float t = p.Life / p.MaxLife;
            using var path = new GraphicsPath();
            path.AddEllipse(p.X - p.R, p.Y - p.R, p.R * 2, p.R * 2);
            using var fog = new PathGradientBrush(path);
            fog.CenterColor = Color.FromArgb((int)(120 * t), Color.FromArgb(8, 6, 14));
            fog.SurroundColors = [Color.FromArgb(0, Color.Black)];
            g.FillEllipse(fog, p.X - p.R, p.Y - p.R, p.R * 2, p.R * 2);
        }
    });
}

// ---------- 34. Caleidoscopio ----------
internal sealed class KaleidoscopeStyle : TrailStyleBase
{
    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        int n = ctx.ConsumeEmission(2);
        for (int i = 0; i < n && ctx.Particles.Count < ctx.ScaledCap(60); i++)
        {
            float maxLife = 0.9f * ctx.Options.LifeScale;
            ctx.Spawn(x, y, (Random.Shared.NextSingle() * 2 - 1) * 60,
                (Random.Shared.NextSingle() * 2 - 1) * 60, maxLife,
                (4 + Random.Shared.NextSingle() * 3) * ctx.Options.SizeScale,
                Random.Shared.Next(360));
        }
    }
    public override void Update(TrailContext ctx, float dt) => Integrate(ctx, dt, 0, damp: 1.5f);
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        float cx = ctx.ScreenCenter.X, cy = ctx.ScreenCenter.Y;
        foreach (var p in ctx.Particles)
        {
            float t = p.Life / p.MaxLife;
            float r = Math.Max(1, p.Size * t);
            Color col = ctx.PalettePick(p.Seed, TrailContext.FromHsv(p.Hue, 0.85f, 1f));
            using var b = new SolidBrush(Color.FromArgb((int)(230 * t), col));
            // 4 ejes simétricos alrededor del centro de pantalla.
            foreach (var (mx, my) in Mirror4(p.X, p.Y, cx, cy))
                g.FillEllipse(b, mx - r, my - r, r * 2, r * 2);
        }
    });
    private static (float, float)[] Mirror4(float x, float y, float cx, float cy) =>
    [
        (x, y),
        (2 * cx - x, y),
        (x, 2 * cy - y),
        (2 * cx - x, 2 * cy - y),
    ];
}

// ---------- 35. Retro 8-bits ----------
internal sealed class RetroStyle : TrailStyleBase
{
    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        int n = ctx.ConsumeEmission(2);
        for (int i = 0; i < n && ctx.Particles.Count < ctx.ScaledCap(48); i++)
        {
            // Velocidad encajada a 45°/90°.
            float a = Random.Shared.NextSingle() * MathF.PI * 2;
            a = MathF.Round(a / (MathF.PI / 4)) * (MathF.PI / 4);
            float sp = 60 + Random.Shared.NextSingle() * 120;
            float maxLife = 0.7f * ctx.Options.LifeScale;
            ctx.Spawn(x, y, MathF.Cos(a) * sp, MathF.Sin(a) * sp, maxLife,
                (6 + Random.Shared.NextSingle() * 4) * ctx.Options.SizeScale,
                Random.Shared.Next(360));
        }
    }
    public override void Update(TrailContext ctx, float dt) => Integrate(ctx, dt, 0, damp: 4f);
    public override void Draw(TrailContext ctx, Graphics g)
    {
        // Sin antialias: bloques perfectos.
        foreach (var p in ctx.Particles)
        {
            float t = p.Life / p.MaxLife;
            float s = Math.Max(3, p.Size * t);
            using var b = new SolidBrush(Color.FromArgb((int)(245 * t),
                ctx.PalettePick(p.Seed, TrailContext.FromHsv(p.Hue, 0.7f, 1f))));
            g.FillRectangle(b, p.X - s / 2, p.Y - s / 2, s, s);
        }
    }
}

// ---------- 36. Cadena de bloques ----------
internal sealed class BlockchainStyle : TrailStyleBase
{
    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        if (ctx.ConsumeEmission(1) == 0) return;
        if (ctx.Particles.Count >= ctx.ScaledCap(40)) ctx.Particles.RemoveAt(0);
        float maxLife = 1.4f * ctx.Options.LifeScale;
        ctx.Spawn(x, y, 0, 0, maxLife,
            (7 + Random.Shared.NextSingle() * 3) * ctx.Options.SizeScale, 0);
    }
    public override void Update(TrailContext ctx, float dt)
    {
        for (int i = ctx.Particles.Count - 1; i >= 0; i--)
        {
            var p = ctx.Particles[i];
            p.Life -= dt;
            if (p.Life <= 0) ctx.Particles.RemoveAt(i);
            else ctx.Particles[i] = p;
        }
    }
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        var ps = ctx.Particles;
        Color col = ctx.PalettePick(0, Color.FromArgb(120, 200, 255));
        for (int i = 1; i < ps.Count; i++)
        {
            float t = Math.Min(ps[i].Life / ps[i].MaxLife, ps[i - 1].Life / ps[i - 1].MaxLife);
            if (t <= 0.03f) continue;
            using var link = new Pen(Color.FromArgb((int)(200 * t), col),
                Math.Max(2, 3 * ctx.Options.SizeScale));
            g.DrawLine(link, ps[i - 1].X, ps[i - 1].Y, ps[i].X, ps[i].Y);
        }
        int k = 0;
        foreach (var p in ps)
        {
            float t = p.Life / p.MaxLife;
            float s = Math.Max(3, p.Size * (0.5f + 0.5f * t));
            using var b = new SolidBrush(Color.FromArgb((int)(245 * t),
                ctx.PalettePick(k++, col)));
            g.FillRectangle(b, p.X - s / 2, p.Y - s / 2, s, s);
        }
    });
}

// ---------- 37. Viento lateral (Blizzard) ----------
internal sealed class BlizzardStyle : TrailStyleBase
{
    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        int n = ctx.ConsumeEmission(2);
        for (int i = 0; i < n && ctx.Particles.Count < ctx.ScaledCap(60); i++)
        {
            float maxLife = 1.0f * ctx.Options.LifeScale;
            ctx.Spawn(x + (Random.Shared.NextSingle() * 2 - 1) * 12,
                y + (Random.Shared.NextSingle() * 2 - 1) * 12,
                (Random.Shared.NextSingle() * 2 - 1) * 50,
                -30 - Random.Shared.NextSingle() * 60, maxLife,
                (3 + Random.Shared.NextSingle() * 3) * ctx.Options.SizeScale, 0);
        }
    }
    public override void Update(TrailContext ctx, float dt)
    {
        var ps = ctx.Particles;
        for (int i = ps.Count - 1; i >= 0; i--)
        {
            var p = ps[i];
            p.Life -= dt;
            if (p.Life <= 0) { ps.RemoveAt(i); continue; }
            // Viento horizontal constante + turbulencia leve.
            p.Vx += (380 + MathF.Sin(ctx.Time * 3 + p.Seed) * 120) * dt;
            p.Vy *= MathF.Max(0, 1 - 0.8f * dt);
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            ps[i] = p;
        }
        while (ps.Count > ctx.ScaledCap(60))
            ps.RemoveAt(0);
    }
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        foreach (var p in ctx.Particles)
        {
            float t = p.Life / p.MaxLife;
            float r = Math.Max(1, p.Size * t);
            using var b = new SolidBrush(Color.FromArgb((int)(220 * t),
                ctx.PalettePick(p.Seed, Color.FromArgb(220, 235, 255))));
            g.FillEllipse(b, p.X - r, p.Y - r, r * 2, r * 2);
        }
    });
}

// ---------- 38. Cuerda vibrante ----------
internal sealed class StringStyle : TrailStyleBase
{
    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist) { }
    public override void Update(TrailContext ctx, float dt) { }
    public override bool HasActivity(TrailContext ctx) =>
        ctx.History.Count >= 6 && ctx.Time - ctx.History[^1].T < 0.8f * ctx.Options.LifeScale;
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        var h = ctx.History;
        if (h.Count < 6) return;
        float window = 0.8f * ctx.Options.LifeScale;
        Color col = ctx.PalettePick(0, Color.FromArgb(255, 210, 120));
        using var pen = new Pen(Color.Empty, Math.Max(1.5f, 2.4f * ctx.Options.SizeScale))
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        int n = h.Count;
        for (int i = 1; i < n; i++)
        {
            float age = 1 - (ctx.Time - h[i].T) / window;
            if (age <= 0) continue;
            // Onda estacionaria: vientre al centro, nodos en extremos.
            float s = i / (float)(n - 1);
            float env = MathF.Sin(s * MathF.PI) * 16 * ctx.Options.SizeScale * age;
            float wob = MathF.Sin(s * 9 - ctx.Time * 22) * env;
            float dx = h[i].X - h[i - 1].X, dy = h[i].Y - h[i - 1].Y;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            float nx = 0, ny = 0;
            if (len > 1e-3) { nx = -dy / len; ny = dx / len; }
            pen.Color = Color.FromArgb((int)(230 * age), col);
            g.DrawLine(pen,
                h[i - 1].X + nx * wob * (1 - 1f / n), h[i - 1].Y + ny * wob * (1 - 1f / n),
                h[i].X + nx * wob, h[i].Y + ny * wob);
        }
    });
}

// ---------- 39. Bioluminiscencia ----------
internal sealed class BioStyle : TrailStyleBase
{
    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        int n = ctx.ConsumeEmission(2);
        for (int i = 0; i < n && ctx.Particles.Count < ctx.ScaledCap(48); i++)
        {
            float maxLife = 2.0f * ctx.Options.LifeScale;
            ctx.Spawn(x + (Random.Shared.NextSingle() * 2 - 1) * 30,
                y + (Random.Shared.NextSingle() * 2 - 1) * 30, 0, 0, maxLife,
                (4 + Random.Shared.NextSingle() * 4) * ctx.Options.SizeScale, 0);
        }
    }
    public override void Update(TrailContext ctx, float dt)
    {
        // Estáticas: solo decaen. El pulso se calcula al dibujar.
        for (int i = ctx.Particles.Count - 1; i >= 0; i--)
        {
            var p = ctx.Particles[i];
            p.Life -= dt;
            if (p.Life <= 0) ctx.Particles.RemoveAt(i);
            else ctx.Particles[i] = p;
        }
    }
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        foreach (var p in ctx.Particles)
        {
            float t = p.Life / p.MaxLife;
            // Pulso oscilante del alfa, también en reposo.
            float pulse = 0.35f + 0.65f * Math.Abs(MathF.Sin(ctx.Time * 3 + p.Seed));
            float r = Math.Max(1.5f, p.Size * t);
            Color col = ctx.PalettePick(p.Seed, Color.FromArgb(100, 255, 190));
            using var halo = new SolidBrush(Color.FromArgb((int)(70 * t * pulse), col));
            g.FillEllipse(halo, p.X - r * 2.2f, p.Y - r * 2.2f, r * 4.4f, r * 4.4f);
            using var core = new SolidBrush(Color.FromArgb((int)(240 * t * pulse), Color.White));
            g.FillEllipse(core, p.X - r * 0.5f, p.Y - r * 0.5f, r, r);
        }
    });
}

// ---------- 40. Agujeros blancos ----------
internal sealed class WhiteHolesStyle : TrailStyleBase
{
    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        int n = ctx.ConsumeEmission(3);
        for (int i = 0; i < n && ctx.Particles.Count < ctx.ScaledCap(70); i++)
        {
            // Expulsión omnidireccional continua mientras hay movimiento.
            float a = Random.Shared.NextSingle() * MathF.PI * 2;
            float sp = 90 + Random.Shared.NextSingle() * 260;
            float maxLife = 0.55f * ctx.Options.LifeScale;
            ctx.Spawn(x, y, MathF.Cos(a) * sp, MathF.Sin(a) * sp, maxLife,
                (2 + Random.Shared.NextSingle() * 2.5f) * ctx.Options.SizeScale, 0);
        }
    }
    public override void Update(TrailContext ctx, float dt) => Integrate(ctx, dt, 0, damp: 2.8f);
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        foreach (var p in ctx.Particles)
        {
            float t = p.Life / p.MaxLife;
            float r = Math.Max(1, p.Size * t);
            using var b = new SolidBrush(Color.FromArgb((int)(245 * t),
                ctx.PalettePick(p.Seed, Color.White)));
            g.FillEllipse(b, p.X - r, p.Y - r, r * 2, r * 2);
        }
    });
}
