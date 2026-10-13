namespace CursorRotator.Core;

/// <summary>
/// Rotación automática en hilo de fondo con COLA real: baraja el pool y lo
/// recorre sin repetir hasta agotarlo (entonces rebaraja). PeekNext() es el
/// próximo exacto (para la vista previa). Start/Stop seguros.
/// </summary>
public sealed class CursorRotator : IDisposable
{
    private System.Threading.Timer _timer;
    private readonly object _gate = new();
    private readonly Queue<string> _queue = new();
    private List<string> _pool = new();
    private TimeSpan _period = TimeSpan.FromMinutes(15);
    private Func<DateTime, (DateTime At, string Scheme)> _scheduler;
    private string _pendingScheme;
    private bool _disposed;

    public bool IsRunning { get; private set; }
    public string CurrentScheme { get; private set; }
    public DateTimeOffset? LastChangeAt { get; private set; }
    public DateTimeOffset? NextChangeAt { get; private set; }

    public event Action<string> SchemeApplied;
    public event Action<string> Failed;

    public void Start(IEnumerable<string> pool, TimeSpan interval) =>
        Start(pool, TimeSpan.Zero, interval);

    /// <summary>
    /// Arranque con primer disparo diferido (p. ej. hora fija diaria con
    /// periodo de 24 h). dueTime.Zero = disparo inmediato como antes.
    /// </summary>
    public void Start(IEnumerable<string> pool, TimeSpan dueTime, TimeSpan period)
    {
        lock (_gate)
        {
            var list = pool.Where(SchemeStore.Exists).ToList();
            if (list.Count == 0)
                throw new InvalidOperationException("No hay esquemas instalados para rotar.");
            StopLocked();
            _pool = list;
            _queue.Clear();
            RefillLocked();
            _period = period;
            _scheduler = null;
            _pendingScheme = null;
            _timer = new System.Threading.Timer(_ => Tick(), null, dueTime, period);
            NextChangeAt = DateTimeOffset.Now + (dueTime > TimeSpan.Zero ? dueTime : period);
            IsRunning = true;
        }
    }

    /// <summary>
    /// Arranque por agenda: cada disparo aplica el cursor programado para esa
    /// hora (o uno de la cola si es aleatorio) y reprograma el siguiente.
    /// El planificador recibe "ahora" y devuelve (próxima hora, esquema o
    /// null); hilo-seguro: no debe tocar UI (pasar foto).
    /// </summary>
    public void Start(IEnumerable<string> pool,
                      Func<DateTime, (DateTime At, string Scheme)> scheduler)
    {
        lock (_gate)
        {
            var list = pool.Where(SchemeStore.Exists).ToList();
            if (list.Count == 0)
                throw new InvalidOperationException("No hay esquemas instalados para rotar.");
            StopLocked();
            _pool = list;
            _queue.Clear();
            RefillLocked();
            _period = TimeSpan.FromHours(24);
            _scheduler = scheduler;
            var (at, scheme) = scheduler(DateTime.Now);
            _pendingScheme = scheme;
            _timer = new System.Threading.Timer(_ => Tick(), null,
                DueFromNow(at), System.Threading.Timeout.InfiniteTimeSpan);
            NextChangeAt = new DateTimeOffset(at);
            IsRunning = true;
        }
    }

    private static TimeSpan DueFromNow(DateTime at)
    {
        var due = at - DateTime.Now;
        return due < TimeSpan.Zero ? TimeSpan.Zero : due;
    }

    /// <summary>Baraja el pool evitando que el primero repita el actual.</summary>
    private void RefillLocked()
    {
        var shuffled = _pool.OrderBy(_ => Random.Shared.Next()).ToList();
        if (shuffled.Count > 1 && shuffled[0] == CurrentScheme)
            (shuffled[0], shuffled[^1]) = (shuffled[^1], shuffled[0]);
        foreach (var s in shuffled)
            _queue.Enqueue(s);
    }

    /// <summary>Próximo esquema exacto sin consumirlo (null si no hay).</summary>
    public string PeekNext()
    {
        lock (_gate)
        {
            if (_queue.Count > 0) return _queue.Peek();
            return _pool.Count > 0 ? _pool[0] : null;
        }
    }

    /// <summary>
    /// Extrae el siguiente de la cola (el que muestra la vista previa como
    /// "Siguiente"). Rebaraja si se agotó. Null si no hay pool.
    /// </summary>
    public string TakeNext()
    {
        lock (_gate)
        {
            if (_queue.Count == 0)
            {
                if (_pool.Count == 0) return null;
                RefillLocked();
            }
            return _queue.Dequeue();
        }
    }

    public void Stop()
    {
        lock (_gate) StopLocked();
    }

    private void StopLocked()
    {
        _timer?.Dispose();
        _timer = null;
        _scheduler = null;
        _pendingScheme = null;
        IsRunning = false;
        NextChangeAt = null;
    }

    private void Tick()
    {
        try
        {
            string pick;
            lock (_gate)
            {
                // Agenda con destino fijo: aplica ese esquema sin consumir
                // la cola (si ya no existe, cae a la cola como aleatorio).
                if (!string.IsNullOrEmpty(_pendingScheme) && SchemeStore.Exists(_pendingScheme))
                    pick = _pendingScheme;
                else if (_queue.Count == 0)
                {
                    if (_pool.Count == 0) return;
                    RefillLocked();
                    pick = _queue.Dequeue();
                }
                else pick = _queue.Dequeue();
                _pendingScheme = null;
            }
            SchemeStore.ApplyScheme(pick);
            lock (_gate)
            {
                CurrentScheme = pick;
                LastChangeAt = DateTimeOffset.Now;
                NextChangeAt = DateTimeOffset.Now + _period;
            }
            SchemeApplied?.Invoke(pick);
        }
        catch (Exception ex)
        {
            Failed?.Invoke(ex.Message);
        }
        finally
        {
            // Agenda: reprogramar el próximo disparo (siempre, incluso si
            // este falló: un error transitorio no mata la programación).
            Func<DateTime, (DateTime At, string Scheme)> sched;
            lock (_gate) sched = _scheduler;
            if (sched is not null)
            {
                try
                {
                    var (at, scheme) = sched(DateTime.Now);
                    System.Threading.Timer t;
                    lock (_gate)
                    {
                        _pendingScheme = scheme;
                        t = _timer;
                    }
                    t?.Change(DueFromNow(at), System.Threading.Timeout.InfiniteTimeSpan);
                    lock (_gate)
                    {
                        if (IsRunning) NextChangeAt = new DateTimeOffset(at);
                    }
                }
                catch { /* si paró entre medias, nada que reprogramar */ }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
