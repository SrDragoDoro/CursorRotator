namespace CursorRotator.Core;

/// <summary>
/// Caché LRU de vistas previas decodificadas (Mousecape guarda los assets
/// una vez en vez de releerlos). Evita releer y reparsear .cur/.ani del
/// disco en cada refresco de la UI. Hilosegura; devuelve CLONES (el
/// llamador dispone su copia, la caché conserva la maestra, tope 32).
/// </summary>
public static class CursorPreviewCache
{
    private const int Cap = 32;
    private static readonly object _gate = new();
    private static readonly Dictionary<string, (Bitmap Master, LinkedListNode<string> Node)> _map
        = new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<string> _lru = new();

    /// <summary>Bitmap del primer frame o null. El llamador debe disponerlo.</summary>
    public static Bitmap Get(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        string key;
        try { key = SchemeStore.ResolveCursorPath(path); }
        catch { return null; }
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var hit))
            {
                _lru.Remove(hit.Node);
                _lru.AddFirst(hit.Node);
                return (Bitmap)hit.Master.Clone();
            }
        }
        Bitmap fresh = CursorImage.LoadFirstFrame(key);
        if (fresh is null) return null;
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var raced))
            {
                fresh.Dispose();
                _lru.Remove(raced.Node);
                _lru.AddFirst(raced.Node);
                return (Bitmap)raced.Master.Clone();
            }
            while (_map.Count >= Cap && _lru.Last is not null)
            {
                string old = _lru.Last.Value;
                _lru.RemoveLast();
                if (_map.TryGetValue(old, out var oe))
                {
                    _map.Remove(old);
                    try { oe.Master.Dispose(); } catch { }
                }
            }
            var node = new LinkedListNode<string>(key);
            _lru.AddFirst(node);
            _map[key] = (fresh, node);
            return (Bitmap)fresh.Clone();
        }
    }

    public static void Clear()
    {
        lock (_gate)
        {
            foreach (var e in _map.Values)
                try { e.Master.Dispose(); } catch { }
            _map.Clear();
            _lru.Clear();
        }
    }
}
