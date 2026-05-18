using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
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

    // ── Public API ────────────────────────────────────────────────────────────

    public void ApplySettings(LaserSettings s)
    {
        settings      = s;
        trailLifetime = TimeSpan.FromMilliseconds(s.TailLengthMs);
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

            MediaColor segColor;
            if (endColor is not null)
            {
                var lerped = ColorHelper.Lerp(tailColor, endColor.Value, 1.0 - prog);
                segColor   = MediaColor.FromArgb((byte)(lerped.A * combinedFade), lerped.R, lerped.G, lerped.B);
            }
            else
            {
                segColor = MediaColor.FromArgb((byte)(tailColor.A * combinedFade), tailColor.R, tailColor.G, tailColor.B);
            }

            var n  = UnitNormal(a.Position, b.Position);
            var p1 = a.Position + n * (wA * 0.5);
            var p2 = a.Position - n * (wA * 0.5);
            var p3 = b.Position - n * (wB * 0.5);
            var p4 = b.Position + n * (wB * 0.5);

            var brush = new SolidColorBrush(segColor);
            brush.Freeze();

            var quad = new StreamGeometry();
            using (var ctx = quad.Open())
            {
                ctx.BeginFigure(p1, true, true);
                ctx.LineTo(p2, true, false);
                ctx.LineTo(p3, true, false);
                ctx.LineTo(p4, true, false);
            }
            quad.Freeze();
            dc.DrawGeometry(brush, null, quad);
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
        var lenDiv = butter ? 1.9 : 2.8;
        var angDiv = butter ? 5.5 : 8.0;
        var minSub = butter ? 10  : 4;
        var maxSub = butter ? 56  : 24;

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
