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
                var mouse = Marshal.PtrToStructure<RAWMOUSE>(mousePtr);
                return mouse.LastX != 0 || mouse.LastY != 0;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        catch { return false; }
    }
}
