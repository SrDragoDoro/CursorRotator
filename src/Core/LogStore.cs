using System.Collections.Concurrent;

namespace CursorRotator.Core;

/// <summary>
/// Bitácora compartida (ring buffer): alimenta el log del dashboard y el
/// modal "Ver Registro" sin acoplarlos.
/// </summary>
public static class LogStore
{
    private const int Capacity = 500;
    private static readonly ConcurrentQueue<(DateTime At, string Msg)> _q = new();

    public static event Action Appended;

    public static void Append(string message)
    {
        _q.Enqueue((DateTime.Now, message));
        while (_q.Count > Capacity && _q.TryDequeue(out _)) { }
        try { Appended?.Invoke(); } catch { }
    }

    public static List<string> Snapshot() =>
        _q.Select(e => $"[{e.At:HH:mm:ss}] {e.Msg}").ToList();

    public static int Count => _q.Count;
}
