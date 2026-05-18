using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LaserCursorAppPro.Models;
using Microsoft.Win32;
using WpfPoint = System.Windows.Point;

namespace LaserCursorAppPro;

public partial class MainWindow : Window
{
    // ── Win32 ─────────────────────────────────────────────────────────────────
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] private static extern int  SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] private static extern int  GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const int    GWL_EXSTYLE        = -20;
    private const int    WS_EX_TRANSPARENT  = 0x00000020;
    private const int    WS_EX_LAYERED      = 0x00080000;
    private const int    WS_EX_TOOLWINDOW   = 0x00000080;
    private const int    WS_EX_NOACTIVATE   = 0x08000000;
    private const uint   SWP_NOMOVE         = 0x0002;
    private const uint   SWP_NOSIZE         = 0x0001;
    private const uint   SWP_NOACTIVATE     = 0x0010;

    // ── State ─────────────────────────────────────────────────────────────────
    private bool _isEnabled = true;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SystemEvents.DisplaySettingsChanged += (_, _) => FitToVirtualDesktop();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ConfigureAsClickThroughOverlay();
        FitToVirtualDesktop();
        CompositionTarget.Rendering += OnRendering;
    }

    public void ApplySettings(LaserSettings s)
    {
        OverlayControl.ApplySettings(s);
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!_isEnabled) return;

        if (GetCursorPos(out var pt))
        {
            var screen = new WpfPoint(pt.X, pt.Y);
            var local  = PointFromScreen(screen);
            OverlayControl.UpdateCursor(local);
        }
        else
        {
            OverlayControl.UpdateCursor(null);
        }
    }

    // ── Visibility toggle ─────────────────────────────────────────────────────

    public new void Show()
    {
        _isEnabled = true;
        base.Show();
    }

    public new void Hide()
    {
        _isEnabled = false;
        OverlayControl.Clear();
        base.Hide();
    }

    // ── Win32 overlay setup ───────────────────────────────────────────────────

    private void ConfigureAsClickThroughOverlay()
    {
        var hwnd    = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private void FitToVirtualDesktop()
    {
        Left   = SystemParameters.VirtualScreenLeft;
        Top    = SystemParameters.VirtualScreenTop;
        Width  = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }
}