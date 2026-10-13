using System.Collections.Concurrent;
using System.Drawing.Imaging;
using System.Windows.Forms;
using CursorRotator.Core;

namespace CursorRotator.Core.Trails;

/// <summary>
/// Contrato de todo motor de estelas: configura la física del núcleo,
/// alimenta puntos capturados y restaura al salir.
/// </summary>
public interface ITrailEngine : IDisposable
{
    TrailCaptureMode Mode { get; }
    void Activate(TrailEngine core);
    void Push(TrailEngine core, float x, float y);
    void Deactivate(TrailEngine core);
}

/// <summary>Base: por defecto no toca la física del núcleo.</summary>
public abstract class TrailEngineBase : ITrailEngine
{
    public abstract TrailCaptureMode Mode { get; }
    public virtual void Activate(TrailEngine core) { }
    public abstract void Push(TrailEngine core, float x, float y);
    public virtual void Deactivate(TrailEngine core)
    {
        core.GravityY = 0;
        core.BounceEdges = false;
        core.MomentumBoost = 1f;
    }
    public virtual void Dispose() { }
}

/// <summary>Motor 1 (predeterminado): sondeo clásico por frame, física neutra.</summary>
internal sealed class TimerEngine : TrailEngineBase
{
    public override TrailCaptureMode Mode => TrailCaptureMode.Timer;
    public override void Push(TrailEngine core, float x, float y) => core.PushPoint(x, y);
}

/// <summary>Motor 2: eventos del hook de alta frecuencia, física neutra.</summary>
internal sealed class HookEngine : TrailEngineBase
{
    public override TrailCaptureMode Mode => TrailCaptureMode.Hook;
    public override void Push(TrailEngine core, float x, float y) => core.PushPoint(x, y);
}

/// <summary>Motor 3: hook + remuestreo Catmull-Rom entre muestras densas.</summary>
internal sealed class SplineEngine : TrailEngineBase
{
    public override TrailCaptureMode Mode => TrailCaptureMode.Spline;
    public override void Push(TrailEngine core, float x, float y) => core.PushSmoothed(x, y);
}

/// <summary>
/// Motor 4: física con inercia. Las partículas heredan el momentum del ratón
/// (×1.35), con gravedad virtual y rebote en los bordes de la pantalla.
/// </summary>
internal sealed class InertialEngine : TrailEngineBase
{
    public override TrailCaptureMode Mode => TrailCaptureMode.Inertial;
    public override void Activate(TrailEngine core)
    {
        core.GravityY = 560f;
        core.BounceEdges = true;
        core.BounceBounds = SystemInformation.VirtualScreen;
        core.MomentumBoost = 1.35f;
    }
    public override void Push(TrailEngine core, float x, float y) => core.PushPoint(x, y);
}

/// <summary>
/// Motor 5: render asíncrono. La captura encola y un hilo worker integra +
/// renderiza a su propio bitmap (doble búfer); el overlay solo transfiere
/// el frame listo con UpdateLayeredWindow, sin tocar GDI+ pesado.
/// </summary>
internal sealed class AsyncEngine : ITrailEngine
{
    public TrailCaptureMode Mode => TrailCaptureMode.Async;

    private readonly ConcurrentQueue<(float X, float Y)> _inbox = new();
    private Thread _worker;
    private ManualResetEvent _stop;
    private readonly ManualResetEvent _frameReady = new(false);
    private readonly object _bmpLock = new();
    private Bitmap _bmp;
    private Graphics _gbmp;
    private int _boxPx = 360;
    private float _scale = 1f;
    private float _lx, _ly;
    private TrailEngine _core;
    private ClickEngine _clicks;

    /// <summary>0 = auto por monitor, -1 = uncapped, otro = tope en Hz.</summary>
    public int RefreshCapHz { get; set; }

    public void Activate(TrailEngine core) { }

    /// <summary>Arranca el worker con el núcleo compartido (mismo Options).</summary>
    public void Start(TrailEngine core, int boxPx, float scale, int refreshCapHz = 0,
        ClickEngine clicks = null)
    {
        Stop();
        _core = core;
        RefreshCapHz = refreshCapHz;
        _clicks = clicks;
        _boxPx = Math.Max(64, boxPx);
        _scale = scale;
        lock (_bmpLock)
        {
            _gbmp?.Dispose();
            _bmp?.Dispose();
            _bmp = new Bitmap(_boxPx, _boxPx, PixelFormat.Format32bppArgb);
            _gbmp = Graphics.FromImage(_bmp);
        }
        _stop = new ManualResetEvent(false);
        _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "TrailAsync" };
        _worker.Start();
    }

    public void Push(TrailEngine core, float x, float y)
    {
        while (_inbox.Count > 64 && _inbox.TryDequeue(out _)) { }
        _inbox.Enqueue((x, y));
        _lx = x; _ly = y;
    }

    public (float X, float Y) LastCursor => (_lx, _ly);

    /// <summary>Espera un frame listo (ms). True = hay frame nuevo para blitear.</summary>
    public bool WaitFrame(int timeoutMs) => _frameReady.WaitOne(timeoutMs);

    public void Blit(Action<Bitmap> paint)
    {
        lock (_bmpLock)
        {
            if (_bmp is null) return;
            paint(_bmp);
        }
        _frameReady.Reset();
    }

    private void WorkerLoop()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long prev = sw.ElapsedTicks;
        while (!_stop.WaitOne(0))
        {
            try
            {
                long now = sw.ElapsedTicks;
                float dt = Math.Min(0.1f,
                    (float)((now - prev) / (double)System.Diagnostics.Stopwatch.Frequency));
                prev = now;

                int drained = 0;
                while (drained < 16 && _inbox.TryDequeue(out var pt))
                {
                    drained++;
                    _core.PushPoint(pt.X, pt.Y);
                }
                _core.Advance(dt);
                _clicks?.Update(dt);

                if (_core.HasLive || (_clicks?.HasActivity == true))
                {
                    // Caja dinámica igual que el overlay síncrono (anti-clipping).
                    var vs = SystemInformation.VirtualScreen;
                    var live = _core.GetLiveBounds();
                    if (_clicks is not null)
                    {
                        var boom = _clicks.GetBounds();
                        if (!boom.IsEmpty)
                            live = live.IsEmpty ? boom : RectangleF.Union(live, boom);
                    }
                    float needW = 0, needH = 0, ccx = _lx, ccy = _ly;
                    if (!live.IsEmpty)
                    {
                        needW = live.Width + 48;
                        needH = live.Height + 48;
                        ccx = live.X + live.Width / 2;
                        ccy = live.Y + live.Height / 2;
                    }
                    int winW = Math.Min(vs.Width, Math.Max(360, (int)Math.Ceiling(needW)));
                    int winH = Math.Min(vs.Height, Math.Max(360, (int)Math.Ceiling(needH)));
                    int wx = (int)Math.Clamp(ccx - winW / 2f, vs.Left, vs.Right - winW);
                    int wy = (int)Math.Clamp(ccy - winH / 2f, vs.Top, vs.Bottom - winH);
                    int pw = Math.Max(64, (int)Math.Ceiling(winW * _scale));
                    int ph = Math.Max(64, (int)Math.Ceiling(winH * _scale));
                    lock (_bmpLock)
                    {
                        if (_bmp is null || _bmp.Width != pw || _bmp.Height != ph)
                        {
                            _gbmp?.Dispose();
                            _bmp?.Dispose();
                            _bmp = new Bitmap(pw, ph, PixelFormat.Format32bppArgb);
                            _gbmp = Graphics.FromImage(_bmp);
                        }
                        _gbmp.Clear(Color.Transparent);
                        var m = _gbmp.Transform;
                        try
                        {
                            _gbmp.TranslateTransform(-wx * _scale, -wy * _scale);
                            _gbmp.ScaleTransform(_scale, _scale);
                            _core.Render(_gbmp);
                            Color ring = Color.White;
                            var opts = _core.Options;
                            if (opts.UseCursorColor && !opts.CursorColor.IsEmpty)
                                ring = opts.CursorColor;
                            _clicks?.Draw(_gbmp);
                        }
                        finally { _gbmp.Transform = m; }
                    }
                    LastWindow = (wx, wy);
                    _frameReady.Set();
                    // Pacing del worker: mismo budget que el loop principal.
                    int budget = FramePacer.GetFrameBudgetMs(
                        new Point((int)_lx, (int)_ly), RefreshCapHz);
                    int elapsed = (int)((sw.ElapsedTicks - now) * 1000
                        / System.Diagnostics.Stopwatch.Frequency);
                    if (budget - elapsed > 0 && _stop.WaitOne(budget - elapsed))
                        break;
                }
                else if (_stop.WaitOne(60))
                {
                    break;
                }
            }
            catch
            {
                if (_stop.WaitOne(250)) break;
            }
        }
    }

    public (int X, int Y) LastWindow { get; private set; }

    public void Deactivate(TrailEngine core) => Stop();

    public void Stop()
    {
        try
        {
            _stop?.Set();
            if (_worker is not null && _worker.IsAlive)
                _worker.Join(1500);
        }
        catch { }
    }

    public void Dispose()
    {
        Stop();
        lock (_bmpLock)
        {
            _gbmp?.Dispose();
            _bmp?.Dispose();
        }
        _stop?.Dispose();
        _frameReady.Dispose();
    }
}

/// <summary>
/// Motor 6: interpolación predictiva de Hermite. Estima velocidad y
/// aceleración del cursor y evalúa la cúbica slightly en el futuro
/// (s=1.18), anticipando microscópicamente el trazo al hardware.
/// </summary>
internal sealed class PredictiveEngine : TrailEngineBase
{
    public override TrailCaptureMode Mode => TrailCaptureMode.Predictive;

    private float _p0x, _p0y, _p1x, _p1y;
    private float _v0x, _v0y, _v1x, _v1y;
    private long _t0, _t1;
    private bool _hasPrev;
    private const float LookAhead = 1.18f;

    public override void Push(TrailEngine core, float x, float y)
    {
        long now = Environment.TickCount64;
        if (!_hasPrev)
        {
            _p0x = _p1x = x; _p0y = _p1y = y;
            _t0 = _t1 = now;
            _hasPrev = true;
            core.PushPoint(x, y);
            return;
        }
        float dt = Math.Max(1e-3f, (now - _t1) / 1000f);
        float nvx = (x - _p1x) / dt, nvy = (y - _p1y) / dt;
        float seg = Math.Max(1e-3f, (_t1 - _t0) / 1000f);

        // Tangentes de Hermite (velocidad × tiempo de segmento).
        float m0x = _v1x * seg, m0y = _v1y * seg;
        float m1x = nvx * seg, m1y = nvy * seg;
        float s = LookAhead;
        float px = Hermite(_p0x, _p1x, m0x, m1x, s);
        float py = Hermite(_p0y, _p1y, m0y, m1y, s);

        _p0x = _p1x; _p0y = _p1y;
        _p1x = x; _p1y = y;
        _v0x = _v1x; _v0y = _v1y;
        _v1x = nvx; _v1y = nvy;
        _t0 = _t1; _t1 = now;

        core.PushPoint(px, py);
    }

    private static float Hermite(float p0, float p1, float m0, float m1, float s)
    {
        float s2 = s * s, s3 = s2 * s;
        return (2 * s3 - 3 * s2 + 1) * p0 + (s3 - 2 * s2 + s) * m0
             + (-2 * s3 + 3 * s2) * p1 + (s3 - s2) * m1;
    }

    public override void Deactivate(TrailEngine core)
    {
        base.Deactivate(core);
        _hasPrev = false;
    }
}

/// <summary>
/// Motor 7: Raw Input HID (cero latencia). La captura la hace el overlay vía
/// WM_INPUT; aquí solo se alimenta el núcleo punto a punto.
/// </summary>
internal sealed class RawEngine : TrailEngineBase
{
    public override TrailCaptureMode Mode => TrailCaptureMode.Raw;
    public override void Push(TrailEngine core, float x, float y) => core.PushPoint(x, y);
}

internal static class TrailEngineFactory
{
    public static ITrailEngine Create(TrailCaptureMode mode) => mode switch
    {
        TrailCaptureMode.Hook => new HookEngine(),
        TrailCaptureMode.Spline => new SplineEngine(),
        TrailCaptureMode.Inertial => new InertialEngine(),
        TrailCaptureMode.Async => new AsyncEngine(),
        TrailCaptureMode.Predictive => new PredictiveEngine(),
        TrailCaptureMode.Raw => new RawEngine(),
        _ => new TimerEngine(),
    };
}
