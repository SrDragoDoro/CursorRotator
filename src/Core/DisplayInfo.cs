using System.Runtime.InteropServices;

namespace CursorRotator.Core;

/// <summary>
/// Tasa de refresco real por monitor (user32 EnumDisplaySettings).
/// Con caché por pantalla para no llamar a la API en cada frame.
/// </summary>
public static class DisplayInfo
{
    private const int ENUM_CURRENT_SETTINGS = -1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct DEVMODE
    {
        private const int CCHDEVICENAME = 32;
        private const int CCHFORMNAME = 32;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
        public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields;
        public int dmPositionX, dmPositionY;
        public int dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
        public string dmFormName;
        public short dmLogPixels, dmBitsPerPel;
        public int dmPelsWidth, dmPelsHeight;
        public int dmDisplayFlags, dmDisplayFrequency;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(
        string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    private static readonly Dictionary<string, (int Hz, DateTime At)> _cache = new();
    private static readonly object _gate = new();

    /// <summary>Hz del monitor que contiene el punto (fallback 60).</summary>
    public static int GetRefreshRate(Point cursor)
    {
        try
        {
            string device = Screen.FromPoint(cursor).DeviceName;
            lock (_gate)
            {
                if (_cache.TryGetValue(device, out var e) &&
                    (DateTime.UtcNow - e.At).TotalSeconds < 5)
                    return e.Hz;
            }
            int hz = Query(device);
            lock (_gate) _cache[device] = (hz, DateTime.UtcNow);
            return hz;
        }
        catch { return 60; }
    }

    private static int Query(string device)
    {
        try
        {
            var dm = new DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf<DEVMODE>();
            if (EnumDisplaySettings(device, ENUM_CURRENT_SETTINGS, ref dm) &&
                dm.dmDisplayFrequency is >= 30 and <= 480)
                return dm.dmDisplayFrequency;
        }
        catch { }
        return 60;
    }

    /// <summary>Solo tests: limpia la caché.</summary>
    public static void ClearCache()
    {
        lock (_gate) _cache.Clear();
    }
}
