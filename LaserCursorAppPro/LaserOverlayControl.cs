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
    // ── Wave animation ─────────────────────────────────────────────────────
    // Phase accumulates based on cursor travel distance so adjacent recorded
    // points have nearly identical offsets (no shaking) and painted positions
    // are frozen in space forever (nyan-cat-style stationary rainbow).
    private double    _waveDistPhase         = 0.0;
    private double    _wavePhaseAtLastRecord  = 0.0;  // phase value when last point was recorded
    private WpfPoint? _prevCursorForWave;
    private WpfPoint? _lastRecordedCursor;
    // ── Auto-hide ─────────────────────────────────────────────────────────────
    private WpfPoint? _prevCursorForHide;
    private DateTime  _lastMoveTime = DateTime.UtcNow;
    // ── Click effects ─────────────────────────────────────────────────────────
    private readonly List<ClickEffect> _clickEffects = new();
    // ── Click particle image ──────────────────────────────────────────────────
    private BitmapSource? _clickParticleImage;
    private string        _loadedParticleImagePath = "";
    // ── Click cursor swap ────────────────────────────────────────────────────────
    private BitmapSource? _clickSwapImage;
    private string        _loadedClickSwapPath = "";
    private DateTime      _clickSwapUntil      = DateTime.MinValue;
    // ── Draw mode ─────────────────────────────────────────────────────────────
    private readonly List<List<WpfPoint>> _drawnStrokes = new();
    private List<WpfPoint>?               _currentStroke = null;
    private bool                          _drawModeActive = false;
    // ── Public API ─────────────────────────────────────────────────────────────

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

        if (s.ClickParticleImagePath != _loadedParticleImagePath)
            LoadClickParticleImage(s.ClickParticleImagePath);
        if (s.ClickSwapImagePath != _loadedClickSwapPath)
            LoadClickSwapImage(s.ClickSwapImagePath);

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

        var butter           = settings.ButterModeEnabled;
        var minDist          = butter ? 0.35 : 0.65;
        var sampleIntervalMs = butter ? 4 : 10;

        // Accumulate travel distance and update movement direction from the raw
        // un-waved cursor. Using _prevCursorForWave (not points[]) avoids direction
        // contamination from wave offsets baked into recorded positions.
        if (_prevCursorForWave is not null)
        {
            var delta     = latestCursor.Value - _prevCursorForWave.Value;
            var deltaDist = delta.Length;
            _waveDistPhase += deltaDist;
            if (deltaDist > 0.5)
            {
                var rNorm = new Vector(delta.X / deltaDist, delta.Y / deltaDist);
                _moveDir  = new Vector(
                    _moveDir.X + (rNorm.X - _moveDir.X) * 0.25,
                    _moveDir.Y + (rNorm.Y - _moveDir.Y) * 0.25);
                var mLen = _moveDir.Length;
                if (mLen > 0.001) _moveDir = new Vector(_moveDir.X / mLen, _moveDir.Y / mLen);
            }
        }
        _prevCursorForWave = latestCursor.Value;

        // Track last movement time for auto-hide
        if (_prevCursorForHide is null || Dist(_prevCursorForHide.Value, latestCursor.Value) > 1.0)
        {
            _prevCursorForHide = latestCursor.Value;
            _lastMoveTime      = now;
        }

        // Fixed-distance recording with sub-frame interpolation.
        // When the cursor jumps a large distance in one hardware polling interval,
        // multiple evenly-spaced anchors are inserted along the path so wave
        // anchor density is constant regardless of cursor speed.
        bool   isWave  = settings.WaveEnabled && settings.WaveAmplitude > 0.01;
        double sampleD = isWave ? 8.0 : minDist;

        bool shouldRecord = _lastRecordedCursor is null
                            || Dist(_lastRecordedCursor.Value, latestCursor.Value) >= sampleD
                            || now - lastSampleTime > TimeSpan.FromMilliseconds(sampleIntervalMs);
        if (shouldRecord)
        {
            var    from   = _lastRecordedCursor;
            var    to     = latestCursor.Value;

            if (isWave && from is not null)
            {
                double segDx  = to.X - from.Value.X;
                double segDy  = to.Y - from.Value.Y;
                double segLen = Math.Sqrt(segDx * segDx + segDy * segDy);

                // Exact local perpendicular — no EMA lag, always correct at any speed
                double nx = segLen > 0.1 ? -segDy / segLen : -_moveDir.Y;
                double ny = segLen > 0.1 ?  segDx / segLen :  _moveDir.X;

                // Subdivide large gaps so every wave cycle has ~10 anchors
                int    n         = Math.Max(1, (int)(segLen / sampleD));
                double phaseSpan = _waveDistPhase - _wavePhaseAtLastRecord;

                for (int i = 1; i <= n; i++)
                {
                    double t      = i / (double)n;
                    double iphase = _wavePhaseAtLastRecord + phaseSpan * t;
                    double woff   = settings.WaveAmplitude
                                    * Math.Sin(iphase * settings.WaveFrequency / 200.0 * Math.PI * 2.0);
                    var ipos = new WpfPoint(from.Value.X + segDx * t, from.Value.Y + segDy * t);
                    var wavePoint = new WpfPoint(ipos.X + nx * woff, ipos.Y + ny * woff);
                    points.Add(new TrailPoint(wavePoint, now));
                    if (_drawModeActive && _currentStroke != null)
                        _currentStroke.Add(wavePoint);
                }
            }
            else
            {
                // Non-wave mode or very first point
                points.Add(new TrailPoint(to, now));
                if (_drawModeActive && _currentStroke != null)
                    _currentStroke.Add(to);
            }

            _lastRecordedCursor    = to;
            _wavePhaseAtLastRecord = _waveDistPhase;
            lastSampleTime         = now;
        }

        TrimPoints(now);
        InvalidateVisual();
    }

    public void SetDrawMode(bool active)
    {
        if (!settings.DrawModeEnabled) return;
        if (active == _drawModeActive) return;
        _drawModeActive = active;
        if (active)
        {
            _currentStroke = new List<WpfPoint>();
        }
        else
        {
            if (_currentStroke is { Count: >= 2 })
                _drawnStrokes.Add(_currentStroke);
            _currentStroke = null;
        }
    }

    public void ClearDrawnStrokes()
    {
        _drawnStrokes.Clear();
        _currentStroke = null;
        _drawModeActive = false;
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

        double opacity = 1.0;
        if (settings.AutoHideEnabled)
        {
            var idleMs = (DateTime.UtcNow - _lastMoveTime).TotalMilliseconds;
            const double FadeMs = 600.0;
            opacity = idleMs < settings.AutoHideDelayMs ? 1.0
                    : Math.Clamp(1.0 - (idleMs - settings.AutoHideDelayMs) / FadeMs, 0.0, 1.0);
        }

        DrawClickEffects(dc);
        DrawPersistentStrokes(dc);

        if (opacity > 0.001)
        {
            bool hasOpacity = opacity < 0.999;
            if (hasOpacity) dc.PushOpacity(opacity);

            if (points.Count > 1)
            {
                var smoothed = BuildSmoothedPoints(DateTime.UtcNow);
                if (smoothed.Count >= 2)
                {
                    if (settings.TailGlowEnabled)
                        DrawTrailGlow(dc, smoothed);
                    DrawTrail(dc, smoothed);
                    DrawTrailPattern(dc, smoothed);
                }
            }

            if (latestCursor is not null)
                DrawHead(dc, latestCursor.Value);

            if (hasOpacity) dc.Pop();
        }
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
        if (count < 2) return;

        for (var i = 1; i < count; i++)
        {
            var a = sp[i - 1];
            var b = sp[i];

            if (b.Timestamp - a.Timestamp > gapLifetime) continue;

            var prog  = i / (double)(count - 1);
            var progA = (i - 1) / (double)(count - 1);
            var fadeB = Math.Clamp(Math.Pow(prog,  power) * (1.0 - b.AgeRatio), 0.0, 1.0);
            var fadeA = Math.Clamp(Math.Pow(progA, power) * (1.0 - a.AgeRatio), 0.0, 1.0);
            if (fadeB <= 0.01) continue;

            double wA, wB, alphaFactor;
            switch (settings.TailFadeStyle)
            {
                case 0: // Alpha only — full width, only opacity fades
                    wA          = maxW;
                    wB          = maxW;
                    alphaFactor = fadeB;
                    break;
                case 2: // Glow-dissolve — width expands as trail ages, softer opacity
                    wA          = minW + (maxW - minW) * (1.0 + 1.4 * (1.0 - fadeA));
                    wB          = minW + (maxW - minW) * (1.0 + 1.4 * (1.0 - fadeB));
                    alphaFactor = fadeB * 0.5;
                    break;
                default: // 1 = Thin-and-fade (default) — both taper simultaneously
                    wA          = minW + (maxW - minW) * fadeA;
                    wB          = minW + (maxW - minW) * fadeB;
                    alphaFactor = fadeB;
                    break;
            }
            alphaFactor = Math.Clamp(alphaFactor, 0, 1);
            if (alphaFactor <= 0.01) continue;

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
                // +1.5px bleed each side so adjacent quads overlap and hide the seam at bends
                var halfLen  = dir.Length * 0.5 + 1.5;

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
                    Opacity       = alphaFactor,
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
                MediaColor segColor;
                if (settings.TailRainbowMode)
                {
                    var rc   = SampleRainbow(prog);
                    segColor = MediaColor.FromArgb((byte)(255 * alphaFactor), rc.R, rc.G, rc.B);
                }
                else if (endColor is not null)
                {
                    var lerped = ColorHelper.Lerp(tailColor, endColor.Value, 1.0 - prog);
                    segColor   = MediaColor.FromArgb((byte)(lerped.A * alphaFactor), lerped.R, lerped.G, lerped.B);
                }
                else
                {
                    segColor = MediaColor.FromArgb((byte)(tailColor.A * alphaFactor), tailColor.R, tailColor.G, tailColor.B);
                }
                var sb = new SolidColorBrush(segColor);
                sb.Freeze();
                // DrawLine with round caps: adjacent segments share endpoints and the
                // semicircular caps fill any gap at bends — no seams regardless of angle.
                var pen = new System.Windows.Media.Pen(sb, (wA + wB) * 0.5)
                    { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                pen.Freeze();
                dc.DrawLine(pen, a.Position, b.Position);
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

    // ── Trail pattern overlays ───────────────────────────────────────────────

    private void DrawTrailPattern(DrawingContext dc, List<SmoothedPoint> sp)
    {
        if (settings.TrailPattern == 0 || sp.Count < 2) return;
        double t    = DateTime.UtcNow.TimeOfDay.TotalSeconds;
        double spd  = settings.TrailPatternSpeed;
        double inty = settings.TrailPatternIntensity;
        double max  = settings.TailThickness;

        int step = Math.Max(1, sp.Count / 30);

        switch (settings.TrailPattern)
        {
            case 1: // Fire — warm flames licking upward from the trail
            {
                for (int i = 0; i < sp.Count; i += step)
                {
                    var    pt  = sp[i].Position;
                    double age = sp[i].AgeRatio;
                    double baseH = max * 2.5 * inty;
                    for (int f = 0; f < 3; f++)
                    {
                        double phase = (t * spd * 2.0 + i * 0.618034 + f * 0.33333) % 1.0;
                        double yOff  = -phase * baseH;
                        double xOff  = Math.Sin(phase * Math.PI * 2.0 + i * 1.309) * max * 0.45;
                        double sz    = Math.Max(0.5, max * 0.55 * (1.0 - phase) * (1.0 - age));
                        double a     = (1.0 - phase) * (1.0 - age * 0.8) * inty;
                        byte   g     = (byte)Math.Max(0, 255 * (1.0 - phase * 0.85));
                        var    c     = MediaColor.FromArgb((byte)(a * 200), 255, g, 0);
                        var    br    = new SolidColorBrush(c); br.Freeze();
                        dc.DrawEllipse(br, null, new WpfPoint(pt.X + xOff, pt.Y + yOff), sz, sz);
                    }
                }
                break;
            }
            case 2: // Electric — jagged spark arcs branching off the trail
            {
                int frame2 = (int)(t * spd * 6) % 9973;
                var rng    = new System.Random(frame2);
                for (int i = 1; i < sp.Count; i += step)
                {
                    if (rng.NextDouble() > 0.35 * inty) continue;
                    var    pt    = sp[i].Position;
                    var    prv   = sp[i - 1].Position;
                    double age   = sp[i].AgeRatio;
                    var    dir   = new Vector(pt.X - prv.X, pt.Y - prv.Y);
                    if (dir.Length < 0.01) continue;
                    dir.Normalize();
                    var    perp  = new Vector(-dir.Y, dir.X);
                    double len   = max * 3.0 * inty * (1.0 - age);
                    double alpha = 0.85 * (1.0 - age) * inty;
                    var    pts   = new List<WpfPoint> { pt };
                    var    cur   = pt;
                    double rem   = len;
                    while (rem > 1.5)
                    {
                        double seg = Math.Min(rem, max * 0.9);
                        double off = (rng.NextDouble() - 0.5) * max * 1.8;
                        var    nxt = new WpfPoint(cur.X + dir.X * seg * 0.5 + perp.X * off,
                                                  cur.Y + dir.Y * seg * 0.5 + perp.Y * off);
                        pts.Add(nxt); cur = nxt; rem -= seg;
                    }
                    var col = MediaColor.FromArgb((byte)(alpha * 230), 170, 210, 255);
                    var pen = new System.Windows.Media.Pen(new SolidColorBrush(col), 0.9); pen.Freeze();
                    for (int k = 0; k < pts.Count - 1; k++)
                        dc.DrawLine(pen, pts[k], pts[k + 1]);
                }
                break;
            }
            case 3: // Smoke — soft gray puffs drifting upward
            {
                for (int i = 0; i < sp.Count; i += step)
                {
                    var    pt    = sp[i].Position;
                    double age   = sp[i].AgeRatio;
                    double phase = (t * spd * 0.4 + i * 0.4142) % 1.0;
                    double yOff  = -phase * max * 5.0 * inty;
                    double xOff  = Math.Sin(phase * Math.PI + i * 0.916) * max * 0.9;
                    double sz    = max * (0.6 + phase * 1.8) * (1.0 - age * 0.7) * inty;
                    double a     = (1.0 - phase) * (1.0 - age * 0.5) * inty * 0.45;
                    if (sz < 0.5) continue;
                    var c  = MediaColor.FromArgb((byte)(a * 180), 185, 185, 200);
                    var br = new SolidColorBrush(c); br.Freeze();
                    dc.DrawEllipse(br, null, new WpfPoint(pt.X + xOff, pt.Y + yOff), sz, sz);
                }
                break;
            }
            case 4: // Plasma — cycling rainbow hue overlay along the trail
            {
                for (int i = 0; i < sp.Count; i += step)
                {
                    var    pt  = sp[i].Position;
                    double age = sp[i].AgeRatio;
                    double hue = (t * spd * 120.0 + i * 8.0) % 360.0;
                    double sz  = max * 0.75 * (1.0 - age) * inty;
                    double a   = (1.0 - age) * inty * 0.65;
                    if (sz < 0.5) continue;
                    var c  = HsvToRgb(hue, 1.0, 1.0, a);
                    var br = new SolidColorBrush(c); br.Freeze();
                    dc.DrawEllipse(br, null, pt, sz, sz);
                }
                break;
            }
        }
    }

    private static MediaColor HsvToRgb(double h, double s, double v, double a)
    {
        h %= 360;
        double c  = v * s;
        double x  = c * (1.0 - Math.Abs(h / 60.0 % 2.0 - 1.0));
        double m  = v - c;
        double r1, g1, b1;
        if      (h < 60 ) { r1 = c; g1 = x; b1 = 0; }
        else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
        else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
        else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
        else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
        else              { r1 = c; g1 = 0; b1 = x; }
        return MediaColor.FromArgb((byte)(a * 255),
            (byte)((r1 + m) * 255), (byte)((g1 + m) * 255), (byte)((b1 + m) * 255));
    }

    private void DrawHead(DrawingContext dc, WpfPoint pt)
    {
        // ── Click cursor swap override ───────────────────────────────────────────────
        if (settings.ClickSwapEnabled && _clickSwapImage is not null && DateTime.UtcNow < _clickSwapUntil)
        {
            var swapHalfH  = settings.ClickSwapSize;
            var swapAspect = (_clickSwapImage.PixelWidth > 0 && _clickSwapImage.PixelHeight > 0)
                             ? (double)_clickSwapImage.PixelWidth / _clickSwapImage.PixelHeight : 1.0;
            var swapHalfW  = swapHalfH * swapAspect;
            var swapAngle  = Math.Atan2(_moveDir.Y, _moveDir.X) * 180.0 / Math.PI;
            bool swapFlipX = Math.Abs(swapAngle) > 90.0;
            if (swapFlipX) swapAngle = -(swapAngle > 0.0 ? 180.0 - swapAngle : -180.0 - swapAngle);
            dc.PushTransform(new RotateTransform(swapAngle, pt.X, pt.Y));
            if (swapFlipX) dc.PushTransform(new ScaleTransform(-1, 1, pt.X, pt.Y));
            dc.DrawImage(_clickSwapImage, new Rect(pt.X - swapHalfW, pt.Y - swapHalfH, swapHalfW * 2, swapHalfH * 2));
            if (swapFlipX) dc.Pop();
            dc.Pop();
            return;
        }

        if (settings.DotUseCustomImage && _dotFrames.Count > 0)
        {
            var frame  = _dotFrames[_dotFrameIdx % _dotFrames.Count];
            var halfH  = settings.DotSize * 6.0;
            if (settings.PulseEnabled)
            {
                var secs = DateTime.UtcNow.TimeOfDay.TotalSeconds;
                halfH *= 1.0 + settings.PulseIntensity * Math.Sin(2.0 * Math.PI * settings.PulseSpeed * secs);
            }
            var aspect = (frame.PixelWidth > 0 && frame.PixelHeight > 0)
                         ? (double)frame.PixelWidth / frame.PixelHeight
                         : 1.0;
            var halfW  = halfH * aspect;

            // Rotate to follow movement; flip X instead of rotating past ±90° (keeps image right-side up)
            var angle  = Math.Atan2(_moveDir.Y, _moveDir.X) * 180.0 / Math.PI;
            bool flipX = Math.Abs(angle) > 90.0;
            if (flipX) angle = -(angle > 0.0 ? 180.0 - angle : -180.0 - angle);
            if (settings.SpinEnabled)
                angle += DateTime.UtcNow.TimeOfDay.TotalSeconds * settings.SpinSpeed * (settings.SpinCW ? 1.0 : -1.0);

            dc.PushTransform(new RotateTransform(angle, pt.X, pt.Y));
            if (flipX) dc.PushTransform(new ScaleTransform(-1, 1, pt.X, pt.Y));
            dc.DrawImage(frame, new Rect(pt.X - halfW, pt.Y - halfH, halfW * 2, halfH * 2));
            if (flipX) dc.Pop();
            dc.Pop();
            return;
        }

        var dotColor = ColorHelper.ParseColor(settings.DotColor);
        var dotSize  = settings.DotSize;
        if (settings.PulseEnabled)
        {
            var secs = DateTime.UtcNow.TimeOfDay.TotalSeconds;
            dotSize *= 1.0 + settings.PulseIntensity * Math.Sin(2.0 * Math.PI * settings.PulseSpeed * secs);
        }

        if (settings.DotGlowEnabled)
        {
            var gc     = ColorHelper.ParseColor(settings.DotGlowColor);
            var radius = settings.DotGlowRadius;
            switch (settings.DotGlowBloom)
            {
                case 1: // Hard — solid fill, sharp edge
                {
                    var hardBrush = new SolidColorBrush(gc);
                    hardBrush.Freeze();
                    dc.DrawEllipse(hardBrush, null, pt, radius, radius);
                    break;
                }
                case 2: // Pulse — soft gradient, radius pulsates with dot
                    if (settings.PulseEnabled)
                    {
                        var secs2 = DateTime.UtcNow.TimeOfDay.TotalSeconds;
                        radius   *= 1.0 + settings.PulseIntensity * Math.Sin(2.0 * Math.PI * settings.PulseSpeed * secs2);
                    }
                    goto default;
                default: // Soft — radial gradient falloff
                {
                    var glow = new RadialGradientBrush(gc, MediaColor.FromArgb(0, gc.R, gc.G, gc.B))
                    {
                        Center         = new WpfPoint(0.5, 0.5),
                        GradientOrigin = new WpfPoint(0.5, 0.5),
                        RadiusX        = 1,
                        RadiusY        = 1,
                    };
                    glow.Freeze();
                    dc.DrawEllipse(glow, null, pt, radius, radius);
                    break;
                }
            }
        }

        var headBrush = new SolidColorBrush(dotColor);
        headBrush.Freeze();
        bool spinShape = settings.SpinEnabled && settings.DotShape != 0;
        if (spinShape)
        {
            double sa = DateTime.UtcNow.TimeOfDay.TotalSeconds * settings.SpinSpeed * (settings.SpinCW ? 1.0 : -1.0);
            dc.PushTransform(new RotateTransform(sa, pt.X, pt.Y));
        }
        switch (settings.DotShape)
        {
            case 1: dc.DrawGeometry(headBrush, null, MakeStarGeometry(pt, dotSize, dotSize * 0.4, 5));          break;
            case 2: dc.DrawGeometry(headBrush, null, MakeDiamondGeometry(pt, dotSize * 1.3));                   break;
            case 3: dc.DrawGeometry(headBrush, null, MakeCrosshairGeometry(pt, dotSize * 1.4, dotSize * 0.4));  break;
            default: dc.DrawEllipse(headBrush, null, pt, dotSize, dotSize);                                     break;
        }
        if (spinShape) dc.Pop();
    }

    private static Geometry MakeStarGeometry(WpfPoint center, double outerR, double innerR, int points)
    {
        var g = new StreamGeometry();
        using var ctx = g.Open();
        for (int i = 0; i < points * 2; i++)
        {
            double angle = Math.PI * i / points - Math.PI / 2.0;
            double r     = (i % 2 == 0) ? outerR : innerR;
            var    p     = new WpfPoint(center.X + r * Math.Cos(angle), center.Y + r * Math.Sin(angle));
            if (i == 0) ctx.BeginFigure(p, isFilled: true, isClosed: true);
            else        ctx.LineTo(p, isStroked: false, isSmoothJoin: false);
        }
        g.Freeze();
        return g;
    }

    private static Geometry MakeDiamondGeometry(WpfPoint center, double r)
    {
        var g = new StreamGeometry();
        using var ctx = g.Open();
        ctx.BeginFigure(new WpfPoint(center.X,           center.Y - r),     isFilled: true, isClosed: true);
        ctx.LineTo(     new WpfPoint(center.X + r * 0.6, center.Y),         isStroked: false, isSmoothJoin: false);
        ctx.LineTo(     new WpfPoint(center.X,           center.Y + r),     isStroked: false, isSmoothJoin: false);
        ctx.LineTo(     new WpfPoint(center.X - r * 0.6, center.Y),         isStroked: false, isSmoothJoin: false);
        g.Freeze();
        return g;
    }

    private static Geometry MakeCrosshairGeometry(WpfPoint center, double armLen, double armWidth)
    {
        double hw = armWidth * 0.5;
        var g = new StreamGeometry();
        using var ctx = g.Open();
        ctx.BeginFigure(new WpfPoint(center.X - armLen, center.Y - hw),   isFilled: true, isClosed: true);
        ctx.LineTo(     new WpfPoint(center.X + armLen, center.Y - hw),   isStroked: false, isSmoothJoin: false);
        ctx.LineTo(     new WpfPoint(center.X + armLen, center.Y + hw),   isStroked: false, isSmoothJoin: false);
        ctx.LineTo(     new WpfPoint(center.X - armLen, center.Y + hw),   isStroked: false, isSmoothJoin: false);
        ctx.BeginFigure(new WpfPoint(center.X - hw,   center.Y - armLen), isFilled: true, isClosed: true);
        ctx.LineTo(     new WpfPoint(center.X + hw,   center.Y - armLen), isStroked: false, isSmoothJoin: false);
        ctx.LineTo(     new WpfPoint(center.X + hw,   center.Y + armLen), isStroked: false, isSmoothJoin: false);
        ctx.LineTo(     new WpfPoint(center.X - hw,   center.Y + armLen), isStroked: false, isSmoothJoin: false);
        g.Freeze();
        return g;
    }
    // ── Click effects ─────────────────────────────────────────────────────────

    public void AddClickEffect(WpfPoint localPosition, int button = 0)
    {
        // button: 0=left, 1=right, 2=middle, 3=double-click
        var (enabled, style, _, _, _) = GetClickParams(button);
        if (!enabled) return;
        var sparks = style == 4 ? GenerateSparks() : null;
        _clickEffects.Add(new ClickEffect(localPosition, DateTime.UtcNow, button, sparks));
        InvalidateVisual();
    }

    public void TriggerClickSwap()
    {
        if (!settings.ClickSwapEnabled || _clickSwapImage is null) return;
        _clickSwapUntil = DateTime.UtcNow.AddMilliseconds(settings.ClickSwapDurationMs);
        InvalidateVisual();
    }

    private (double Vx, double Vy)[] GenerateSparks()
    {
        var count     = Math.Max(1, settings.SparkCount);
        var speed     = settings.SparkInitialSpeed;
        var spreadRad = settings.SparkSpreadDeg * Math.PI / 180.0;
        var rng       = new Random();
        var result    = new (double Vx, double Vy)[count];
        for (int i = 0; i < count; i++)
        {
            // Full circle: evenly distributed + jitter. Partial spread: fan centred upward.
            double baseAng = (settings.SparkSpreadDeg >= 359.0) ? 0.0 : -Math.PI / 2.0;
            double ang     = baseAng + (rng.NextDouble() - 0.5) * spreadRad;
            double s       = speed * (0.6 + rng.NextDouble() * 0.8);
            result[i]      = (s * Math.Cos(ang), s * Math.Sin(ang));
        }
        return result;
    }

    private void DrawPersistentStrokes(DrawingContext dc)
    {
        if (_drawnStrokes.Count == 0 && (_currentStroke == null || _currentStroke.Count < 2)) return;

        var tailColor = ColorHelper.ParseColor(settings.TailColor);
        var penWidth  = Math.Max(0.5, settings.TailThickness);
        var brush     = new SolidColorBrush(tailColor);
        brush.Freeze();
        var pen = new System.Windows.Media.Pen(brush, penWidth)
            { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        pen.Freeze();

        foreach (var stroke in _drawnStrokes)
        {
            if (stroke.Count < 2) continue;
            for (int i = 1; i < stroke.Count; i++)
                dc.DrawLine(pen, stroke[i - 1], stroke[i]);
        }

        if (_currentStroke != null && _currentStroke.Count >= 2)
        {
            for (int i = 1; i < _currentStroke.Count; i++)
                dc.DrawLine(pen, _currentStroke[i - 1], _currentStroke[i]);
        }
    }

    private void DrawClickEffects(DrawingContext dc)
    {
        if (_clickEffects.Count == 0) return;
        var now = DateTime.UtcNow;
        _clickEffects.RemoveAll(e =>
        {
            var (_, _, _, _, dur) = GetClickParams(e.Button);
            return (now - e.SpawnTime).TotalMilliseconds >= dur;
        });
        if (_clickEffects.Count == 0) return;

        foreach (var effect in _clickEffects)
        {
            var (enabled, style, bc, size, durMs) = GetClickParams(effect.Button);
            if (!enabled) continue;
            double t      = Math.Clamp((now - effect.SpawnTime).TotalMilliseconds / durMs, 0, 1);
            double alpha  = 1.0 - t;
            double radius = size * t;
            if (radius < 0.5) continue;

            switch (style)
            {
                case 0: // Ripple — thin expanding ring
                {
                    var c   = MediaColor.FromArgb((byte)(bc.A * alpha), bc.R, bc.G, bc.B);
                    var br  = new SolidColorBrush(c); br.Freeze();
                    var pen = new System.Windows.Media.Pen(br, Math.Max(0.5, 3.0 * (1 - t)));
                    pen.Freeze();
                    dc.DrawEllipse(null, pen, effect.Position, radius, radius);
                    break;
                }
                case 1: // Burst — 8 dots radiating out
                {
                    var c   = MediaColor.FromArgb((byte)(bc.A * alpha), bc.R, bc.G, bc.B);
                    var br  = new SolidColorBrush(c); br.Freeze();
                    double dotR = Math.Max(0.5, 3.5 * (1 - t));
                    for (int k = 0; k < 8; k++)
                    {
                        double ang = k * Math.PI / 4.0;
                        var pt = new WpfPoint(effect.Position.X + radius * Math.Cos(ang),
                                              effect.Position.Y + radius * Math.Sin(ang));
                        dc.DrawEllipse(br, null, pt, dotR, dotR);
                    }
                    break;
                }
                case 2: // Shockwave — thick expanding ring
                {
                    double thickness = Math.Max(0.5, 8.0 * (1 - t));
                    var c   = MediaColor.FromArgb((byte)(bc.A * alpha * 0.8), bc.R, bc.G, bc.B);
                    var br  = new SolidColorBrush(c); br.Freeze();
                    var pen = new System.Windows.Media.Pen(br, thickness);
                    pen.Freeze();
                    dc.DrawEllipse(null, pen, effect.Position, radius, radius);
                    break;
                }
                case 3: // Image Burst — images (or dots) radiating outward in a circle
                {
                    if (radius < 0.5) break;
                    var count = Math.Max(1, settings.SparkCount);
                    double pSize = settings.SparkParticleSize * Math.Max(0.2, 1.0 - t * 0.6);
                    for (int k = 0; k < count; k++)
                    {
                        double ang = k * 2.0 * Math.PI / count;
                        var pt = new WpfPoint(effect.Position.X + radius * Math.Cos(ang),
                                              effect.Position.Y + radius * Math.Sin(ang));
                        if (_clickParticleImage is not null && pSize >= 0.5)
                        {
                            double aspect = _clickParticleImage.PixelWidth > 0 && _clickParticleImage.PixelHeight > 0
                                            ? (double)_clickParticleImage.PixelWidth / _clickParticleImage.PixelHeight : 1.0;
                            double hw = pSize * aspect; double hh = pSize;
                            dc.PushOpacity(alpha);
                            double rotDeg = ang * 180.0 / Math.PI;
                            dc.PushTransform(new RotateTransform(rotDeg, pt.X, pt.Y));
                            dc.DrawImage(_clickParticleImage, new Rect(pt.X - hw, pt.Y - hh, hw * 2, hh * 2));
                            dc.Pop(); dc.Pop();
                        }
                        else if (pSize >= 0.5)
                        {
                            var c  = MediaColor.FromArgb((byte)(bc.A * alpha), bc.R, bc.G, bc.B);
                            var br = new SolidColorBrush(c); br.Freeze();
                            dc.DrawEllipse(br, null, pt, pSize, pSize);
                        }
                    }
                    break;
                }
                case 4: // Sparks — physics-based particles with gravity that sizzle downward
                {
                    if (effect.Sparks is null) break;
                    double secs    = (now - effect.SpawnTime).TotalSeconds;
                    double gravity = settings.SparkGravity;
                    double pSize   = settings.SparkParticleSize;
                    for (int k = 0; k < effect.Sparks.Length; k++)
                    {
                        var (vx, vy) = effect.Sparks[k];
                        double px  = effect.Position.X + vx * secs;
                        double py  = effect.Position.Y + vy * secs + 0.5 * gravity * secs * secs;
                        double sz  = pSize * Math.Max(0.2, 1.0 - t * 0.6);
                        if (sz < 0.5) continue;
                        if (_clickParticleImage is not null)
                        {
                            double aspect = _clickParticleImage.PixelWidth > 0 && _clickParticleImage.PixelHeight > 0
                                            ? (double)_clickParticleImage.PixelWidth / _clickParticleImage.PixelHeight : 1.0;
                            double hw = sz * aspect; double hh = sz;
                            dc.PushOpacity(alpha);
                            dc.DrawImage(_clickParticleImage, new Rect(px - hw, py - hh, hw * 2, hh * 2));
                            dc.Pop();
                        }
                        else
                        {
                            var c  = MediaColor.FromArgb((byte)(bc.A * alpha), bc.R, bc.G, bc.B);
                            var br = new SolidColorBrush(c); br.Freeze();
                            dc.DrawEllipse(br, null, new WpfPoint(px, py), sz, sz);
                        }
                    }
                    break;
                }
            }
        }
    }
    private (bool enabled, int style, MediaColor color, double size, double durMs) GetClickParams(int button) => button switch
    {
        1 => (settings.RightClickEnabled,  settings.RightClickStyle,  ColorHelper.ParseColor(settings.RightClickColor),  settings.RightClickSize,  settings.RightClickDuration),
        2 => (settings.MiddleClickEnabled, settings.MiddleClickStyle, ColorHelper.ParseColor(settings.MiddleClickColor), settings.MiddleClickSize, settings.MiddleClickDuration),
        3 => (settings.DoubleClickEnabled, settings.ClickEffectStyle, ColorHelper.ParseColor(settings.ClickEffectColor), settings.ClickEffectSize * settings.DoubleClickMultiplier, settings.ClickEffectDuration),
        _ => (settings.ClickEffectEnabled, settings.ClickEffectStyle, ColorHelper.ParseColor(settings.ClickEffectColor), settings.ClickEffectSize, settings.ClickEffectDuration),
    };

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

    private void LoadClickParticleImage(string path)
    {
        _clickParticleImage      = null;
        _loadedParticleImagePath = path;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try
        {
            var decoder = BitmapDecoder.Create(new Uri(path, UriKind.Absolute),
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count > 0)
            {
                var stripped = StripWhiteBackground(decoder.Frames[0]);
                _clickParticleImage = stripped;
            }
        }
        catch { /* ignore unreadable image files */ }
    }

    private void LoadClickSwapImage(string path)
    {
        _clickSwapImage      = null;
        _loadedClickSwapPath = path;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try
        {
            var decoder = BitmapDecoder.Create(new Uri(path, UriKind.Absolute),
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count > 0)
            {
                var stripped = StripWhiteBackground(decoder.Frames[0]);
                _clickSwapImage = stripped;
            }
        }
        catch { /* ignore unreadable image files */ }
    }

    /// <summary>
    /// Converts near-white pixels to transparent so images with white backgrounds
    /// can be used as particles without a visible box around them.
    /// Pixels whose minimum channel value is >= <paramref name="threshold"/> are faded
    /// out proportionally (soft edge-preserving removal).
    /// </summary>
    private static BitmapSource StripWhiteBackground(BitmapSource source, byte threshold = 220)
    {
        var conv   = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int w      = conv.PixelWidth;
        int h      = conv.PixelHeight;
        int stride = w * 4;
        var px     = new byte[h * stride];
        conv.CopyPixels(px, stride, 0);

        for (int i = 0; i < px.Length; i += 4)
        {
            byte b = px[i], g = px[i + 1], r = px[i + 2], a = px[i + 3];
            if (a == 0) continue;                            // already transparent
            int minCh = Math.Min(r, Math.Min(g, b));
            if (minCh >= threshold)
            {
                // Linearly fade: at threshold → fully opaque, at 255 → fully transparent
                double t = (minCh - threshold) / (double)(255 - threshold);
                px[i + 3] = (byte)(a * Math.Max(0.0, 1.0 - t));
            }
        }

        var result = BitmapSource.Create(w, h, source.DpiX, source.DpiY,
            PixelFormats.Bgra32, null, px, stride);
        result.Freeze();
        return result;
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
        var isWave = settings.WaveEnabled;
        var smooth = Math.Clamp(settings.TrailSmoothness, 0.25, 3.0);
        // Wave mode: points are already 8 px apart with the wave shape baked in,
        // so heavy Catmull-Rom expansion is wasteful — 2–3 subs is plenty.
        var lenDiv = butter ? 1.9 : (isWave ? 8.0 : 2.4);
        var angDiv = butter ? 5.5 : 8.0;
        var minSub = Math.Max(1,  (int)Math.Round((butter ? 10 : (isWave ? 2 : 6))  * smooth));
        var maxSub = Math.Max(2,  (int)Math.Round((butter ? 56 : (isWave ? 14 : 32)) * smooth));

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
        // Hard cap: prevent runaway point growth during very fast movement
        const int MaxPoints = 400;
        if (points.Count > MaxPoints)
            points.RemoveRange(0, points.Count - MaxPoints);
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
    private readonly record struct ClickEffect(WpfPoint Position, DateTime SpawnTime, int Button, (double Vx, double Vy)[]? Sparks);
}
