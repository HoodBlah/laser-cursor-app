using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using LaserCursorApp.Helpers;
using MediaColor  = System.Windows.Media.Color;
using WpfPoint    = System.Windows.Point;
using WpfMouseArgs = System.Windows.Input.MouseEventArgs;
using UserControl  = System.Windows.Controls.UserControl;

namespace LaserCursorApp.Controls;

public partial class ColorPickerControl : UserControl
{
    private double _h = 0, _s = 1, _v = 1;
    private byte   _a = 255;
    private bool   _updating;

    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(nameof(SelectedColor), typeof(MediaColor), typeof(ColorPickerControl),
            new PropertyMetadata(Colors.Red, OnSelectedColorChanged));

    public MediaColor SelectedColor
    {
        get => (MediaColor)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public event EventHandler? ColorChanged;

    public ColorPickerControl()
    {
        InitializeComponent();
        Loaded += (_, _) => SyncFromColor(SelectedColor);
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPickerControl c && !c._updating)
            c.SyncFromColor((MediaColor)e.NewValue);
    }

    private void SyncFromColor(MediaColor c)
    {
        RgbToHsv(c.R, c.G, c.B, out _h, out _s, out _v);
        _a = c.A;
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (!IsLoaded) return;

        // Hue base
        HueBaseBrush.Color = HsvToColor(_h, 1, 1, 255);

        // SV cursor
        Canvas.SetLeft(SvCursor, _s * SvCanvas.Width  - 6);
        Canvas.SetTop (SvCursor, (1 - _v) * SvCanvas.Height - 6);

        // Hue cursor
        Canvas.SetLeft(HueCursor, Math.Clamp(_h / 360.0 * HueCanvas.Width - 2, 0, HueCanvas.Width - 4));

        // Alpha gradient
        var solid = HsvToColor(_h, _s, _v, 255);
        AlphaGradRect.Fill = new LinearGradientBrush(
            MediaColor.FromArgb(0, solid.R, solid.G, solid.B), solid, new WpfPoint(0, 0), new WpfPoint(1, 0));

        // Alpha cursor
        Canvas.SetLeft(AlphaCursor, Math.Clamp(_a / 255.0 * AlphaCanvas.Width - 2, 0, AlphaCanvas.Width - 4));

        // Preview swatch
        var selected = HsvToColor(_h, _s, _v, _a);
        PreviewSwatch.Fill = new SolidColorBrush(selected);

        // Hex box
        _updating = true;
        HexBox.Text = ColorHelper.ToHex(selected);
        _updating = false;

        // DP
        _updating = true;
        SelectedColor = selected;
        _updating = false;

        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── SV Canvas ────────────────────────────────────────────────────────────

    private void SvCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        SvCanvas.CaptureMouse();
        HandleSv(e.GetPosition(SvCanvas));
    }

    private void SvCanvas_MouseMove(object sender, WpfMouseArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        HandleSv(e.GetPosition(SvCanvas));
    }

    private void SvCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        => SvCanvas.ReleaseMouseCapture();

    private void HandleSv(WpfPoint p)
    {
        _s = Math.Clamp(p.X / SvCanvas.Width,  0, 1);
        _v = 1 - Math.Clamp(p.Y / SvCanvas.Height, 0, 1);
        RefreshUI();
    }

    // ── Hue Bar ───────────────────────────────────────────────────────────────

    private void HueCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        HueCanvas.CaptureMouse();
        HandleHue(e.GetPosition(HueCanvas));
    }

    private void HueCanvas_MouseMove(object sender, WpfMouseArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        HandleHue(e.GetPosition(HueCanvas));
    }

    private void HueCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        => HueCanvas.ReleaseMouseCapture();

    private void HandleHue(WpfPoint p)
    {
        _h = Math.Clamp(p.X / HueCanvas.Width * 360.0, 0, 360);
        RefreshUI();
    }

    // ── Alpha Bar ─────────────────────────────────────────────────────────────

    private void AlphaCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        AlphaCanvas.CaptureMouse();
        HandleAlpha(e.GetPosition(AlphaCanvas));
    }

    private void AlphaCanvas_MouseMove(object sender, WpfMouseArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        HandleAlpha(e.GetPosition(AlphaCanvas));
    }

    private void AlphaCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        => AlphaCanvas.ReleaseMouseCapture();

    private void HandleAlpha(WpfPoint p)
    {
        _a = (byte)Math.Clamp(p.X / AlphaCanvas.Width * 255, 0, 255);
        RefreshUI();
    }

    // ── Hex Box ───────────────────────────────────────────────────────────────

    private void HexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating) return;
        var text = HexBox.Text.TrimStart('#');
        try
        {
            if (text.Length == 8)
            {
                _a = Convert.ToByte(text[0..2], 16);
                var r = Convert.ToByte(text[2..4], 16);
                var g = Convert.ToByte(text[4..6], 16);
                var b = Convert.ToByte(text[6..8], 16);
                RgbToHsv(r, g, b, out _h, out _s, out _v);
                RefreshUI();
            }
            else if (text.Length == 6)
            {
                var r = Convert.ToByte(text[0..2], 16);
                var g = Convert.ToByte(text[2..4], 16);
                var b = Convert.ToByte(text[4..6], 16);
                RgbToHsv(r, g, b, out _h, out _s, out _v);
                RefreshUI();
            }
        }
        catch { }
    }

    // ── Color Conversion ─────────────────────────────────────────────────────

    private static MediaColor HsvToColor(double h, double s, double v, byte a)
    {
        double r, g, b;
        if (s == 0) { r = g = b = v; }
        else
        {
            h = (h % 360) / 60;
            var i = (int)h;
            var f = h - i;
            var p = v * (1 - s);
            var q = v * (1 - s * f);
            var t = v * (1 - s * (1 - f));
            (r, g, b) = i switch
            {
                0 => (v, t, p), 1 => (q, v, p), 2 => (p, v, t),
                3 => (p, q, v), 4 => (t, p, v), _ => (v, p, q)
            };
        }
        return MediaColor.FromArgb(a, (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
    {
        var rf = r / 255.0; var gf = g / 255.0; var bf = b / 255.0;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var d   = max - min;
        v = max;
        s = max == 0 ? 0 : d / max;
        if (d == 0) { h = 0; return; }
        if      (max == rf) h = 60 * (((gf - bf) / d) % 6);
        else if (max == gf) h = 60 * ((bf - rf) / d + 2);
        else                h = 60 * ((rf - gf) / d + 4);
        if (h < 0) h += 360;
    }
}
