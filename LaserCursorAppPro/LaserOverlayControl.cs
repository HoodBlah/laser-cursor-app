using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LaserCursorAppPro.Helpers;
using LaserCursorAppPro.Models;
using WpfPoint = System.Windows.Point;
using MediaColor = System.Windows.Media.Color;

namespace LaserCursorAppPro;

public class LaserOverlayControl : FrameworkElement
{
    private readonly List<TrailPoint>  points = new();
    private TimeSpan                   trailLifetime = TimeSpan.FromMilliseconds(380);
    private readonly TimeSpan          gapLifetime   = TimeSpan.FromMilliseconds(90);

    private WpfPoint?    latestCursor;
    private WpfPoint?    filteredCursor;
    private DateTime     lastSampleTime = DateTime.MinValue;
    private DateTime     lastInputTime  = DateTime.MinValue;

    private LaserSettings settings = new();
    // ── Custom dot image ────────────────────────────────────────────────────────────
    private readonly List<BitmapSource> _dotFrames    = new();
    private int              _dotFrameIdx  = 0;
    private DispatcherTimer? _gifTimer;
    private string           _loadedDotPath = "";    // ── Trail image ───────────────────────────────────────────────────────────────
    private BitmapSource? _trailImage;
    private string        _loadedTrailPath = "";
    // ── Movement direction for dot rotation ───────────────────────────────
    private Vector _moveDir = new Vector(1, 0);
    // ── Wave animation ────────────────────────────────────────────────────
    private double   _wavePhase    = 0.0;
    private DateTime _lastWaveTime = DateTime.MinValue;    // ── Public API ────────────────────────────────────────────────────────────

    public void ApplySettings(LaserSettings s)
    {
        settings      = s;
        trailLifetime = TimeSpan.FromMilliseconds(s.TailLengthMs);

        if (s.DotUseCustomImage && s.DotImagePath != _loadedDotPath)
            LoadDotImage(s.DotImagePath);
        else if (!s.DotUseCustomImage)
            UnloadDotImage();

        if (s.TailImageMode && s.TailImagePath != _loadedTrailPath)
            LoadTrailImage(s.TailImagePath);
        else if (!s.TailImageMode)
            UnloadTrailImage();

        InvalidateVisual();
    }

    public void UpdateCursor(WpfPoint? position)
    {
        var now = DateTime.UtcNow;

        if (position is null)
        {
            latestCursor   = null;
            filteredCursor = null;
            TrimPoints(now);
            InvalidateVisual();
            return;
        }

        latestCursor = ApplyAdaptiveSmoothing(position.Value, now);

        var butter          = settings.ButterModeEnabled;
        var minDist         = butter ? 0.35 : 0.65;
        var sampleIntervalMs = butter ? 4 : 10;

        if (points.Count == 0 || Dist(points[^1].Position, latestCursor.Value) > minDist)
        {
            points.Add(new TrailPoint(latestCursor.Value, now));
            lastSampleTime = now;
        }
        else if (now - lastSampleTime > TimeSpan.FromMilliseconds(sampleIntervalMs))
        {
            points.Add(new TrailPoint(latestCursor.Value, now));
            lastSampleTime = now;
        }

        TrimPoints(now);

        // Smooth movement direction for dot rotation
        if (points.Count >= 3)
        {
            int back = Math.Min(4, points.Count - 1);
            var recent = points[^1].Position - points[^(back + 1)].Position;
            if (recent.Length > 0.5)
            {
                var rLen  = recent.Length;
                var rNorm = new Vector(recent.X / rLen, recent.Y / rLen);
                _moveDir  = new Vector(
                    _moveDir.X + (rNorm.X - _moveDir.X) * 0.25,
                    _moveDir.Y + (rNorm.Y - _moveDir.Y) * 0.25);
                var mLen = _moveDir.Length;
                if (mLen > 0.001) _moveDir = new Vector(_moveDir.X / mLen, _moveDir.Y / mLen);
            }
        }

        InvalidateVisual();
    }

    public void Clear()
    {
        latestCursor   = null;
        filteredCursor = null;
        points.Clear();
        InvalidateVisual();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (settings.WaveEnabled)
        {
            var wNow = DateTime.UtcNow;
            if (_lastWaveTime != DateTime.MinValue)
                _wavePhase += (wNow - _lastWaveTime).TotalSeconds * 4.0; // 4 rad/sec travel speed
            _lastWaveTime = wNow;
        }
        else
        {
            _wavePhase = 0.0;
            _lastWaveTime = DateTime.MinValue;
        }

        if (points.Count > 1)
        {
            var smoothed = BuildSmoothedPoints(DateTime.UtcNow);
            if (smoothed.Count >= 2)
            {
                if (settings.TailGlowEnabled)
                    DrawTrailGlow(dc, smoothed);
                DrawTrail(dc, smoothed);
            }
        }

        if (latestCursor is not null)
            DrawHead(dc, latestCursor.Value);
    }

    private void DrawTrail(DrawingContext dc, List<SmoothedPoint> sp)
    {
        var maxW        = settings.TailThickness;
        var minW        = 0.35;
        var power       = settings.TailTaperPower;
        var tailColor   = ColorHelper.ParseColor(settings.TailColor);
        var endColor    = settings.TailGradientEnabled
                          ? (MediaColor?)ColorHelper.ParseColor(settings.TailGradientEndColor)
                          : null;
        var count       = sp.Count;

        for (var i = 1; i < count; i++)
        {
            var a = sp[i - 1];
            var b = sp[i];

            if (b.Timestamp - a.Timestamp > gapLifetime) continue;

            var prog = i / (double)(count - 1);
            var combinedFade = Math.Clamp(Math.Pow(prog, power) * (1.0 - b.AgeRatio), 0.0, 1.0);
            if (combinedFade <= 0.01) continue;

            var wA = minW + (maxW - minW) * Math.Clamp(Math.Pow((i - 1) / (double)(count - 1), power) * (1.0 - a.AgeRatio), 0, 1);
            var wB = minW + (maxW - minW) * Math.Clamp(Math.Pow(prog, power) * (1.0 - b.AgeRatio), 0, 1);

            if (Dist(a.Position, b.Position) < 0.01) continue;

            if (settings.TailImageMode && _trailImage != null)
            {
                // Draw the quad in a rotated frame aligned to the trail direction so that
                // RelativeToBoundingBox always sees an axis-aligned rectangle. This prevents
                // the barcode/pixelated look that occurs when segments run vertically.
                var dir      = b.Position - a.Position;
                var segAngle = Math.Atan2(dir.Y, dir.X) * 180.0 / Math.PI;
                var midPt    = new WpfPoint((a.Position.X + b.Position.X) * 0.5,
                                            (a.Position.Y + b.Position.Y) * 0.5);
                var halfLen  = dir.Length * 0.5;

                var imgW   = _trailImage.PixelWidth;
                var imgH   = _trailImage.PixelHeight;
                var colX   = (1.0 - prog) * imgW;
                var sliceW = Math.Max(1.0, (double)imgW / count);
                colX       = Math.Clamp(colX, 0, imgW - sliceW);

                var ib = new ImageBrush(_trailImage)
                {
                    ViewboxUnits  = BrushMappingMode.Absolute,
                    Viewbox       = new Rect(colX, 0, sliceW, imgH),
                    ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                    Viewport      = new Rect(0, 0, 1, 1),
                    Stretch       = Stretch.Fill,
                    TileMode      = TileMode.None,
                    Opacity       = combinedFade,
                };
                ib.Freeze();

                // Horizontal quad in rotated frame: tail-end (a) on left, head-end (b) on right
                var hQuad = new StreamGeometry();
                using (var ctx = hQuad.Open())
                {
                    ctx.BeginFigure(new WpfPoint(midPt.X - halfLen, midPt.Y - wA * 0.5), true, true);
                    ctx.LineTo(   new WpfPoint(midPt.X - halfLen, midPt.Y + wA * 0.5), true, false);
                    ctx.LineTo(   new WpfPoint(midPt.X + halfLen, midPt.Y + wB * 0.5), true, false);
                    ctx.LineTo(   new WpfPoint(midPt.X + halfLen, midPt.Y - wB * 0.5), true, false);
                }
                hQuad.Freeze();

                dc.PushTransform(new RotateTransform(segAngle, midPt.X, midPt.Y));
                dc.DrawGeometry(ib, null, hQuad);
                dc.Pop();
            }
            else
            {
                var n  = UnitNormal(a.Position, b.Position);
                var p1 = a.Position + n * (wA * 0.5);
                var p2 = a.Position - n * (wA * 0.5);
                var p3 = b.Position - n * (wB * 0.5);
                var p4 = b.Position + n * (wB * 0.5);

                var quad = new StreamGeometry();
                using (var ctx = quad.Open())
                {
                    ctx.BeginFigure(p1, true, true);
                    ctx.LineTo(p2, true, false);
                    ctx.LineTo(p3, true, false);
                    ctx.LineTo(p4, true, false);
                }
                quad.Freeze();

                MediaColor segColor;
                if (settings.TailRainbowMode)
                {
                    var rc   = SampleRainbow(prog);
                    segColor = MediaColor.FromArgb((byte)(255 * combinedFade), rc.R, rc.G, rc.B);
                }
                else if (endColor is not null)
                {
                    var lerped = ColorHelper.Lerp(tailColor, endColor.Value, 1.0 - prog);
                    segColor   = MediaColor.FromArgb((byte)(lerped.A * combinedFade), lerped.R, lerped.G, lerped.B);
                }
                else
                {
                    segColor = MediaColor.FromArgb((byte)(tailColor.A * combinedFade), tailColor.R, tailColor.G, tailColor.B);
                }
                var sb = new SolidColorBrush(segColor);
                sb.Freeze();
                dc.DrawGeometry(sb, null, quad);
            }
        }
    }

    private void DrawTrailGlow(DrawingContext dc, List<SmoothedPoint> sp)
    {
        var gc    = ColorHelper.ParseColor(settings.TailGlowColor);
        var gw    = settings.TailGlowWidth;
        var maxW  = settings.TailThickness + gw * 2;
        var count = sp.Count;

        for (var i = 2; i < count; i += 2)
        {
            var a = sp[i - 1];
            var b = sp[i];

            if (b.Timestamp - a.Timestamp > gapLifetime) continue;

            var prog  = i / (double)(count - 1);
            var fade  = Math.Clamp(prog * (1.0 - b.AgeRatio) * 0.45, 0, 0.45);
            if (fade <= 0.01) continue;

            var w     = gw + maxW * prog;
            var alpha = (byte)(gc.A * fade);
            var brush = new SolidColorBrush(MediaColor.FromArgb(alpha, gc.R, gc.G, gc.B));
            brush.Freeze();
            var pen = new System.Windows.Media.Pen(brush, w) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();
            dc.DrawLine(pen, a.Position, b.Position);
        }
    }

    private void DrawHead(DrawingContext dc, WpfPoint pt)
    {
        if (settings.DotUseCustomImage && _dotFrames.Count > 0)
        {
            var frame  = _dotFrames[_dotFrameIdx % _dotFrames.Count];
            var halfH  = settings.DotSize * 6.0;
            var aspect = (frame.PixelWidth > 0 && frame.PixelHeight > 0)
                         ? (double)frame.PixelWidth / frame.PixelHeight
                         : 1.0;
            var halfW  = halfH * aspect;

            // Rotate to follow movement; flip X instead of rotating past ±90° (keeps image right-side up)
            var angle  = Math.Atan2(_moveDir.Y, _moveDir.X) * 180.0 / Math.PI;
            bool flipX = Math.Abs(angle) > 90.0;
            if (flipX) angle = -(angle > 0.0 ? 180.0 - angle : -180.0 - angle);

            dc.PushTransform(new RotateTransform(angle, pt.X, pt.Y));
            if (flipX) dc.PushTransform(new ScaleTransform(-1, 1, pt.X, pt.Y));
            dc.DrawImage(frame, new Rect(pt.X - halfW, pt.Y - halfH, halfW * 2, halfH * 2));
            if (flipX) dc.Pop();
            dc.Pop();
            return;
        }

        var dotColor = ColorHelper.ParseColor(settings.DotColor);
        var dotSize  = settings.DotSize;

        if (settings.DotGlowEnabled)
        {
            var gc     = ColorHelper.ParseColor(settings.DotGlowColor);
            var radius = settings.DotGlowRadius;
            var glow   = new RadialGradientBrush(gc, MediaColor.FromArgb(0, gc.R, gc.G, gc.B))
            {
                Center         = new WpfPoint(0.5, 0.5),
                GradientOrigin = new WpfPoint(0.5, 0.5),
                RadiusX        = 1,
                RadiusY        = 1,
            };
            glow.Freeze();
            dc.DrawEllipse(glow, null, pt, radius, radius);
        }

        var headBrush = new SolidColorBrush(dotColor);
        headBrush.Freeze();
        dc.DrawEllipse(headBrush, null, pt, dotSize, dotSize);
    }

    // ── Custom image loading ──────────────────────────────────────────────────

    private void UnloadDotImage()
    {
        _gifTimer?.Stop();
        _gifTimer      = null;
        _dotFrames.Clear();
        _dotFrameIdx   = 0;
        _loadedDotPath = "";
    }

    private void LoadDotImage(string path)
    {
        UnloadDotImage();
        _loadedDotPath = path;

        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        try
        {
            using var img  = System.Drawing.Image.FromFile(path);
            var fd         = new System.Drawing.Imaging.FrameDimension(img.FrameDimensionsList[0]);
            int frameCount = img.GetFrameCount(fd);

            // Pre-decode all frames to BitmapSources
            for (int i = 0; i < frameCount; i++)
            {
                img.SelectActiveFrame(fd, i);
                using var bmp = new System.Drawing.Bitmap(img);
                using var ms  = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Seek(0, SeekOrigin.Begin);
                var frame = BitmapFrame.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                frame.Freeze();
                _dotFrames.Add(frame);
            }

            // Start animation timer for multi-frame GIFs
            if (frameCount > 1)
            {
                int delayMs = 100;
                try
                {
                    var prop = img.GetPropertyItem(0x5100);
                    if (prop?.Value is not null)
                        delayMs = Math.Max(20, BitConverter.ToInt32(prop.Value, 0) * 10);
                }
                catch { }

                _gifTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
                _gifTimer.Tick += (_, _) =>
                {
                    _dotFrameIdx = (_dotFrameIdx + 1) % _dotFrames.Count;
                    InvalidateVisual();
                };
                _gifTimer.Start();
            }
        }
        catch { /* ignore unreadable image files */ }
    }

    private void UnloadTrailImage()
    {
        _trailImage      = null;
        _loadedTrailPath = "";
    }

    private void LoadTrailImage(string path)
    {
        UnloadTrailImage();
        _loadedTrailPath = path;

        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        try
        {
            var decoder = BitmapDecoder.Create(
                new Uri(path, UriKind.Absolute),
                BitmapCreateOptions.None,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count > 0)
            {
                var frame = decoder.Frames[0];
                frame.Freeze();
                _trailImage = frame;
            }
        }
        catch { /* ignore unreadable image files */ }
    }

    // ── Rainbow trail ─────────────────────────────────────────────────────────

    private static readonly MediaColor[] RainbowStops =
    {
        MediaColor.FromRgb(148,   0, 211), // Violet  — tail (oldest)
        MediaColor.FromRgb( 75,   0, 130), // Indigo
        MediaColor.FromRgb(  0,   0, 255), // Blue
        MediaColor.FromRgb(  0, 200,   0), // Green
        MediaColor.FromRgb(255, 255,   0), // Yellow
        MediaColor.FromRgb(255, 127,   0), // Orange
        MediaColor.FromRgb(255,   0,   0), // Red     — head (newest)
    };

    private static MediaColor SampleRainbow(double t)
    {
        // t: 0 = tail, 1 = head → maps to violet…red
        double idx = t * (RainbowStops.Length - 1);
        int lo = (int)idx;
        int hi = Math.Min(lo + 1, RainbowStops.Length - 1);
        return ColorHelper.Lerp(RainbowStops[lo], RainbowStops[hi], idx - lo);
    }

    // ── Smoothing & Interpolation ─────────────────────────────────────────────

    private List<SmoothedPoint> BuildSmoothedPoints(DateTime now)
    {
        var active = points.ToList();
        if (active.Count < 2)
        {
            return active.Select(p => new SmoothedPoint(
                p.Position, p.Timestamp,
                Math.Clamp((now - p.Timestamp).TotalMilliseconds / trailLifetime.TotalMilliseconds, 0, 1))).ToList();
        }

        var butter = settings.ButterModeEnabled;
        var lenDiv = butter ? 1.9 : 2.4;
        var angDiv = butter ? 5.5 : 8.0;
        var minSub = butter ? 10  : 6;
        var maxSub = butter ? 56  : 32;

        var expanded = new List<SmoothedPoint>(active.Count * 8)
        {
            new(active[0].Position, active[0].Timestamp,
                Math.Clamp((now - active[0].Timestamp).TotalMilliseconds / trailLifetime.TotalMilliseconds, 0, 1))
        };

        for (var i = 0; i < active.Count - 1; i++)
        {
            var p0 = i > 0 ? active[i - 1].Position : active[i].Position;
            var p1 = active[i].Position;
            var p2 = active[i + 1].Position;
            var p3 = i + 2 < active.Count ? active[i + 2].Position : active[i + 1].Position;
            var t1 = active[i].Timestamp;
            var t2 = active[i + 1].Timestamp;

            var len  = Dist(p1, p2);
            var turn = TurnAngle(p0, p1, p2);
            var subs = Math.Clamp((int)(len / lenDiv) + (int)(turn / angDiv), minSub, maxSub);

            for (var step = 1; step <= subs; step++)
            {
                var t         = step / (double)subs;
                var pos       = CatmullRom(p0, p1, p2, p3, t);
                var ts        = LerpTime(t1, t2, t);
                var ageRatio  = Math.Clamp((now - ts).TotalMilliseconds / trailLifetime.TotalMilliseconds, 0, 1);
                expanded.Add(new SmoothedPoint(pos, ts, ageRatio));
            }
        }

        // ── Laplacian smoothing pass (improves circle / curve quality) ──────────
        for (int i = 1; i < expanded.Count - 1; i++)
        {
            var px = expanded[i - 1].Position.X * 0.2 + expanded[i].Position.X * 0.6 + expanded[i + 1].Position.X * 0.2;
            var py = expanded[i - 1].Position.Y * 0.2 + expanded[i].Position.Y * 0.6 + expanded[i + 1].Position.Y * 0.2;
            expanded[i] = new SmoothedPoint(new WpfPoint(px, py), expanded[i].Timestamp, expanded[i].AgeRatio);
        }

        // ── Wave effect ──────────────────────────────────────────────────────────
        if (settings.WaveEnabled && settings.WaveAmplitude > 0.01 && expanded.Count > 2)
        {
            var totalLen = 0.0;
            var lengths  = new double[expanded.Count];
            lengths[0] = 0;
            for (int i = 1; i < expanded.Count; i++)
            {
                totalLen  += Dist(expanded[i - 1].Position, expanded[i].Position);
                lengths[i] = totalLen;
            }

            if (totalLen > 0.01)
            {
                var amp  = settings.WaveAmplitude;
                var freq = settings.WaveFrequency;
                for (int i = 1; i < expanded.Count - 1; i++)
                {
                    var dx  = expanded[i + 1].Position.X - expanded[i - 1].Position.X;
                    var dy  = expanded[i + 1].Position.Y - expanded[i - 1].Position.Y;
                    var len = Math.Sqrt(dx * dx + dy * dy);
                    if (len < 0.001) continue;
                    var nx     = -dy / len;
                    var ny     =  dx / len;
                    var phase  = lengths[i] / totalLen * freq * Math.PI * 2.0 + _wavePhase;
                    var offset = amp * Math.Sin(phase);
                    var p      = expanded[i];
                    expanded[i] = new SmoothedPoint(
                        new WpfPoint(p.Position.X + nx * offset, p.Position.Y + ny * offset),
                        p.Timestamp, p.AgeRatio);
                }
            }
        }

        return expanded;
    }

    private WpfPoint ApplyAdaptiveSmoothing(WpfPoint raw, DateTime now)
    {
        if (filteredCursor is null)
        {
            filteredCursor = raw;
            lastInputTime  = now;
            return raw;
        }

        var dtMs  = Math.Max((now - lastInputTime).TotalMilliseconds, 1.0);
        var speed = Dist(filteredCursor.Value, raw) / dtMs;
        var b     = settings.ButterModeEnabled;
        var alpha = Math.Clamp((b ? 0.22 : 0.28) + speed * (b ? 0.022 : 0.03), b ? 0.22 : 0.28, b ? 0.78 : 0.65);

        filteredCursor = new WpfPoint(
            filteredCursor.Value.X + (raw.X - filteredCursor.Value.X) * alpha,
            filteredCursor.Value.Y + (raw.Y - filteredCursor.Value.Y) * alpha);

        lastInputTime = now;
        return filteredCursor.Value;
    }

    private void TrimPoints(DateTime now)
    {
        var cutoff = now - trailLifetime;
        points.RemoveAll(p => p.Timestamp < cutoff);
    }

    // ── Math helpers ──────────────────────────────────────────────────────────

    private static double Dist(WpfPoint a, WpfPoint b)
    {
        var dx = a.X - b.X; var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static Vector UnitNormal(WpfPoint a, WpfPoint b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        return len < 0.0001 ? new Vector(0, 0) : new Vector(-dy / len, dx / len);
    }

    private static double TurnAngle(WpfPoint p0, WpfPoint p1, WpfPoint p2)
    {
        var v1x = p1.X - p0.X; var v1y = p1.Y - p0.Y;
        var v2x = p2.X - p1.X; var v2y = p2.Y - p1.Y;
        var l1  = Math.Sqrt(v1x * v1x + v1y * v1y);
        var l2  = Math.Sqrt(v2x * v2x + v2y * v2y);
        if (l1 < 0.0001 || l2 < 0.0001) return 0;
        var dot = Math.Clamp((v1x * v2x + v1y * v2y) / (l1 * l2), -1, 1);
        return Math.Acos(dot) * 180.0 / Math.PI;
    }

    private static WpfPoint CatmullRom(WpfPoint p0, WpfPoint p1, WpfPoint p2, WpfPoint p3, double t)
    {
        var t2 = t * t; var t3 = t2 * t;
        return new WpfPoint(
            0.5 * ((2 * p1.X) + (-p0.X + p2.X) * t + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3),
            0.5 * ((2 * p1.Y) + (-p0.Y + p2.Y) * t + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3));
    }

    private static DateTime LerpTime(DateTime a, DateTime b, double t) =>
        new(a.Ticks + (long)((b.Ticks - a.Ticks) * t), DateTimeKind.Utc);

    private readonly record struct TrailPoint(WpfPoint Position, DateTime Timestamp);
    private readonly record struct SmoothedPoint(WpfPoint Position, DateTime Timestamp, double AgeRatio);
}
