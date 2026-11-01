using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Treemap.Core.Settings;
using Treemap.Core.Monitoring;

namespace Treemap.App;

/// <summary>
/// The in-game overlay: a small always-on-top panel that never takes focus and lets every click through to the
/// game underneath. It works over windowed and borderless-windowed games; exclusive full screen hides it.
/// </summary>
public partial class OverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const double ScreenMargin = 16;

    private OverlayCorner _corner = OverlayCorner.TopLeft;

    public OverlayWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Reposition();
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr window, int index, int value);

    public void SetCorner(OverlayCorner corner)
    {
        _corner = corner;
        Reposition();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Click-through (transparent to the mouse), never activated, and kept out of Alt+Tab.
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(handle, GwlExStyle);
        _ = SetWindowLong(handle, GwlExStyle, style | WsExTransparent | WsExNoActivate | WsExToolWindow);
        Reposition();
    }

    private void Reposition()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var area = SystemParameters.WorkArea;
        (Left, Top) = OverlayPlacement.Compute(_corner, area.Left, area.Top, area.Width, area.Height, ActualWidth, ActualHeight, ScreenMargin);
    }
}
