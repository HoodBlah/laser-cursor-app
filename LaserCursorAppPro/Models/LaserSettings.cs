namespace LaserCursorAppPro.Models;

public class LaserSettings
{
    // ── Dot ─────────────────────────────────────────────────────────────────
    public string DotColor { get; set; } = "#FFFF1E1E";
    public double DotSize  { get; set; } = 3.5;
    public int    DotShape { get; set; } = 0;   // 0=Circle 1=Star 2=Diamond 3=Crosshair

    // ── Dot Glow ─────────────────────────────────────────────────────────────
    public bool   DotGlowEnabled { get; set; } = false;
    public string DotGlowColor   { get; set; } = "#80FF1E1E";
    public double DotGlowRadius  { get; set; } = 14.0;
    public int    DotGlowBloom   { get; set; } = 0;   // 0=Soft 1=Hard 2=Pulse

    // ── Tail ─────────────────────────────────────────────────────────────────
    public string TailColor           { get; set; } = "#FAFF1C1C";
    public bool   TailGradientEnabled { get; set; } = false;
    public string TailGradientEndColor{ get; set; } = "#FAFF6400";
    public double TailLengthMs        { get; set; } = 380.0;
    public double TailThickness       { get; set; } = 8.0;
    public double TailTaperPower      { get; set; } = 0.8;

    // ── Tail Glow ─────────────────────────────────────────────────────────────
    public bool   TailGlowEnabled { get; set; } = false;
    public string TailGlowColor   { get; set; } = "#59FF1E1E";
    public double TailGlowWidth   { get; set; } = 8.0;

    // ── Smoothness ────────────────────────────────────────────────────────────
    public bool   ButterModeEnabled { get; set; } = false;
    public int    TailFadeStyle     { get; set; } = 1;    // 0=Alpha only  1=Thin-and-fade  2=Glow-dissolve
    public double TrailSmoothness   { get; set; } = 1.0;  // 0.25–3.0 Catmull-Rom subdivision multiplier

    // ── Dot custom image ─────────────────────────────────────────────────────
    public bool   DotUseCustomImage { get; set; } = false;
    public string DotImagePath      { get; set; } = "";

    // ── Trail rainbow ─────────────────────────────────────────────────────────
    public bool TailRainbowMode     { get; set; } = false;

    // ── Trail custom image ─────────────────────────────────────────────────────
    public bool   TailImageMode { get; set; } = false;
    public string TailImagePath { get; set; } = "";

    // ── Trail wave ────────────────────────────────────────────────────────────
    public bool   WaveEnabled   { get; set; } = false;
    public double WaveAmplitude { get; set; } = 12.0;  // pixels perpendicular offset
    public double WaveFrequency { get; set; } = 3.0;   // full cycles across trail length

    // ── Dot pulse ─────────────────────────────────────────────────────────────
    public bool   PulseEnabled   { get; set; } = false;
    public double PulseSpeed     { get; set; } = 1.5;   // Hz
    public double PulseIntensity { get; set; } = 0.35;  // fraction of DotSize

    // ── Auto-hide ─────────────────────────────────────────────────────────────
    public bool   AutoHideEnabled { get; set; } = false;
    public double AutoHideDelayMs { get; set; } = 3000.0;

    // ── Click effects ─────────────────────────────────────────────────────────
    public bool   ClickEffectEnabled  { get; set; } = false;
    public int    ClickEffectStyle    { get; set; } = 0;   // 0=Ripple  1=Burst(dots)  2=Shockwave  3=ImageBurst  4=Sparks
    public string ClickEffectColor    { get; set; } = "#CCFF4444";
    public double ClickEffectSize     { get; set; } = 48.0;
    public double ClickEffectDuration { get; set; } = 450.0;

    // ── Spark / image burst (styles 3 & 4) ────────────────────────────────────
    public string ClickParticleImagePath { get; set; } = "";
    public int    SparkCount             { get; set; } = 12;
    public double SparkGravity           { get; set; } = 500.0;  // px/s²
    public double SparkInitialSpeed      { get; set; } = 220.0;  // px/s
    public double SparkSpreadDeg         { get; set; } = 360.0;  // full circle
    public double SparkParticleSize      { get; set; } = 7.0;    // px (radius or half-height)

    // ── Click cursor swap ─────────────────────────────────────────────────────
    public bool   ClickSwapEnabled    { get; set; } = false;
    public string ClickSwapImagePath  { get; set; } = "";
    public double ClickSwapDurationMs { get; set; } = 400.0;
    public double ClickSwapSize       { get; set; } = 21.0;  // half-height in px

    // ── Dot spin ──────────────────────────────────────────────────────────────
    public bool   SpinEnabled { get; set; } = false;
    public double SpinSpeed   { get; set; } = 90.0;  // degrees/sec
    public bool   SpinCW      { get; set; } = true;

    // ── Trail patterns ────────────────────────────────────────────────────────
    public int    TrailPattern          { get; set; } = 0;   // 0=None 1=Fire 2=Electric 3=Smoke 4=Plasma
    public double TrailPatternSpeed     { get; set; } = 1.0;
    public double TrailPatternIntensity { get; set; } = 0.7;

    // ── Right-click effect ───────────────────────────────────────────────
    public bool   RightClickEnabled  { get; set; } = true;
    public int    RightClickStyle    { get; set; } = 1;    // 1=Burst default
    public string RightClickColor    { get; set; } = "#FF8800";
    public double RightClickSize     { get; set; } = 24.0;
    public double RightClickDuration { get; set; } = 400.0;

    // ── Middle-click effect ──────────────────────────────────────────────
    public bool   MiddleClickEnabled  { get; set; } = true;
    public int    MiddleClickStyle    { get; set; } = 2;    // 2=Shockwave default
    public string MiddleClickColor    { get; set; } = "#00BBFF";
    public double MiddleClickSize     { get; set; } = 20.0;
    public double MiddleClickDuration { get; set; } = 350.0;

    // ── Double-click effect ───────────────────────────────────────────────
    public bool   DoubleClickEnabled    { get; set; } = true;
    public double DoubleClickMultiplier { get; set; } = 1.8;
    public int    DoubleClickDetectMs   { get; set; } = 350;

    public LaserSettings Clone() => (LaserSettings)MemberwiseClone();
}
