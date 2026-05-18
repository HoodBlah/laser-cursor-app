using System;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LaserCursorApp;

public partial class MainWindow : Window
{
    private bool isLaserEnabled = true;
    private bool isButterModeEnabled;

    private const int GwlExstyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExLayered = 0x80000;
    private const int WsExToolwindow = 0x80;
    private const int WsExNoactivate = 0x08000000;

    private const uint SwpNosize = 0x0001;
    private const uint SwpNomove = 0x0002;
    private const uint SwpNoactivate = 0x0010;
    private const uint SwpShowwindow = 0x0040;

    private static readonly IntPtr HwndTopmost = new(-1);

    public MainWindow()
    {
        InitializeComponent();
        OverlayControl.ButterModeEnabled = isButterModeEnabled;

        Loaded += (_, _) => FitToVirtualDesktop();
        SourceInitialized += (_, _) => ConfigureAsClickThroughOverlay();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        CompositionTarget.Rendering += OnRendering;
    }

    public bool IsLaserEnabled => isLaserEnabled;
    public bool IsButterModeEnabled => isButterModeEnabled;

    public void ToggleEnabled()
    {
        isLaserEnabled = !isLaserEnabled;
        if (!isLaserEnabled)
        {
            OverlayControl.Clear();
        }
    }

    public void ToggleButterMode()
    {
        isButterModeEnabled = !isButterModeEnabled;
        OverlayControl.ButterModeEnabled = isButterModeEnabled;
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        CompositionTarget.Rendering -= OnRendering;
        base.OnClosed(e);
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!isLaserEnabled)
        {
            OverlayControl.UpdateCursor(null);
            return;
        }

        if (!GetCursorPos(out var cursorPos))
        {
            return;
        }

        var windowPoint = PointFromScreen(new System.Windows.Point(cursorPos.X, cursorPos.Y));
        OverlayControl.UpdateCursor(windowPoint);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(FitToVirtualDesktop);
    }

    private void FitToVirtualDesktop()
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    private void ConfigureAsClickThroughOverlay()
    {
        if (PresentationSource.FromVisual(this) is not HwndSource source)
        {
            return;
        }

        var hwnd = source.Handle;
        var exStyle = GetWindowLong(hwnd, GwlExstyle);
        exStyle |= WsExTransparent | WsExLayered | WsExToolwindow | WsExNoactivate;
        SetWindowLong(hwnd, GwlExstyle, exStyle);

        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpNoactivate | SwpShowwindow);
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out WinPoint lpPoint);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct WinPoint
    {
        public int X;
        public int Y;
    }
}