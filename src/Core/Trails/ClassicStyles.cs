namespace CursorRotator.Core.Trails;

/// <summary>Los 8 presets originales como ITrailStyle (misma matemática).</summary>
internal static class ClassicStyleFactory
{
    public static ITrailStyle Create(TrailPreset preset) => preset switch
    {
        TrailPreset.Destellos => new SparklesStyle(),
        TrailPreset.Degradado => new GradientStyle(),
        TrailPreset.Cometa => new CometStyle(),
        TrailPreset.Fuego => new FireStyle(),
        TrailPreset.PolvoEstrellas => new StardustStyle(),
        TrailPreset.LineaNeon => new NeonStyle(),
        TrailPreset.Burbujas => new BubblesStyle(),
        TrailPreset.PixelGlitch => new PixelStyle(),
        _ => new CometStyle(),
    };
}

internal abstract class ClassicParticleStyle : TrailStyleBase
{
    protected abstract float BaseLife { get; }
    protected virtual float Spread => 80f;

    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist)
    {
        if (ctx.Options.Preset == TrailPreset.LineaNeon) return; // el neón usa historial
        int n = ctx.ConsumeEmission(dist > 30 ? 3 : 2);
        float life = BaseLife * ctx.Options.LifeScale;
        for (int i = 0; i < n; i++)
            SpawnOne(ctx, x, y, vx, vy, life);
    }

    protected virtual void SpawnOne(TrailContext ctx, float x, float y, float vx, float vy, float life)
    {
        float a = Random.Shared.NextSingle() * MathF.PI * 2;
        float sp = 25f + Random.Shared.NextSingle() * 55f;
        float px = MathF.Cos(a) * sp + vx * 0.3f * (ctx.Momentum - 1);
        float py = MathF.Sin(a) * sp + vy * 0.3f * (ctx.Momentum - 1);
        float maxLife = life * (0.8f + Random.Shared.NextSingle() * 0.4f);
        ctx.Spawn(x + MathF.Cos(a) * 3, y + MathF.Sin(a) * 3, px, py, maxLife,
            (6f + Random.Shared.NextSingle() * 4f) * ctx.Options.SizeScale,
            (ctx.Time * 90f + Random.Shared.Next(360)) % 360);
    }
}

internal sealed class SparklesStyle : ClassicParticleStyle
{
    protected override float BaseLife => 0.45f;
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        foreach (var p in ctx.Particles)
        {
            float t = p.Life / p.MaxLife;
            float r = Math.Max(1, p.Size * (0.35f + 0.65f * t)) * 1.15f;
            float tw = 0.55f + 0.45f * MathF.Sin(ctx.Time * 28 + p.Seed);
            // Con paleta: reparto secuencial; con color único: base/aclarado;
            // si no: par blanco/cian clásico.
            Color col;
            if (ctx.Options.CustomPalette is { Length: > 0 })
                col = ctx.PalettePick(p.Seed, Color.White);
            else if (ctx.Options.UseCursorColor && !ctx.Options.CursorColor.IsEmpty)
                col = p.Seed % 2 == 0
                    ? TrailContext.Lerp(ctx.Options.CursorColor, Color.White, 0.30f)
                    : ctx.Options.CursorColor;
            else
                col = p.Seed % 2 == 0 ? Color.White : Color.FromArgb(160, 240, 255);
            using var b = new SolidBrush(Color.FromArgb((int)(255 * t * tw), col));
            g.FillPolygon(b, SparklePoints(p.X, p.Y, r));
        }
    });
}

internal sealed class GradientStyle : ClassicParticleStyle
{
    protected override float BaseLife => 0.60f;
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        foreach (var p in ctx.Particles)
        {
            float t = p.Life / p.MaxLife;
            float r = Math.Max(1, p.Size * (0.35f + 0.65f * t));
            float hue = ctx.Options.UseCursorColor && !ctx.Options.CursorColor.IsEmpty
                ? TrailContext.HueOf(ctx.Options.CursorColor) + (p.Seed % 50 - 25)
                : p.Hue;
            using var b = new SolidBrush(Color.FromArgb((int)(230 * t), TrailContext.FromHsv(hue, 0.85f, 1f)));
            g.FillEllipse(b, p.X - r, p.Y - r, r * 2, r * 2);
        }
    });
}

internal sealed class CometStyle : ClassicParticleStyle
{
    protected override float BaseLife => 0.55f;
    protected override void SpawnOne(TrailContext ctx, float x, float y, float vx, float vy, float life)
    {
        float a = Random.Shared.NextSingle() * MathF.PI * 2;
        float maxLife = life * (0.8f + Random.Shared.NextSingle() * 0.4f);
        ctx.Spawn(x + MathF.Cos(a) * 3, y + MathF.Sin(a) * 3,
            MathF.Cos(a) * 45f - vx * 0.12f * ctx.Momentum,
            MathF.Sin(a) * 45f - vy * 0.12f * ctx.Momentum,
            maxLife, (6f + Random.Shared.NextSingle() * 4f) * ctx.Options.SizeScale,
            (ctx.Time * 90f + Random.Shared.Next(360)) % 360);
    }
    public override void Update(TrailContext ctx, float dt) => Integrate(ctx, dt, 0, damp: 2.2f);
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        foreach (var p in ctx.Particles)
        {
            float t = p.Life / p.MaxLife;
            float r = Math.Max(1, p.Size * (0.35f + 0.65f * t));
            using var halo = new SolidBrush(Color.FromArgb((int)(90 * t),
                ctx.BaseColor(Color.FromArgb(120, 220, 255))));
            g.FillEllipse(halo, p.X - r * 1.7f, p.Y - r * 1.7f, r * 3.4f, r * 3.4f);
            using var core = new SolidBrush(Color.FromArgb((int)(255 * t), Color.White));
            g.FillEllipse(core, p.X - r, p.Y - r, r * 2, r * 2);
        }
    });
}

internal sealed class FireStyle : ClassicParticleStyle
{
    protected override float BaseLife => 0.70f;
    public override void Update(TrailContext ctx, float dt) => Integrate(ctx, dt, extraGravityY: -300f);
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        foreach (var p in ctx.Particles)
        {
            float t = p.Life / p.MaxLife;
            float r = Math.Max(1, p.Size * (0.35f + 0.65f * t));
            Color col = ctx.Options.UseCursorColor && !ctx.Options.CursorColor.IsEmpty
                ? TrailContext.FromHsv(TrailContext.HueOf(ctx.Options.CursorColor), 1f, 0.35f + 0.65f * t)
                : TrailContext.FromHsv(55f * t, 1f, 1f);
            using var b = new SolidBrush(Color.FromArgb((int)(240 * t), col));
            g.FillEllipse(b, p.X - r, p.Y - r, r * 2, r * 2);
        }
    });
}

internal sealed class StardustStyle : ClassicParticleStyle
{
    protected override float BaseLife => 0.65f;
    protected override float Spread => 30f;
    public override void Update(TrailContext ctx, float dt) => Integrate(ctx, dt, extraGravityY: 420f);
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        foreach (var p in ctx.Particles)
        {
            float t = p.Life / p.MaxLife;
            float r = Math.Max(1, p.Size * (0.35f + 0.65f * t) * 0.45f);
            Color col = ctx.PalettePick(p.Seed, Color.FromArgb(255, 240, 200));
            if (ctx.Options.CustomPalette is not { Length: > 0 }
                && ctx.Options.UseCursorColor && !ctx.Options.CursorColor.IsEmpty)
                col = TrailContext.Lerp(ctx.Options.CursorColor, Color.White, 0.55f);
            using var b = new SolidBrush(Color.FromArgb((int)(235 * t), col));
            g.FillEllipse(b, p.X - r, p.Y - r, r * 2, r * 2);
        }
    });
}

internal sealed class NeonStyle : TrailStyleBase
{
    public override void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist) { }
    public override void Update(TrailContext ctx, float dt) { } // el historial lo poda el motor
    public override bool HasActivity(TrailContext ctx) =>
        ctx.History.Count >= 2 && ctx.Time - ctx.History[^1].T < 0.6f * ctx.Options.LifeScale;
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        if (ctx.History.Count < 2) return;
        float window = 0.55f * ctx.Options.LifeScale;
        var col = ctx.BaseColor(Color.FromArgb(70, 224, 255));
        float w = Math.Max(1.5f, 3.2f * ctx.Options.SizeScale);
        for (int pass = 0; pass < 2; pass++)
        {
            float wp = pass == 0 ? w * 2.6f : w;
            using var pen = new Pen(Color.Empty, wp)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round,
            };
            for (int i = 1; i < ctx.History.Count; i++)
            {
                float age = 1 - (ctx.Time - ctx.History[i].T) / window;
                if (age <= 0) continue;
                int s = Math.Max(0, i - 2), e = Math.Min(ctx.History.Count - 1, i + 1);
                var pts = new PointF[e - s + 1];
                for (int k = s; k <= e; k++)
                    pts[k - s] = new PointF(ctx.History[k].X, ctx.History[k].Y);
                pen.Color = Color.FromArgb((int)(255 * age * (pass == 0 ? 0.22f : 0.95f)), col);
                if (pts.Length == 2) g.DrawLine(pen, pts[0], pts[1]);
                else g.DrawCurve(pen, pts, 0.5f);
            }
        }
    });
}

internal sealed class BubblesStyle : ClassicParticleStyle
{
    protected override float BaseLife => 0.90f;
    protected override float Spread => 20f;
    public override void Update(TrailContext ctx, float dt) => Integrate(ctx, dt, extraGravityY: -55f);
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        foreach (var p in ctx.Particles)
        {
            float t = p.Life / p.MaxLife;
            float rb = p.Size * (1.9f - 0.9f * t);
            using var pen = new Pen(Color.FromArgb((int)(220 * t),
                ctx.PalettePick(p.Seed, Color.FromArgb(140, 220, 255))), Math.Max(1, 2 * ctx.Options.SizeScale));
            g.DrawEllipse(pen, p.X - rb, p.Y - rb, rb * 2, rb * 2);
        }
    });
}

internal sealed class PixelStyle : ClassicParticleStyle
{
    protected override float BaseLife => 0.40f;
    protected override void SpawnOne(TrailContext ctx, float x, float y, float vx, float vy, float life)
    {
        float jx = Random.Shared.NextSingle() * 14 - 7;
        float jy = Random.Shared.NextSingle() * 14 - 7;
        float a = Random.Shared.NextSingle() * MathF.PI * 2;
        float maxLife = life * (0.8f + Random.Shared.NextSingle() * 0.4f);
        ctx.Spawn(x + MathF.Cos(a) * 3 + jx, y + MathF.Sin(a) * 3 + jy,
            MathF.Cos(a) * 80f, MathF.Sin(a) * 80f, maxLife,
            (6f + Random.Shared.NextSingle() * 4f) * ctx.Options.SizeScale,
            (ctx.Time * 90f + Random.Shared.Next(360)) % 360);
    }
    public override void Draw(TrailContext ctx, Graphics g) => UseSmooth(g, () =>
    {
        foreach (var p in ctx.Particles)
        {
            float t = p.Life / p.MaxLife;
            float s = Math.Max(2, p.Size * 0.5f);
            float jitter = 0.4f + 0.6f * ((p.Seed * 37 % 100) / 100f);
            using var b = new SolidBrush(Color.FromArgb((int)(240 * t * jitter),
                ctx.PalettePick(p.Seed, Color.FromArgb(150, 255, 200))));
            g.FillRectangle(b, p.X - s / 2, p.Y - s / 2, s, s);
        }
    });
}
