using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Treemap.App.Services;

/// <summary>A system-wide keyboard shortcut, delivered even while a game has the focus.</summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x1;
    private const uint ModControl = 0x2;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkF10 = 0x79;
    private const int Id = 0x7472; // arbitrary, unique within this window

    private readonly Action _pressed;
    private HwndSource? _source;
    private IntPtr _handle;

    public GlobalHotkey(Action pressed)
    {
        _pressed = pressed;
    }

    /// <summary>Human-readable name of the shortcut, for the interface.</summary>
    public static string Description => "Ctrl+Alt+F10";

    /// <summary>False when another program already owns the shortcut.</summary>
    public bool IsRegistered { get; private set; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    public void Register(Window owner)
    {
        _handle = new WindowInteropHelper(owner).EnsureHandle();
        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WndProc);
        IsRegistered = RegisterHotKey(_handle, Id, ModControl | ModAlt | ModNoRepeat, VkF10);
    }

    public void Dispose()
    {
        if (IsRegistered)
        {
            _ = UnregisterHotKey(_handle, Id);
            IsRegistered = false;
        }

        _source?.RemoveHook(WndProc);
        _source = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == Id)
        {
            _pressed();
            handled = true;
        }

        return IntPtr.Zero;
    }
}
