using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace LaserCursorApp.Helpers;

public static class ColorHelper
{
    public static MediaColor ParseColor(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 8)
                return MediaColor.FromArgb(
                    Convert.ToByte(hex[0..2], 16),
                    Convert.ToByte(hex[2..4], 16),
                    Convert.ToByte(hex[4..6], 16),
                    Convert.ToByte(hex[6..8], 16));

            if (hex.Length == 6)
                return MediaColor.FromRgb(
                    Convert.ToByte(hex[0..2], 16),
                    Convert.ToByte(hex[2..4], 16),
                    Convert.ToByte(hex[4..6], 16));
        }
        catch { }
        return Colors.Red;
    }

    public static string ToHex(MediaColor c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    public static MediaColor Lerp(MediaColor a, MediaColor b, double t) =>
        MediaColor.FromArgb(
            (byte)(a.A + (b.A - a.A) * t),
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
}
