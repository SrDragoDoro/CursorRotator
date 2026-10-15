using CursorRotator.Core;
using Xunit;

namespace CursorRotator.Tests;

/// <summary>
/// Cola de rotación: vista previa == botón, sin repetir hasta agotar el pool,
/// y validación del pool contra el registro.
/// </summary>
public class CursorRotatorTests
{
    [Fact]
    public void Start_PoolSinEsquemasInstalados_Lanza()
    {
        using var r = new CursorRotator.Core.CursorRotator();
        Assert.Throws<InvalidOperationException>(() =>
            r.Start(new[] { "zzNoExisteA_", "zzNoExisteB_" }, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void PeekNext_CoincideConTakeNext_Y_NoRepiteHastaAgotar()
    {
        using var t = new TempDir();
        string store = t.Combine("store");
        Directory.CreateDirectory(store);
        var (names, _) = TestRegistry.RegisterSchemes(3, store);
        try
        {
            using var r = new CursorRotator.Core.CursorRotator();
            // dueTime de 1 h: el timer no dispara, la cola queda determinista.
            r.Start(names, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
            Assert.True(r.IsRunning);
            Assert.NotNull(r.NextChangeAt);

            string peek = r.PeekNext();
            string take = r.TakeNext();
            Assert.Equal(peek, take);

            var cycle = new List<string> { take };
            for (int i = 0; i < names.Count - 1; i++)
                cycle.Add(r.TakeNext());

            Assert.Equal(names.Count, cycle.Distinct().Count());      // sin repetir
            Assert.Equal(names.OrderBy(x => x), cycle.OrderBy(x => x)); // recorre todo

            r.Stop();
            Assert.False(r.IsRunning);
            Assert.Null(r.NextChangeAt);
        }
        finally { TestRegistry.Unregister(names, store); }
    }

    [Fact]
    public void TakeNext_SinPool_DevuelveNull()
    {
        using var r = new CursorRotator.Core.CursorRotator();
        Assert.Null(r.TakeNext());
        Assert.Null(r.PeekNext());
    }
}
