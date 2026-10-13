using System.Windows.Forms;
using CursorRotator.Core.Trails;

namespace CursorRotator.Core;

/// <summary>Presets de estela disponibles en la GUI.</summary>
public enum TrailPreset
{
    Destellos = 0,
    Degradado = 1,
    Cometa = 2,
    Fuego = 3,
    PolvoEstrellas = 4,
    LineaNeon = 5,
    Burbujas = 6,
    PixelGlitch = 7,
    Orbital = 8,
    Dna = 9,
    Tesla = 10,
    Swarm = 11,
    Ripples = 12,
    HackerRain = 13,
    Metaballs = 14,
    LightRibbon = 15,
    Confetti = 16,
    MatrixGrid = 17,
    Singularity = 18,
    DoublePendulum = 19,
    Wisp = 20,
    Web = 21,
    Lorenz = 22,
    Ferrofluid = 23,
    Moebius = 24,
    GoldenSpiral = 25,
    Constellation = 26,
    DragonBreath = 27,
    TacticalLaser = 28,
    HexDump = 29,
    HologramRgb = 30,
    Monarchs = 31,
    DarkFog = 32,
    Kaleidoscope = 33,
    Retro8Bit = 34,
    Blockchain = 35,
    Blizzard = 36,
    VibratingString = 37,
    Bioluminescence = 38,
    WhiteHoles = 39,
}

/// <summary>De dónde nace la estela respecto al puntero.</summary>
public enum TrailOrigin
{
    /// <summary>En las coordenadas (X, Y) del cursor.</summary>
    Punta = 0,
    /// <summary>Offset (+16, +20): parece salir de la cola (cursor 32×32).</summary>
    Cola = 1,
}

/// <summary>Motor de captura/render de la estela (conmutable en vivo).</summary>
public enum TrailCaptureMode
{
    /// <summary>Sondeo clásico por frame (~60 Hz). Predeterminado estable.</summary>
    Timer = 0,
    /// <summary>Hook WH_MOUSE_LL: cada evento del SO, sin perder puntos.</summary>
    Hook = 1,
    /// <summary>Hook + remuestreo Catmull-Rom entre muestras (trazo denso).</summary>
    Spline = 2,
    /// <summary>Física con inercia: momentum, gravedad y rebote en bordes.</summary>
    Inertial = 3,
    /// <summary>Render asíncrono multihilo con doble búfer.</summary>
    Async = 4,
    /// <summary>Interpolación predictiva de Hermite (anticipa el trazo).</summary>
    Predictive = 5,
    /// <summary>Raw Input HID sin latencia (WM_INPUT directo del driver).</summary>
    Raw = 6,
}

/// <summary>Opciones ajustables desde la GUI (persistidas en AppSettings).</summary>
public sealed class TrailOptions
{
    public TrailPreset Preset { get; set; } = TrailPreset.Cometa;
    /// <summary>Multiplicador de tamaño: 0.5 – 3.0.</summary>
    public float SizeScale { get; set; } = 1f;
    /// <summary>Multiplicador de duración: 0.3 – 2.5.</summary>
    public float LifeScale { get; set; } = 1f;
    /// <summary>Densidad de emisión: 0.1 – 3.0 (slider Cantidad).</summary>
    public float Density { get; set; } = 1f;
    /// <summary>Si true, los presets se tiñen con <see cref="CursorColor"/>.</summary>
    public bool UseCursorColor { get; set; } = false;
    public Color CursorColor { get; set; } = Color.Empty;
    /// <summary>
    /// Paleta activa (heredada del cursor o derivada del color manual).
    /// Null/vacía = usar paletas por defecto de cada preset.
    /// </summary>
    public Color[] CustomPalette { get; set; }
    public TrailOrigin Origin { get; set; } = TrailOrigin.Punta;

    /// <summary>Offset aplicado al origen según <see cref="Origin"/>.</summary>
    public (float X, float Y) OriginOffset =>
        Origin == TrailOrigin.Cola ? (16f, 20f) : (0f, 0f);
}

/// <summary>
/// Coordinador de estelas: captura (puntos), historial, delegación al
/// <see cref="Trails.ITrailStyle"/> activo y física configurable por el motor.
/// Thread-safe para el modo Async (un solo lock, sin anidamiento).
/// Sin dependencias de UI: se prueba headless con --trail-test.
/// </summary>
public sealed class TrailEngine : IDisposable
{
    private readonly TrailContext _ctx = new();
    private readonly Dictionary<TrailPreset, Trails.ITrailStyle> _styles = new();
    private Trails.ITrailStyle _active;
    private float _px, _py;
    private bool _hasPrev;
    private float _lastDx, _lastDy;
    private bool _disposed;

    /// <summary>Bloqueo para mutaciones concurrentes (modo Async).</summary>
    public readonly object Sync = new();

    // Física configurable por el motor activo (ver ITrailEngine.Activate).
    public float GravityY { get; set; }
    public bool BounceEdges { get; set; }
    public RectangleF BounceBounds { get; set; }
    public float MomentumBoost { get; set; } = 1f;

    public TrailOptions Options => _ctx.Options;
    public int MaxAlive { get => _ctx.MaxAlive; set => _ctx.MaxAlive = value; }

    public bool HasLive
    {
        get { lock (Sync) return Active.HasActivity(_ctx); }
    }

    public int AliveCount
    {
        get { lock (Sync) return _ctx.Particles.Count; }
    }

    /// <summary>Último punto de emisión (diagnóstico/test).</summary>
    public (float X, float Y) LastEmitPoint { get; private set; }

    /// <summary>Caja que encierra lo vivo (el overlay dimensiona con ella).</summary>
    public RectangleF GetLiveBounds()
    {
        lock (Sync) return SyncActive().GetBounds(_ctx);
    }

    public TrailEngine()
    {
        _active = Resolve(Options.Preset);
    }

    private Trails.ITrailStyle Active
    {
        get
        {
            var want = Options.Preset;
            if (!_styles.TryGetValue(want, out var style))
            {
                style = want <= TrailPreset.PixelGlitch
                    ? ClassicStyleFactory.Create(want)
                    : AdvancedStyleFactory.Create(want);
                _styles[want] = style;
            }
            return style;
        }
    }

    /// <summary>
    /// Sincroniza el estilo activo con Options.Preset (la UI y los tests
    /// pueden asignar el preset directamente). Llamar con el lock tomado.
    /// </summary>
    private Trails.ITrailStyle SyncActive() => _active = Active;

    /// <summary>Cambia de preset limpiando rastro anterior (sin sangrado).</summary>
    public void SetPreset(TrailPreset preset)
    {
        lock (Sync)
        {
            Options.Preset = preset;
            _ctx.Particles.Clear();
            _ctx.History.Clear();
            _active = Resolve(preset);
            _active.Reset();
        }
    }

    private Trails.ITrailStyle Resolve(TrailPreset preset)
    {
        if (!_styles.TryGetValue(preset, out var style))
        {
            style = preset <= TrailPreset.PixelGlitch
                ? ClassicStyleFactory.Create(preset)
                : AdvancedStyleFactory.Create(preset);
            _styles[preset] = style;
        }
        _active = style;
        return style;
    }

    /// <summary>Velocidad actual estimada del cursor (px/s).</summary>
    public (float Vx, float Vy) CursorVelocity(float dt) =>
        (Math.Clamp(_lastDx / Math.Max(dt, 1e-4f), -8000, 8000) * MomentumBoost,
         Math.Clamp(_lastDy / Math.Max(dt, 1e-4f), -8000, 8000) * MomentumBoost);

    /// <summary>Registra un punto capturado. Emisión estable, sin lerp.</summary>
    /// <returns>true si el cursor se movió (se generó trazo).</returns>
    public bool PushPoint(float cx, float cy)
    {
        lock (Sync)
        {
            float dx = _hasPrev ? cx - _px : 0;
            float dy = _hasPrev ? cy - _py : 0;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            bool moved = _hasPrev && dist > 0.75f;
            _lastDx = dx; _lastDy = dy;
            if (moved)
            {
                var (ox, oy) = Options.OriginOffset;
                float ex = cx + ox, ey = cy + oy;
                _ctx.CursorX = ex; _ctx.CursorY = ey;
                _ctx.CursorVx = dx * 60 * MomentumBoost;
                _ctx.CursorVy = dy * 60 * MomentumBoost;
                _ctx.History.Add((ex, ey, _ctx.Time));
                LastEmitPoint = (ex, ey);
                SyncActive().OnPush(_ctx, ex, ey, _ctx.CursorVx, _ctx.CursorVy, dist);
            }
            _px = cx; _py = cy; _hasPrev = true;
            return moved;
        }
    }

    private readonly List<(float X, float Y)> _raw = new();

    /// <summary>
    /// Variante suavizada (modo Spline): remuestrea el tramo con Catmull-Rom
    /// usando las últimas 4 muestras crudas. Solo entre muestras densas.
    /// </summary>
    public bool PushSmoothed(float cx, float cy)
    {
        lock (Sync)
        {
            float dx = _hasPrev ? cx - _px : 0;
            float dy = _hasPrev ? cy - _py : 0;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            bool moved = _hasPrev && dist > 0.75f;
            _lastDx = dx; _lastDy = dy;
            _px = cx; _py = cy; _hasPrev = true;
            if (!moved) return false;

            _raw.Add((cx, cy));
            if (_raw.Count > 4) _raw.RemoveAt(0);

            var (ox, oy) = Options.OriginOffset;
            if (_raw.Count < 4)
            {
                float ex = cx + ox, ey = cy + oy;
                _ctx.CursorX = ex; _ctx.CursorY = ey;
                _ctx.CursorVx = dx * 60 * MomentumBoost;
                _ctx.CursorVy = dy * 60 * MomentumBoost;
                _ctx.History.Add((ex, ey, _ctx.Time));
                LastEmitPoint = (ex, ey);
                SyncActive().OnPush(_ctx, ex, ey, _ctx.CursorVx, _ctx.CursorVy, dist);
                return true;
            }

            var (x0, y0) = _raw[0];
            var (x1, y1) = _raw[1];
            var (x2, y2) = _raw[2];
            var (x3, y3) = _raw[3];
            float seg = MathF.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            int steps = Math.Clamp((int)(seg / 4), 1, 6);
            float vx = (x2 - x1) * 60 * MomentumBoost;
            float vy = (y2 - y1) * 60 * MomentumBoost;
            _ctx.CursorVx = vx; _ctx.CursorVy = vy;
            for (int k = 1; k <= steps; k++)
            {
                float t = k / (float)steps;
                float ix = CatmullRom(x0, x1, x2, x3, t) + ox;
                float iy = CatmullRom(y0, y1, y2, y3, t) + oy;
                _ctx.CursorX = ix; _ctx.CursorY = iy;
                _ctx.History.Add((ix, iy, _ctx.Time));
                LastEmitPoint = (ix, iy);
                SyncActive().OnPush(_ctx, ix, iy, vx, vy, seg / steps);
            }
            return true;
        }
    }

    internal static float CatmullRom(float p0, float p1, float p2, float p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2 * p1) + (-p0 + p2) * t
            + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2
            + (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
    }

    /// <summary>Avanza tiempo, poda historial y física del estilo (DeltaTime real).</summary>
    public void Advance(float dt)
    {
        lock (Sync)
        {
            _ctx.Time += dt;
            _ctx.GravityY = GravityY;
            _ctx.BounceEdges = BounceEdges;
            _ctx.BounceBounds = BounceBounds;
            _ctx.Momentum = MomentumBoost;
            float histWindow = 0.55f * Options.LifeScale;
            while (_ctx.History.Count > 0 && _ctx.Time - _ctx.History[0].T > histWindow)
                _ctx.History.RemoveAt(0);
            // Tope escalado con la duración (estelas de hasta ~10 s).
            int histCap = Math.Clamp((int)(48 * Options.LifeScale), 48, 200);
            while (_ctx.History.Count > histCap)
                _ctx.History.RemoveAt(0);
            var vs = System.Windows.Forms.SystemInformation.VirtualScreen;
            _ctx.ScreenCenter = new PointF(vs.Left + vs.Width / 2f, vs.Top + vs.Height / 2f);
            SyncActive().Update(_ctx, dt);
            int cap = Math.Max(8, (int)Math.Round(_ctx.MaxAlive * Options.Density));
            while (_ctx.Particles.Count > cap)
                _ctx.Particles.RemoveAt(0);
        }
    }

    /// <summary>Compatibilidad: captura + avance en un paso (sondeo clásico).</summary>
    public bool Update(float cx, float cy, float dt)
    {
        bool moved = PushPoint(cx, cy);
        Advance(dt);
        return moved;
    }

    /// <summary>Dibuja en coordenadas de pantalla (el llamador aplica transform).</summary>
    public void Render(Graphics g)
    {
        lock (Sync)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            try { SyncActive().Draw(_ctx, g); }
            finally { g.SmoothingMode = old; }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (Sync)
        {
            foreach (var s in _styles.Values)
                try { s.Dispose(); } catch { }
            _styles.Clear();
        }
    }
}
