using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CursorRotator.Core;

/// <summary>
/// Hook de ratón de bajo nivel (WH_MOUSE_LL): captura cada movimiento a
/// resolución de milisegundos, sin perder puntos en giros rápidos.
/// El callback solo encola (rápido); el render consume en su propio hilo.
/// Debe instalarse en un hilo con message pump (el hilo UI).
/// </summary>
public sealed class MouseHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT Pt;
        public uint MouseData, Flags, Time;
        public IntPtr ExtraInfo;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private HookProc _proc;
    private IntPtr _hook = IntPtr.Zero;
    private bool _disposed;

    /// <summary>Se invoca en el hilo que instaló el hook (hilo UI).</summary>
    public event Action<int, int> MouseMoved;
    /// <summary>Clic izquierdo (para la supernova global).</summary>
    public event Action<int, int> MouseDown;

    public bool IsInstalled => _hook != IntPtr.Zero;

    public void Install()
    {
        if (IsInstalled || _disposed) return;
        _proc = Callback; // mantener viva la referencia
        using var process = Process.GetCurrentProcess();
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc,
            GetModuleHandle(process.MainModule?.ModuleName), 0);
        if (!IsInstalled)
            throw new InvalidOperationException(
                "No se pudo instalar el hook de ratón (Win32 error " +
                Marshal.GetLastWin32Error() + "). Se usará sondeo clásico.");
    }

    public void Uninstall()
    {
        if (!IsInstalled) return;
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == WM_MOUSEMOVE || msg == WM_LBUTTONDOWN)
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                try
                {
                    if (msg == WM_MOUSEMOVE) MouseMoved?.Invoke(data.Pt.X, data.Pt.Y);
                    else MouseDown?.Invoke(data.Pt.X, data.Pt.Y);
                }
                catch { /* el hook nunca debe lanzar */ }
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { Uninstall(); } catch { }
        _proc = null;
    }
}
