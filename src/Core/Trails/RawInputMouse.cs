using System.Runtime.InteropServices;

namespace CursorRotator.Core.Trails;

/// <summary>
/// Raw Input de ratón (HID sin latencia): registra el dispositivo para recibir
/// WM_INPUT incluso sin foco (INPUTSINK, sin NOLEGACY para no romper el ratón).
/// Solo detecta movimiento (deltas); la posición absoluta se lee con GetCursorPos.
/// </summary>
public static class RawInputMouse
{
    public const int WM_INPUT = 0x00FF;
    private const ushort MOUSE_MOVE_ABSOLUTE = 0x0001;
    private const ushort MOUSE_VIRTUAL_DESKTOP = 0x0002;
    private const uint RID_INPUT = 0x10000003;
    private const uint RIDEV_INPUTSINK = 0x00000100;
    private const uint RIDEV_REMOVE = 0x00000001;
    private const int RIM_TYPEMOUSE = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort UsagePage, Usage;
        public uint Flags;
        public IntPtr HwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public uint Type, Size;
        public IntPtr Device, WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWMOUSE
    {
        public ushort Flags;
        public ushort Pad;
        public uint Buttons;
        public ushort ButtonFlags, ButtonData;
        public uint RawButtons;
        public int LastX, LastY;
        public uint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(
        [In] RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr hRawInput, uint uiCommand, IntPtr pData,
        ref uint pcbSize, uint cbSizeHeader);

    /// <summary>Registra (true) o elimina (false) la escucha en la ventana dada.</summary>
    public static bool SetEnabled(IntPtr hwnd, bool enable)
    {
        var dev = new RAWINPUTDEVICE
        {
            UsagePage = 1, // controles genéricos
            Usage = 2,     // ratón
            Flags = enable ? RIDEV_INPUTSINK : RIDEV_REMOVE,
            HwndTarget = hwnd,
        };
        return RegisterRawInputDevices([dev], 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
    }

    /// <summary>Procesa WM_INPUT; true si el ratón se movió (deltas ≠ 0).</summary>
    public static bool HandleMessage(IntPtr lParam)
    {
        try
        {
            if (!TryParseMouse(lParam, out var mouse)) return false;
            return mouse.LastX != 0 || mouse.LastY != 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// Posición absoluta de digitalizadores/tabletas (0-65535) mapeada al
    /// escritorio que corresponda. False si el reporte es relativo.
    /// </summary>
    public static bool TryGetAbsolutePosition(IntPtr lParam, out int x, out int y)
    {
        x = 0; y = 0;
        try
        {
            if (!TryParseMouse(lParam, out var mouse)) return false;
            var vs = SystemInformation.VirtualScreen;
            var primary = Screen.PrimaryScreen?.Bounds ?? vs;
            if (!MapRawMouse(mouse.Flags, mouse.LastX, mouse.LastY,
                vs, primary, out var pt))
                return false;
            x = pt.X; y = pt.Y;
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Núcleo puro del mapeo (sin P/Invoke): testeable headless.
    /// </summary>
    public static bool MapRawMouse(ushort flags, int lastX, int lastY,
        Rectangle virtualScreen, Rectangle primary, out Point pt)
    {
        pt = default;
        if ((flags & MOUSE_MOVE_ABSOLUTE) == 0) return false;
        bool vdesk = (flags & MOUSE_VIRTUAL_DESKTOP) != 0;
        pt = CursorPointMapper.MapAbsoluteToScreen(
            lastX, lastY, vdesk, virtualScreen, primary);
        return true;
    }

    private static bool TryParseMouse(IntPtr lParam, out RAWMOUSE mouse)
    {
        mouse = default;
        uint size = 0;
        uint headerSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
        GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, headerSize);
        if (size == 0) return false;
        IntPtr buf = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(lParam, RID_INPUT, buf, ref size, headerSize) != size)
                return false;
            var header = Marshal.PtrToStructure<RAWINPUTHEADER>(buf);
            if (header.Type != RIM_TYPEMOUSE) return false;
            IntPtr mousePtr = IntPtr.Add(buf, Marshal.SizeOf<RAWINPUTHEADER>());
            mouse = Marshal.PtrToStructure<RAWMOUSE>(mousePtr);
            return true;
        }
        finally { Marshal.FreeHGlobal(buf); }
    }
}
