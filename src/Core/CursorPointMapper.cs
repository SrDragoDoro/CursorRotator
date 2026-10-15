namespace CursorRotator.Core;

/// <summary>
/// Matemática pura de mapeo cursor→overlay (Pixel-Perfect Tracking):
/// con PerMonitorV2 todas las fuentes entregan píxeles físicos y el motor
/// trabaja en coordenadas de pantalla; el dibujado resta el origen del
/// escritorio virtual. Sin dependencias HWND: testeable headless.
/// </summary>
public static class CursorPointMapper
{
    /// <summary>
    /// Convierte coordenadas absolutas Raw Input (0-65535) a pantalla.
    /// Con VIRTUAL_DESKTOP mapea contra TODO el escritorio virtual
    /// (nunca contra la primaria): el bug clásico en ultrawide.
    /// </summary>
    public static Point MapAbsoluteToScreen(int rawX, int rawY,
        bool virtualDesktop, Rectangle virtualScreen, Rectangle primary)
    {
        int width = virtualDesktop ? virtualScreen.Width : primary.Width;
        int height = virtualDesktop ? virtualScreen.Height : primary.Height;
        int left = virtualDesktop ? virtualScreen.Left : primary.Left;
        int top = virtualDesktop ? virtualScreen.Top : primary.Top;
        int x = (int)((rawX / 65535.0f) * width) + left;
        int y = (int)((rawY / 65535.0f) * height) + top;
        return new Point(x, y);
    }

    /// <summary>
    /// De coordenadas de pantalla a locales del overlay: restar el offset
    /// del VirtualScreen (soporta orígenes negativos).
    /// </summary>
    public static Point ToLocal(Point screen, Rectangle virtualScreen) =>
        new(screen.X - virtualScreen.Left, screen.Y - virtualScreen.Top);

    /// <summary>
    /// Offset de dibujado en píxeles de bitmap para una ventana layered en
    /// (winX, winY) con escala DPI: equivale a ToLocal escalado.
    /// </summary>
    public static PointF ToLayerOffset(int winX, int winY, float scale) =>
        new(-winX * scale, -winY * scale);
}
