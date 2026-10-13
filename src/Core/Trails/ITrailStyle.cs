using System.Drawing.Drawing2D;

namespace CursorRotator.Core.Trails;

/// <summary>Contrato de todo estilo de estela: captura, física y dibujo.</summary>
public interface ITrailStyle : IDisposable
{
    /// <summary>Punto capturado (ya con offset de origen aplicado).</summary>
    void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist);
    void Update(TrailContext ctx, float dt);
    void Draw(TrailContext ctx, Graphics g);
    /// <summary>¿Sigue habiendo algo visible? (apaga el render en reposo).</summary>
    bool HasActivity(TrailContext ctx) => ctx.Particles.Count > 0;
    void Reset() { }

    /// <summary>Caja que encierra lo vivo (para dimensionar el overlay).</summary>
    RectangleF GetBounds(TrailContext ctx);
}

/// <summary>Base con integración compartida (gravedad/rebote del motor) y utilidades.</summary>
public abstract class TrailStyleBase : ITrailStyle
{
    public abstract void OnPush(TrailContext ctx, float x, float y, float vx, float vy, float dist);
    public abstract void Draw(TrailContext ctx, Graphics g);

    public virtual void Update(TrailContext ctx, float dt) => Integrate(ctx, dt, 0);

    /// <summary>Por defecto: hay actividad si quedan partículas compartidas.</summary>
    public virtual bool HasActivity(TrailContext ctx) => ctx.Particles.Count > 0;

    /// <summary>Por defecto: partículas compartidas + historial + cursor.</summary>
    public virtual RectangleF GetBounds(TrailContext ctx)
    {
        bool any = false;
        float x0 = float.MaxValue, y0 = float.MaxValue;
        float x1 = float.MinValue, y1 = float.MinValue;
        foreach (var p in ctx.Particles)
        {
            float r = Math.Max(4, p.Size * 2);
            if (p.X - r < x0) x0 = p.X - r;
            if (p.Y - r < y0) y0 = p.Y - r;
            if (p.X + r > x1) x1 = p.X + r;
            if (p.Y + r > y1) y1 = p.Y + r;
            any = true;
        }
        foreach (var h in ctx.History)
        {
            if (h.X - 10 < x0) x0 = h.X - 10;
            if (h.Y - 10 < y0) y0 = h.Y - 10;
            if (h.X + 10 > x1) x1 = h.X + 10;
            if (h.Y + 10 > y1) y1 = h.Y + 10;
            any = true;
        }
        if (!any) return RectangleF.Empty;
        return RectangleF.FromLTRB(x0, y0, x1, y1);
    }

    public virtual void Reset() { }
    public virtual void Dispose() { }

    protected static void Integrate(TrailContext ctx, float dt, float extraGravityY,
        float damp = 3.5f)
    {
        var ps = ctx.Particles;
        for (int i = ps.Count - 1; i >= 0; i--)
        {
            var p = ps[i];
            p.Life -= dt;
            if (p.Life <= 0) { ps.RemoveAt(i); continue; }
            float k = MathF.Max(0, 1 - damp * dt);
            p.Vx *= k; p.Vy *= k;
            p.Vy += (ctx.GravityY + extraGravityY) * dt;
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            if (ctx.BounceEdges)
            {
                var b = ctx.BounceBounds;
                if (p.X < b.Left) { p.X = b.Left; p.Vx = -p.Vx * 0.55f; }
                else if (p.X > b.Right) { p.X = b.Right; p.Vx = -p.Vx * 0.55f; }
                if (p.Y < b.Top) { p.Y = b.Top; p.Vy = -p.Vy * 0.55f; }
                else if (p.Y > b.Bottom) { p.Y = b.Bottom; p.Vy = -p.Vy * 0.55f; }
            }
            ps[i] = p;
        }
        while (ps.Count > ctx.MaxAlive)
            ps.RemoveAt(0);
    }

    protected static PointF[] SparklePoints(float cx, float cy, float r) =>
    [
        new PointF(cx, cy - r), new PointF(cx + r * 0.22f, cy - r * 0.22f),
        new PointF(cx + r, cy), new PointF(cx + r * 0.22f, cy + r * 0.22f),
        new PointF(cx, cy + r), new PointF(cx - r * 0.22f, cy + r * 0.22f),
        new PointF(cx - r, cy), new PointF(cx - r * 0.22f, cy - r * 0.22f),
    ];

    protected static void UseSmooth(Graphics g, Action draw)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        try { draw(); }
        finally { g.SmoothingMode = old; }
    }
}
