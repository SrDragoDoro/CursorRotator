using System.Collections.Concurrent;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using CursorRotator.Core;
using CursorRotator.Core.Trails;
using Microsoft.Win32;

namespace CursorRotator.UI;

/// <summary>
/// Overlay de estela: ventana flotante 100% click-through (WS_EX_TRANSPARENT |
/// WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_NOACTIVATE) de 360×360 px que sigue al
/// cursor y renderiza a ~60 FPS solo mientras hay movimiento o fundido.
/// En reposo (>1 s quieto y sin partículas) el hilo duerme: CPU ≈ 0.
/// </summary>
public sealed class TrailOverlay : Form
{
    private const int Box = 360;

    private readonly TrailEngine _engine = new();
    /// <summary>
    /// Segundo motor concurrente (dual trail): colección de partículas
    /// independiente con su propio preset, física y desvanecimiento.
    /// Se emite y renderiza junto al primario en el mismo doble búfer.
    /// </summary>
    private readonly TrailEngine _secondary = new();
    private Thread _thread;
    private ManualResetEvent _stop;
    private Bitmap _bmp;
    private Graphics _gbmp;
    private float _scale = 1f;
    private DateTime _lastActive = DateTime.UtcNow;
    private bool _started;

    // Captura: hook o Raw Input (hilo UI) encolan, el render consume en su hilo.
    private MouseHook _hook;
    private readonly ConcurrentQueue<(int X, int Y)> _points = new();
    private readonly List<(float X, float Y)> _batch = new();
    private int _lastX, _lastY;
    private bool _hookOk, _rawActive;
    private float _feedX, _feedY;
    private bool _hasFeed;
    // Supernova global: ráfaga en clic, independiente del preset.
    private readonly ClickEngine _clicks = new();

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TrailOptions Options => _engine.Options;
    public TrailEngine Core => _engine;

    /// <summary>Núcleo de la estela secundaria (colección independiente).</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TrailEngine SecondaryCore => _secondary;

    /// <summary>Activa la segunda colección de partículas concurrente.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool SecondaryEnabled { get; set; }

    /// <summary>Preset de la estela secundaria (independiente del primario).</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TrailPreset SecondaryPreset
    {
        get => _secondary.Options.Preset;
        set
        {
            if (_secondary.Options.Preset != value)
                _secondary.SetPreset(value);
        }
    }

    /// <summary>
    /// Copia los parámetros globales (tamaño, duración, densidad, origen,
    /// paleta/herencia de color) del motor primario al secundario.
    /// Cada colección conserva su preset y sus partículas por separado.
    /// </summary>
    public void SyncSecondaryFromPrimary()
    {
        var src = _engine.Options;
        var dst = _secondary.Options;
        dst.SizeScale = src.SizeScale;
        dst.LifeScale = src.LifeScale;
        dst.Density = src.Density;
        dst.Origin = src.Origin;
        dst.UseCursorColor = src.UseCursorColor;
        dst.CursorColor = src.CursorColor;
        dst.CustomPalette = src.CustomPalette;
        _secondary.GravityY = _engine.GravityY;
        _secondary.BounceEdges = _engine.BounceEdges;
        _secondary.BounceBounds = _engine.BounceBounds;
        _secondary.MomentumBoost = _engine.MomentumBoost;
    }

    /// <summary>Reaplica z-order topmost (modo pantalla completa / juegos).</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ForceTopmost { get; set; }
    /// <summary>0 = auto por monitor, -1 = uncapped, otro = tope en Hz.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int RefreshCapHz { get; set; }
    /// <summary>Multiplicador de tamaño de los efectos de clic.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float ClickSizeScale { get; set; } = 1f;
    /// <summary>Si true, los clics heredan la paleta de la estela.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ClickUseTrailPalette { get; set; } = true;

    private readonly Dictionary<TrailCaptureMode, ITrailEngine> _engines = new();
    private ITrailEngine _activeEngine;
    private TrailCaptureMode _captureMode = TrailCaptureMode.Timer;

    private ITrailEngine ActiveEngine
    {
        get
        {
            if (!_engines.TryGetValue(_captureMode, out var e))
            {
                e = TrailEngineFactory.Create(_captureMode);
                _engines[_captureMode] = e;
            }
            return e;
        }
    }

    /// <summary>
    /// Motor de captura/render. Timer = sondeo por frame (predeterminado);
    /// Hook/Spline/Inercial/Async/Predictivo usan hook LL; Raw usa WM_INPUT.
    /// Conmutable en vivo.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TrailCaptureMode CaptureMode
    {
        get => _captureMode;
        set
        {
            if (_captureMode == value) return;
            try { _activeEngine?.Deactivate(_engine); } catch { }
            _captureMode = value;
            _activeEngine = ActiveEngine;
            try { _activeEngine.Activate(_engine); } catch { }
            if (!_started) return;
            ApplyCaptureSource();
            if (_activeEngine is AsyncEngine ae)
                ae.Start(_engine, BitmapSize, _scale, RefreshCapHz, _clicks);
        }
    }

    /// <summary>Instala la fuente de captura del modo actual (hilo UI).</summary>
    private void ApplyCaptureSource()
    {
        DisableCapture();
        // El hook siempre se instala (los clics alimentan la supernova global).
        EnsureHook();
        if (_captureMode == TrailCaptureMode.Raw)
        {
            try { _rawActive = RawInputMouse.SetEnabled(Handle, true); }
            catch { _rawActive = false; }
        }
    }

    private void DisableCapture()
    {
        try { RawInputMouse.SetEnabled(Handle, false); } catch { }
        _rawActive = false;
        try { _hook?.Dispose(); } catch { }
        _hook = null;
        _hookOk = false;
    }

    protected override void WndProc(ref Message m)
    {
        // WM_INPUT del driver HID → posición absoluta actual a la cola.
        // Absoluto (tableta/digitalizador) manda: ya viene en pantalla.
        if (m.Msg == RawInputMouse.WM_INPUT && _rawActive && _started)
        {
            try
            {
                if (RawInputMouse.TryGetAbsolutePosition(m.LParam, out int ax, out int ay))
                    _points.Enqueue((ax, ay));
                else if (RawInputMouse.HandleMessage(m.LParam))
                {
                    NativeLayer.GetCursorPos(out int rx, out int ry);
                    _points.Enqueue((rx, ry));
                }
            }
            catch { /* nunca bloquear el mensaje del sistema */ }
        }
        base.WndProc(ref m);
    }

    private int BitmapSize => Math.Max(64, (int)Math.Ceiling(Box * _scale));

    private void EnsureHook()
    {
        // Sin guarda de _started: se llama desde Show() (aún no arrancado) y
        // desde cambios de motor en vivo. El campo _hook retiene el delegado
        // para que el GC nunca lo limpie mientras esté instalado.
        if (_hookOk) return;
        try
        {
            _hook = new MouseHook();
            _hook.MouseMoved += (x, y) => _points.Enqueue((x, y));
            _hook.MouseDown += (x, y) =>
            {
                try
                {
                    _clicks.SizeScale = ClickSizeScale;
                    _clicks.UseTrailPalette = ClickUseTrailPalette;
                    _clicks.TrailPalette = Options.CustomPalette;
                    _clicks.Trigger(x, y);
                }
                catch { }
            };
            _hook.Install();
            _hookOk = true;
        }
        catch
        {
            // Fallback silencioso a sondeo: el render sigue funcionando.
            _hookOk = false;
            _hook?.Dispose();
            _hook = null;
        }
    }

    public TrailOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        // Caja inicial = escritorio virtual completo (orígenes negativos
        // incluidos): el overlay nunca se ancla solo a la primaria.
        try { Bounds = SystemInformation.VirtualScreen; }
        catch { Size = new Size(Box, Box); }
        BackColor = Color.Black;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_LAYERED = 0x00080000;
            const int WS_EX_TRANSPARENT = 0x00000020;
            const int WS_EX_TOPMOST = 0x00000008;
            const int WS_EX_NOACTIVATE = 0x08000000;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST
                        | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try { Bounds = SystemInformation.VirtualScreen; } catch { }
        _scale = DeviceDpi / 96f;
        RecreateBitmap();
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
    }

    private void OnDisplayChanged(object sender, EventArgs e)
    {
        _scale = DeviceDpi / 96f;
        RecreateBitmap();
    }

    private void RecreateBitmap()
    {
        lock (this)
        {
            _gbmp?.Dispose();
            _bmp?.Dispose();
            int s = Math.Max(1, (int)Math.Ceiling(Box * _scale));
            _bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
            _gbmp = Graphics.FromImage(_bmp);
        }
    }

    public new void Show()
    {
        base.Show();
        if (_started) return;
        _activeEngine = ActiveEngine;
        try { _activeEngine.Activate(_engine); } catch { }
        // Siempre: el hook alimenta los clics en TODOS los modos (en Timer el
        // movimiento se sondea, pero los clics siguen viniendo del hook).
        ApplyCaptureSource(); // corre en hilo UI (message pump)
        if (_activeEngine is AsyncEngine ae0)
            ae0.Start(_engine, BitmapSize, _scale, RefreshCapHz, _clicks);
        _started = true;
        _stop = new ManualResetEvent(false);
        StartTopmostEnforcer();
        _thread = new Thread(RenderLoop) { IsBackground = true, Name = "TrailRender" };
        _thread.Start();
    }

    public void HideOverlay()
    {
        try
        {
            _stop?.Set();
            if (_thread is not null && _thread.IsAlive)
                _thread.Join(1500);
        }
        catch { /* cierre best-effort */ }
        StopTopmostEnforcer();
        try { _activeEngine?.Deactivate(_engine); } catch { }
        DisableCapture();
        _clicks.Clear();
        try { _secondary.SetPreset(_secondary.Options.Preset); } catch { }
        _started = false;
        if (!IsDisposed) Hide();
    }

    // ---------- Forzar topmost (juegos / pantalla completa) ----------
    private System.Threading.Timer _topmostTimer;

    /// <summary>
    /// Reaplica z-order topmost (modo pantalla completa / juegos).
    /// (No aplica a fullscreen EXCLUSIVO: ahí el DWM no compone overlays.)
    /// </summary>
    private void StartTopmostEnforcer()
    {
        StopTopmostEnforcer();
        _topmostTimer = new System.Threading.Timer(_ =>
        {
            try
            {
                if (ForceTopmost && !IsDisposed && IsHandleCreated && Visible)
                    NativeMethods.ForceTopmost(Handle);
            }
            catch { }
        }, null, 2000, 2000);
    }

    private void StopTopmostEnforcer()
    {
        try { _topmostTimer?.Dispose(); } catch { }
        _topmostTimer = null;
    }

    private void RenderLoop()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long prevTicks = sw.ElapsedTicks;
        int consecutiveErrors = 0;
        while (!_stop.WaitOne(0))
        {
            try
            {
                long now = sw.ElapsedTicks;
                float dt = Math.Min(0.1f, (float)((now - prevTicks) / (double)System.Diagnostics.Stopwatch.Frequency));
                prevTicks = now;

                // Producir puntos: eventos (hook/Raw) drenados o sondeo Timer.
                var batch = _batch;
                batch.Clear();
                bool eventDriven = (_hookOk || _rawActive) && _captureMode != TrailCaptureMode.Timer;
                if (eventDriven)
                {
                    // Ratón gaming a 1000 Hz: descartar lo más viejo si nos saturamos.
                    while (_points.Count > 64 && _points.TryDequeue(out _)) { }
                    int drained = 0;
                    while (drained < 16 && _points.TryDequeue(out var pt))
                    {
                        drained++;
                        _lastX = pt.X; _lastY = pt.Y;
                        batch.Add((pt.X, pt.Y));
                    }
                    if (batch.Count == 0)
                    {
                        // Saneamiento anti-deriva: sin eventos, la cabeza se
                        // pega a la posición lógica de Windows (GetCursorPos),
                        // inmune a drivers ruidosos o aceleraciones raras.
                        NativeLayer.GetCursorPos(out int gx, out int gy);
                        _lastX = gx; _lastY = gy;
                        batch.Add((gx, gy));
                    }
                }
                else
                {
                    NativeLayer.GetCursorPos(out _lastX, out _lastY);
                    batch.Add((_lastX, _lastY));
                }

                // Escala DPI viva: si la ventana cambió de monitor, el bitmap
                // se adapta solo (EnsureBitmap ya redimensiona por demanda).
                try
                {
                    float live = DeviceDpi / 96f;
                    if (live > 0 && Math.Abs(live - _scale) > 0.001f)
                        _scale = live;
                }
                catch { }

                if (_activeEngine is AsyncEngine ae)
                {
                    // Motor asíncrono: alimentar y blitear el frame pre-renderizado.
                    foreach (var (bx, by) in batch)
                    {
                        ae.Push(_engine, bx, by);
                        if (SecondaryEnabled)
                            _secondary.PushPoint(bx, by);
                    }
                    if (SecondaryEnabled)
                        _secondary.Advance(Math.Min(0.1f, (float)((sw.ElapsedTicks - now) / (double)System.Diagnostics.Stopwatch.Frequency)));
                    if (ae.WaitFrame(60))
                    {
                        var (ccx, ccy) = ae.LastCursor;
                        _lastX = (int)ccx; _lastY = (int)ccy;
                        _lastActive = DateTime.UtcNow;
                        var win = ae.LastWindow;
                        ae.Blit(bmp =>
                        {
                            // Dual trail en modo Async: compone la secundaria
                            // sobre el frame del worker con el mismo transform.
                            if (SecondaryEnabled)
                            {
                                try
                                {
                                    using var g = Graphics.FromImage(bmp);
                                    var m = g.Transform;
                                    try
                                    {
                                        g.TranslateTransform(-win.X * _scale, -win.Y * _scale);
                                        g.ScaleTransform(_scale, _scale);
                                        _secondary.Render(g);
                                    }
                                    finally { g.Transform = m; }
                                }
                                catch { }
                            }
                            PaintBitmap(bmp, win.X, win.Y);
                        });
                    }
                    else if (_stop.WaitOne(16))
                    {
                        break;
                    }
                    continue;
                }

                bool moved = false;
                foreach (var (bx, by) in batch)
                {
                    float ddx = bx - _feedX, ddy = by - _feedY;
                    if (!_hasFeed || MathF.Sqrt(ddx * ddx + ddy * ddy) > 0.75f)
                        moved = true;
                    _feedX = bx; _feedY = by; _hasFeed = true;
                    _activeEngine.Push(_engine, bx, by);
                    if (SecondaryEnabled)
                    {
                        if (_captureMode == TrailCaptureMode.Spline)
                            _secondary.PushSmoothed(bx, by);
                        else
                            _secondary.PushPoint(bx, by);
                    }
                }
                _engine.Advance(dt);
                if (SecondaryEnabled)
                    _secondary.Advance(dt);
                _clicks.Update(dt); // la rama Async hace continue antes: aquí solo síncrono
                if (moved || _clicks.HasActivity) _lastActive = DateTime.UtcNow;

                bool active = _engine.HasLive || _clicks.HasActivity ||
                              (SecondaryEnabled && _secondary.HasLive) ||
                              (DateTime.UtcNow - _lastActive).TotalMilliseconds < 400;
                if (!active)
                {
                    // Reposo: duerme y reintenta 4 veces por segundo (despertar inmediato al mover).
                    if (_stop.WaitOne(250)) break;
                    continue;
                }

                DrawFrame(_lastX, _lastY);
                consecutiveErrors = 0;

                // Frame pacing dinámico: budget según el monitor bajo el cursor.
                int budget = FramePacer.GetFrameBudgetMs(
                    new Point(_lastX, _lastY), RefreshCapHz);
                int elapsed = (int)((sw.ElapsedTicks - now) * 1000 / System.Diagnostics.Stopwatch.Frequency);
                int wait = budget - elapsed;
                if (wait > 0 && _stop.WaitOne(wait)) break;
            }
            catch (Exception ex)
            {
                // Un frame fallido jamás debe cerrar la app: se registra y se sigue.
                consecutiveErrors++;
                LogRenderError(ex);
                if (_stop.WaitOne(Math.Min(2000, 250 * consecutiveErrors))) break;
            }
        }
    }

    private static DateTime _lastErrorLog = DateTime.MinValue;

    private static void LogRenderError(Exception ex)
    {
        // Throttle: como máximo 1 línea por segundo para no inundar el disco.
        if (DateTime.UtcNow - _lastErrorLog < TimeSpan.FromSeconds(1)) return;
        _lastErrorLog = DateTime.UtcNow;
        try
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [overlay] {ex.Message}{Environment.NewLine}");
        }
        catch { }
    }

    private void DrawFrame(int cx, int cy)
    {
        // Caja dinámica: encierra lo vivo (nunca recorta) con mínimo de 360
        // centrado en el cursor; recortada a la pantalla virtual.
        var vs = SystemInformation.VirtualScreen;
        var live = _engine.GetLiveBounds();
        if (SecondaryEnabled)
        {
            var live2 = _secondary.GetLiveBounds();
            if (!live2.IsEmpty)
                live = live.IsEmpty ? live2 : RectangleF.Union(live, live2);
        }
        var boom = _clicks.GetBounds();
        if (!boom.IsEmpty)
            live = live.IsEmpty ? boom : RectangleF.Union(live, boom);
        float needW = 0, needH = 0, ccx = cx, ccy = cy;
        if (!live.IsEmpty)
        {
            needW = live.Width + 48;
            needH = live.Height + 48;
            ccx = live.X + live.Width / 2;
            ccy = live.Y + live.Height / 2;
        }
        int winW = Math.Max(Box, (int)Math.Ceiling(needW));
        int winH = Math.Max(Box, (int)Math.Ceiling(needH));
        winW = Math.Min(winW, vs.Width);
        winH = Math.Min(winH, vs.Height);
        int wx = Math.Clamp((int)(ccx - winW / 2f), vs.Left, vs.Right - winW);
        int wy = Math.Clamp((int)(ccy - winH / 2f), vs.Top, vs.Bottom - winH);

        lock (this)
        {
            if (IsDisposed) return;
            EnsureBitmap(winW, winH);
            if (_bmp is null) return;
            _gbmp.Clear(Color.Transparent);
            var m = _gbmp.Transform;
            try
            {
                // Normalización pantalla→bitmap (Pixel-Perfect Tracking):
                // el motor vive en coords de pantalla (físicas con
                // PerMonitorV2); aquí se resta el origen de la ventana
                // layered y se escala al DPI real. Equivale a
                // CursorPointMapper.ToLocal(pantalla, VirtualScreen) × escala.
                // NUNCA se mezcla con coords virtualizadas (ver manifest +
                // ApplicationHighDpiMode=PerMonitorV2).
                var off = CursorPointMapper.ToLayerOffset(wx, wy, _scale);
                _gbmp.TranslateTransform(off.X, off.Y);
                _gbmp.ScaleTransform(_scale, _scale);
                _engine.Render(_gbmp);
                if (SecondaryEnabled)
                    _secondary.Render(_gbmp);
                _clicks.Draw(_gbmp);
            }
            finally { _gbmp.Transform = m; }

            PaintBitmap(_bmp, wx, wy);
        }
    }

    /// <summary>Motor de clics (la UI empuja Enabled/SelectedIndex).</summary>
    public ClickEngine Clicks => _clicks;

    /// <summary>Crea el bitmap solo si cambian las dimensiones.</summary>
    private void EnsureBitmap(int winW, int winH)
    {
        int pw = Math.Max(64, (int)Math.Ceiling(winW * _scale));
        int ph = Math.Max(64, (int)Math.Ceiling(winH * _scale));
        if (_bmp is not null && _bmp.Width == pw && _bmp.Height == ph) return;
        _gbmp?.Dispose();
        _bmp?.Dispose();
        _bmp = new Bitmap(pw, ph, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        _gbmp = Graphics.FromImage(_bmp);
    }

    /// <summary>Transfiere un bitmap ya renderizado a la ventana layered.</summary>
    private void PaintBitmap(Bitmap bmp, int x, int y)
    {
        if (IsDisposed) return;
        var hBmp = bmp.GetHbitmap(Color.FromArgb(0));
        try
        {
            NativeLayer.PaintLayered(Handle, hBmp, x, y, bmp.Width, bmp.Height);
        }
        finally { NativeLayer.DeleteObject(hBmp); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { HideOverlay(); } catch { }
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            lock (this)
            {
                _gbmp?.Dispose();
                _bmp?.Dispose();
            }
            foreach (var e in _engines.Values)
                try { e.Dispose(); } catch { }
            _engines.Clear();
            try { _engine.Dispose(); } catch { }
            try { _secondary.Dispose(); } catch { }
            _stop?.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>P/Invoke para cursor, DCs y ventana layered con alfa por píxel.</summary>
internal static class NativeLayer
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE { public int Cx, Cy; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BLENDFUNCTION
    {
        public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    public static void GetCursorPos(out int x, out int y)
    {
        GetCursorPos(out POINT p);
        x = p.X; y = p.Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(
        IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, uint crKey,
        ref BLENDFUNCTION pblend, uint dwFlags);

    private const uint ULW_ALPHA = 0x2;
    private const byte AC_SRC_ALPHA = 0x1;

    public static void PaintLayered(IntPtr hwnd, IntPtr hBitmap, int x, int y, int w, int h)
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        IntPtr old = SelectObject(memDc, hBitmap);
        try
        {
            var dst = new POINT { X = x, Y = y };
            var size = new SIZE { Cx = w, Cy = h };
            var src = new POINT { X = 0, Y = 0 };
            var blend = new BLENDFUNCTION
            {
                BlendOp = 0,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = AC_SRC_ALPHA,
            };
            UpdateLayeredWindow(hwnd, screenDc, ref dst, ref size,
                memDc, ref src, 0, ref blend, ULW_ALPHA);
        }
        finally
        {
            SelectObject(memDc, old);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}
