namespace LaserCursorAppPro.Models;

public class LaserSettings
{
    // ── Dot ─────────────────────────────────────────────────────────────────
    public string DotColor { get; set; } = "#FFFF1E1E";
    public double DotSize  { get; set; } = 3.5;

    // ── Dot Glow ─────────────────────────────────────────────────────────────
    public bool   DotGlowEnabled { get; set; } = false;
    public string DotGlowColor   { get; set; } = "#80FF1E1E";
    public double DotGlowRadius  { get; set; } = 14.0;

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
    public bool ButterModeEnabled { get; set; } = false;

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

    public LaserSettings Clone() => (LaserSettings)MemberwiseClone();
}
