using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using MediaColor = System.Windows.Media.Color;

namespace LaserCursorApp;

public class LaserOverlayControl : FrameworkElement
{
    private readonly List<TrailPoint> points = new();
    private readonly TimeSpan trailLifetime = TimeSpan.FromMilliseconds(380);
    private readonly TimeSpan gapLifetime = TimeSpan.FromMilliseconds(90);

    private WpfPoint? latestCursor;
    private DateTime lastSampleTime = DateTime.MinValue;
    private DateTime lastInputTime = DateTime.MinValue;
    private WpfPoint? filteredCursor;
    private readonly MediaColor trailColor = MediaColor.FromArgb(250, 255, 28, 28);

    private static readonly MediaColor LaserColor = MediaColor.FromRgb(255, 40, 40);

    public bool ButterModeEnabled { get; set; }

    public void UpdateCursor(WpfPoint? position)
    {
        var now = DateTime.UtcNow;

        if (position is null)
        {
            latestCursor = null;
            filteredCursor = null;
            TrimPoints(now);
            InvalidateVisual();
            return;
        }

        latestCursor = ApplyAdaptiveSmoothing(position.Value, now);

        var minDistance = ButterModeEnabled ? 0.35 : 0.65;
        var sampleIntervalMs = ButterModeEnabled ? 4 : 10;

        if (points.Count == 0 || Distance(points[^1].Position, latestCursor.Value) > minDistance)
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
        latestCursor = null;
        filteredCursor = null;
        points.Clear();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (points.Count > 1)
        {
            DrawTrail(drawingContext);
        }

        if (latestCursor is not null)
        {
            DrawHead(drawingContext, latestCursor.Value);
        }
    }

    private void DrawTrail(DrawingContext drawingContext)
    {
        var smoothedPoints = BuildSmoothedPoints(DateTime.UtcNow);
        if (smoothedPoints.Count < 2)
        {
            return;
        }

        var maxThickness = ButterModeEnabled ? 8.5 : 8.0;
        var minThickness = 0.35;

        for (var i = 1; i < smoothedPoints.Count; i++)
        {
            var a = smoothedPoints[i - 1];
            var b = smoothedPoints[i];

            if (b.Timestamp - a.Timestamp > gapLifetime)
            {
                continue;
            }

            var progress = i / (double)(smoothedPoints.Count - 1);
            var taper = Math.Pow(progress, 0.8);
            var ageFade = 1.0 - b.AgeRatio;
            var combinedFade = Math.Clamp(taper * ageFade, 0.0, 1.0);

            if (combinedFade <= 0.01)
            {
                continue;
            }

            var widthA = minThickness + (maxThickness - minThickness) * Math.Clamp(Math.Pow((i - 1) / (double)(smoothedPoints.Count - 1), 0.8) * (1.0 - a.AgeRatio), 0.0, 1.0);
            var widthB = minThickness + (maxThickness - minThickness) * Math.Clamp(Math.Pow(i / (double)(smoothedPoints.Count - 1), 0.8) * (1.0 - b.AgeRatio), 0.0, 1.0);

            if (Distance(a.Position, b.Position) < 0.01)
            {
                continue;
            }

            var normal = GetUnitNormal(a.Position, b.Position);
            var p1 = a.Position + normal * (widthA * 0.5);
            var p2 = a.Position - normal * (widthA * 0.5);
            var p3 = b.Position - normal * (widthB * 0.5);
            var p4 = b.Position + normal * (widthB * 0.5);

            var alpha = (byte)(trailColor.A * combinedFade);
            var brush = new SolidColorBrush(MediaColor.FromArgb(alpha, trailColor.R, trailColor.G, trailColor.B));
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

            drawingContext.DrawGeometry(brush, null, quad);
        }
    }

    private void DrawHead(DrawingContext drawingContext, WpfPoint point)
    {
        var headBrush = new SolidColorBrush(MediaColor.FromArgb(255, 255, 30, 30));
        headBrush.Freeze();

        drawingContext.DrawEllipse(headBrush, null, point, 3.5, 3.5);
    }

    private void TrimPoints(DateTime now)
    {
        var cutoff = now - trailLifetime;
        points.RemoveAll(p => p.Timestamp < cutoff);
    }

    private static double Distance(WpfPoint p1, WpfPoint p2)
    {
        var dx = p1.X - p2.X;
        var dy = p1.Y - p2.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private List<SmoothedPoint> BuildSmoothedPoints(DateTime now)
    {
        var active = points.ToList();
        if (active.Count < 2)
        {
            return active
                .Select(p => new SmoothedPoint(p.Position, p.Timestamp, Math.Clamp((now - p.Timestamp).TotalMilliseconds / trailLifetime.TotalMilliseconds, 0, 1)))
                .ToList();
        }

        var expanded = new List<SmoothedPoint>(active.Count * 8)
        {
            new(
                active[0].Position,
                active[0].Timestamp,
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

            var segmentLength = Distance(p1, p2);
            var turnAngle = CalculateTurnAngle(p0, p1, p2);

            var lengthDivisor = ButterModeEnabled ? 1.9 : 2.8;
            var turnDivisor = ButterModeEnabled ? 5.5 : 8.0;
            var minSubdivisions = ButterModeEnabled ? 10 : 4;
            var maxSubdivisions = ButterModeEnabled ? 56 : 24;

            var subdivisions = Math.Clamp((int)(segmentLength / lengthDivisor) + (int)(turnAngle / turnDivisor), minSubdivisions, maxSubdivisions);

            for (var step = 1; step <= subdivisions; step++)
            {
                var t = step / (double)subdivisions;
                var position = CatmullRom(p0, p1, p2, p3, t);
                var timestamp = InterpolateTime(t1, t2, t);
                var ageRatio = Math.Clamp((now - timestamp).TotalMilliseconds / trailLifetime.TotalMilliseconds, 0, 1);
                expanded.Add(new SmoothedPoint(position, timestamp, ageRatio));
            }
        }

        return expanded;
    }

    private static DateTime InterpolateTime(DateTime start, DateTime end, double t)
    {
        var ticks = start.Ticks + (long)((end.Ticks - start.Ticks) * t);
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    private static double CalculateTurnAngle(WpfPoint p0, WpfPoint p1, WpfPoint p2)
    {
        var v1x = p1.X - p0.X;
        var v1y = p1.Y - p0.Y;
        var v2x = p2.X - p1.X;
        var v2y = p2.Y - p1.Y;

        var len1 = Math.Sqrt(v1x * v1x + v1y * v1y);
        var len2 = Math.Sqrt(v2x * v2x + v2y * v2y);

        if (len1 < 0.0001 || len2 < 0.0001)
        {
            return 0;
        }

        var dot = (v1x * v2x + v1y * v2y) / (len1 * len2);
        dot = Math.Clamp(dot, -1.0, 1.0);
        var radians = Math.Acos(dot);
        return radians * 180.0 / Math.PI;
    }

    private static WpfPoint CatmullRom(WpfPoint p0, WpfPoint p1, WpfPoint p2, WpfPoint p3, double t)
    {
        var t2 = t * t;
        var t3 = t2 * t;

        var x = 0.5 * (
            (2.0 * p1.X) +
            (-p0.X + p2.X) * t +
            (2.0 * p0.X - 5.0 * p1.X + 4.0 * p2.X - p3.X) * t2 +
            (-p0.X + 3.0 * p1.X - 3.0 * p2.X + p3.X) * t3);

        var y = 0.5 * (
            (2.0 * p1.Y) +
            (-p0.Y + p2.Y) * t +
            (2.0 * p0.Y - 5.0 * p1.Y + 4.0 * p2.Y - p3.Y) * t2 +
            (-p0.Y + 3.0 * p1.Y - 3.0 * p2.Y + p3.Y) * t3);

        return new WpfPoint(x, y);
    }

    private static Vector GetUnitNormal(WpfPoint a, WpfPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);

        if (length < 0.0001)
        {
            return new Vector(0, 0);
        }

        return new Vector(-dy / length, dx / length);
    }

    private WpfPoint ApplyAdaptiveSmoothing(WpfPoint raw, DateTime now)
    {
        if (filteredCursor is null)
        {
            filteredCursor = raw;
            lastInputTime = now;
            return raw;
        }

        var dtMs = Math.Max((now - lastInputTime).TotalMilliseconds, 1.0);
        var speed = Distance(filteredCursor.Value, raw) / dtMs;

        var baseAlpha = ButterModeEnabled ? 0.22 : 0.28;
        var speedBoost = ButterModeEnabled ? 0.022 : 0.03;
        var maxAlpha = ButterModeEnabled ? 0.78 : 0.65;
        var alpha = Math.Clamp(baseAlpha + speed * speedBoost, baseAlpha, maxAlpha);

        var x = filteredCursor.Value.X + (raw.X - filteredCursor.Value.X) * alpha;
        var y = filteredCursor.Value.Y + (raw.Y - filteredCursor.Value.Y) * alpha;

        var smoothed = new WpfPoint(x, y);
        filteredCursor = smoothed;
        lastInputTime = now;
        return smoothed;
    }

    private readonly record struct TrailPoint(WpfPoint Position, DateTime Timestamp);
    private readonly record struct SmoothedPoint(WpfPoint Position, DateTime Timestamp, double AgeRatio);
}
