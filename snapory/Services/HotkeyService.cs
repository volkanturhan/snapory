using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace snapory.Services;

/// <summary>
/// Registers a system-wide hotkey (Ctrl + Shift + S) and raises
/// <see cref="Pressed"/> whenever it is used, from any application.
///
/// The hotkey is registered against the thread (not a window) and caught via
/// <see cref="ComponentDispatcher"/>, so it keeps working no matter what happens
/// to the app's windows — even after they are all closed/hidden to the tray.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    // Arbitrary id used to identify our single registration in WM_HOTKEY.
    private const int HotkeyId = 1;

    // Virtual-key code for the "S" key.
    private const uint VirtualKeyS = 0x53;

    [Flags]
    private enum Modifiers : uint
    {
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        // Stops the hotkey from auto-repeating while the keys are held down.
        NoRepeat = 0x4000,
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    private bool _disposed;

    /// <summary>Raised when the registered hotkey is pressed.</summary>
    public event Action? Pressed;

    public HotkeyService()
    {
        var modifiers = (uint)(Modifiers.Control | Modifiers.Shift | Modifiers.NoRepeat);

        // hwnd = IntPtr.Zero registers the hotkey against this thread, so the
        // WM_HOTKEY is posted to the thread's queue and surfaces through the
        // WPF dispatcher regardless of which (if any) window is open.
        if (!RegisterHotKey(IntPtr.Zero, HotkeyId, modifiers, VirtualKeyS))
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                "Could not register the Ctrl+Shift+S hotkey; another app may already own it.");

        ComponentDispatcher.ThreadPreprocessMessage += OnThreadMessage;
    }

    private void OnThreadMessage(ref MSG msg, ref bool handled)
    {
        if (msg.message == WM_HOTKEY && (int)msg.wParam == HotkeyId)
        {
            Pressed?.Invoke();
            handled = true;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        ComponentDispatcher.ThreadPreprocessMessage -= OnThreadMessage;
        UnregisterHotKey(IntPtr.Zero, HotkeyId);
    }
}
