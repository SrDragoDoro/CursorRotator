using System.Runtime.InteropServices;

namespace CursorRotator.Core;

/// <summary>API nativa de Windows necesaria para aplicar cursores al instante.</summary>
internal static class NativeMethods
{
    public const uint SPI_SETCURSORS = 0x0057;
    public const uint SPIF_UPDATEINIFILE = 0x01;
    public const uint SPIF_SENDCHANGE = 0x02;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SystemParametersInfo(
        uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    /// <summary>Recarga los cursores desde el registro sin cerrar sesión.</summary>
    public static void RefreshCursors()
    {
        SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero,
            SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
    }

    // ---- Topmost forzado (overlay sobre apps borderless) ----
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    /// <summary>Reaplica z-order superior sin mover, redimensionar ni activar.</summary>
    public static void ForceTopmost(IntPtr hwnd)
    {
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    // Barra de título oscura (DWMWA_USE_IMMERSIVE_DARK_MODE = 20). Best-effort.
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void TryUseDarkTitleBar(IntPtr handle, bool dark)
    {
        try
        {
            int v = dark ? 1 : 0;
            DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
        }
        catch { /* Windows < 10 1809: se ignora */ }
    }
}
