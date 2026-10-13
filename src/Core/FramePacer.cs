namespace CursorRotator.Core;

/// <summary>
/// Frame pacing dinámico: el render espera lo justo según el monitor bajo el
/// cursor (o un tope fijo). La física ya es independiente de FPS (DeltaTime).
/// </summary>
public static class FramePacer
{
    /// <param name="capHz">0 = auto-detect, -1 = uncapped, otro = tope fijo.</param>
    /// <returns>Milisegundos a esperar por frame (frame budget).</returns>
    public static int GetFrameBudgetMs(Point cursor, int capHz)
    {
        if (capHz == -1) return 4; // uncapped: piso anti-spin, ~250 FPS máx
        int rate = capHz > 0 ? capHz : DisplayInfo.GetRefreshRate(cursor);
        rate = Math.Clamp(rate, 30, 240);
        return Math.Max(1, 1000 / rate);
    }
}
