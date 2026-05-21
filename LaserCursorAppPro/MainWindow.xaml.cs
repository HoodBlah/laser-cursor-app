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
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool   UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]                      private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")]                    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MSLLHOOKSTRUCT
    {
        public POINT pt; public uint mouseData, flags, time; public IntPtr dwExtraInfo;
    }

    // ── Mouse hook ──────────────────────────────────────────────────────────────
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    private const int WH_MOUSE_LL    = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private IntPtr              _mouseHook;
    private LowLevelMouseProc?  _mouseProc;
    private DateTime            _lastLeftClick      = DateTime.MinValue;
    private int                 _doubleClickDetectMs = 350;

    // ── Keyboard hook ────────────────────────────────────────────────────────────
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN     = 0x0100;
    private const int WM_KEYUP       = 0x0101;
    private const int WM_SYSKEYDOWN  = 0x0104;
    private const int WM_SYSKEYUP    = 0x0105;
    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }
    private IntPtr             _keyboardHook;
    private LowLevelMouseProc? _keyProc;
    private DateTime           _lastDrawKeyTap  = DateTime.MinValue;
    private bool               _drawKeyDown     = false;
    private bool               _drawModeEnabled = true;
    private int                _drawModeVKey    = 0xA2;  // VK_LCONTROL
    private int                _drawModeClearMs = 400;

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
        Loaded  += OnLoaded;
        Closing += (_, _) => { UninstallMouseHook(); UninstallKeyboardHook(); };
        SystemEvents.DisplaySettingsChanged += (_, _) => FitToVirtualDesktop();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ConfigureAsClickThroughOverlay();
        FitToVirtualDesktop();
        CompositionTarget.Rendering += OnRendering;
        InstallMouseHook();
        InstallKeyboardHook();
    }

    public void ApplySettings(LaserSettings s)
    {
        _doubleClickDetectMs = s.DoubleClickDetectMs;
        _drawModeEnabled     = s.DrawModeEnabled;
        _drawModeVKey        = s.DrawModeVKey;
        _drawModeClearMs     = s.DrawModeClearDoubleTapMs;
        OverlayControl.ApplySettings(s);
    }

    public void ClearDrawnLines() => OverlayControl.ClearDrawnStrokes();

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
    // ── Mouse hook ─────────────────────────────────────────────────────────────

    private void InstallMouseHook()
    {
        _mouseProc = MouseHookCallback;
        using var proc = System.Diagnostics.Process.GetCurrentProcess();
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc,
                                      GetModuleHandle(proc.MainModule?.ModuleName), 0);
    }

    private void UninstallMouseHook()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
    }

    private void InstallKeyboardHook()
    {
        _keyProc = KeyboardHookCallback;
        using var proc = System.Diagnostics.Process.GetCurrentProcess();
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyProc,
                                         GetModuleHandle(proc.MainModule?.ModuleName), 0);
    }

    private void UninstallKeyboardHook()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _drawModeEnabled)
        {
            var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (kb.vkCode == (uint)_drawModeVKey)
            {
                bool isDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
                bool isUp   = wParam == (IntPtr)WM_KEYUP   || wParam == (IntPtr)WM_SYSKEYUP;

                if (isDown && !_drawKeyDown)
                {
                    _drawKeyDown = true;
                    var now = DateTime.UtcNow;
                    if ((now - _lastDrawKeyTap).TotalMilliseconds <= _drawModeClearMs)
                    {
                        _lastDrawKeyTap = DateTime.MinValue; // reset to prevent triple-tap
                        Dispatcher.BeginInvoke(() => OverlayControl.ClearDrawnStrokes());
                    }
                    else
                    {
                        _lastDrawKeyTap = now;
                        Dispatcher.BeginInvoke(() => OverlayControl.SetDrawMode(true));
                    }
                }
                else if (isUp && _drawKeyDown)
                {
                    _drawKeyDown = false;
                    Dispatcher.BeginInvoke(() => OverlayControl.SetDrawMode(false));
                }
            }
        }
        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int btn = -1;
            if      (wParam == (IntPtr)WM_LBUTTONDOWN) btn = 0;
            else if (wParam == (IntPtr)WM_RBUTTONDOWN) btn = 1;
            else if (wParam == (IntPtr)WM_MBUTTONDOWN) btn = 2;

            if (btn >= 0)
            {
                var s      = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var screen = new WpfPoint(s.pt.X, s.pt.Y);
                int effectBtn = btn;
                if (btn == 0)
                {
                    var now = DateTime.UtcNow;
                    if ((now - _lastLeftClick).TotalMilliseconds <= _doubleClickDetectMs)
                        effectBtn = 3; // double-click
                    _lastLeftClick = now;
                }
                int captured = effectBtn;
                Dispatcher.BeginInvoke(() =>
                {
                    var local = PointFromScreen(screen);
                    OverlayControl.AddClickEffect(local, captured);
                    if (captured == 0 || captured == 3)
                        OverlayControl.TriggerClickSwap();
                });
            }
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
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