using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LaserCursorApp.Helpers;
using LaserCursorApp.Models;
using LaserCursorApp.Services;
using MediaColor  = System.Windows.Media.Color;
using MessageBox  = System.Windows.MessageBox;
using WpfPoint    = System.Windows.Point;
using WpfTextBox  = System.Windows.Controls.TextBox;
using WpfKey      = System.Windows.Input.Key;
using WpfKeyArgs  = System.Windows.Input.KeyEventArgs;

namespace LaserCursorApp.Options;

public partial class OptionsWindow : Window
{
    private LaserSettings         _settings;
    private LaserSettings         _baseline = new();  // reference preset for reset buttons
    private readonly Action<LaserSettings> _onChanged;
    private List<LaserProfile>    _profiles = new();
    private bool                  _loading = true;  // suppress events during InitializeComponent

    public OptionsWindow(LaserSettings settings, Action<LaserSettings> onChanged)
    {
        _settings  = settings;
        _onChanged = onChanged;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // ── Startup ───────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loading = true;
        LoadSettingsToUI(_settings);
        _loading = false;

        LoadProfiles();
        LoadBuiltInPresets();
        DrawPreview();
    }

    private void LoadSettingsToUI(LaserSettings s)
    {
        DotColorPicker.SelectedColor    = ColorHelper.ParseColor(s.DotColor);
        DotSizeSlider.Value             = s.DotSize;
        DotShapeCombo.SelectedIndex          = s.DotShape;
        DotGlowCheck.IsChecked               = s.DotGlowEnabled;
        DotGlowColorPicker.SelectedColor     = ColorHelper.ParseColor(s.DotGlowColor);
        DotGlowRadiusSlider.Value            = s.DotGlowRadius;
        DotGlowBloomCombo.SelectedIndex      = s.DotGlowBloom;
        DotGlowPanel.IsEnabled               = s.DotGlowEnabled;

        DotImageCheck.IsChecked           = s.DotUseCustomImage;
        DotImagePanel.IsEnabled           = s.DotUseCustomImage;
        DotImagePathLabel.Text            = string.IsNullOrEmpty(s.DotImagePath)
                                            ? "No file selected"
                                            : Path.GetFileName(s.DotImagePath);
        DotImagePreviewBorder.Visibility  = Visibility.Collapsed;
        DotImagePreview.Source            = null;
        if (!string.IsNullOrEmpty(s.DotImagePath) && File.Exists(s.DotImagePath))
            SetDotImagePreview(s.DotImagePath);

        TailColorPicker.SelectedColor    = ColorHelper.ParseColor(s.TailColor);
        TailRainbowCheck.IsChecked       = s.TailRainbowMode;
        TailGradCheck.IsChecked          = s.TailGradientEnabled;
        TailGradColorPicker.SelectedColor= ColorHelper.ParseColor(s.TailGradientEndColor);
        TailGradColorPicker.IsEnabled    = s.TailGradientEnabled;

        TailImageCheck.IsChecked           = s.TailImageMode;
        TailImagePanel.IsEnabled           = s.TailImageMode;
        TailImagePathLabel.Text            = string.IsNullOrEmpty(s.TailImagePath)
                                             ? "No file selected"
                                             : Path.GetFileName(s.TailImagePath);
        TailImagePreviewBorder.Visibility  = Visibility.Collapsed;
        TailImagePreview.Source            = null;
        if (!string.IsNullOrEmpty(s.TailImagePath) && File.Exists(s.TailImagePath))
            SetTailImagePreview(s.TailImagePath);
        TailLengthSlider.Value           = s.TailLengthMs;
        TailThicknessSlider.Value        = s.TailThickness;
        TailTaperSlider.Value            = s.TailTaperPower;
        TailFadeStyleCombo.SelectedIndex  = s.TailFadeStyle;
        TrailSmoothnessSlider.Value       = s.TrailSmoothness;
        ButterCheck.IsChecked             = s.ButterModeEnabled;

        TailGlowCheck.IsChecked          = s.TailGlowEnabled;
        TailGlowColorPicker.SelectedColor= ColorHelper.ParseColor(s.TailGlowColor);
        TailGlowWidthSlider.Value        = s.TailGlowWidth;
        TailGlowPanel.IsEnabled          = s.TailGlowEnabled;

        WaveCheck.IsChecked  = s.WaveEnabled;
        WavePanel.IsEnabled  = s.WaveEnabled;
        WaveAmpSlider.Value  = s.WaveAmplitude;
        WaveFreqSlider.Value = s.WaveFrequency;

        PulseCheck.IsChecked       = s.PulseEnabled;
        PulsePanel.IsEnabled       = s.PulseEnabled;
        PulseSpeedSlider.Value     = s.PulseSpeed;
        PulseIntensitySlider.Value = s.PulseIntensity;

        SpinCheck.IsChecked        = s.SpinEnabled;
        SpinPanel.IsEnabled        = s.SpinEnabled;
        SpinSpeedSlider.Value      = s.SpinSpeed;
        SpinDirCombo.SelectedIndex = s.SpinCW ? 0 : 1;

        TrailPatternCombo.SelectedIndex   = s.TrailPattern;
        TrailPatternPanel.IsEnabled       = s.TrailPattern > 0;
        TrailPatternSpeedSlider.Value     = s.TrailPatternSpeed;
        TrailPatternIntensitySlider.Value = s.TrailPatternIntensity;

        DrawModeCheck.IsChecked         = s.DrawModeEnabled;
        DrawModePanel.IsEnabled         = s.DrawModeEnabled;
        DrawModeKeyCombo.SelectedIndex  = VKeyToComboIndex(s.DrawModeVKey);
        DrawModeClearMsSlider.Value     = s.DrawModeClearDoubleTapMs;

        ClickEffectCheck.IsChecked           = s.ClickEffectEnabled;
        ClickEffectPanel.IsEnabled           = s.ClickEffectEnabled;
        ClickEffectStyleCombo.SelectedIndex  = s.ClickEffectStyle;
        ClickEffectColorPicker.SelectedColor = ColorHelper.ParseColor(s.ClickEffectColor);
        ClickEffectSizeSlider.Value          = s.ClickEffectSize;
        ClickEffectDurSlider.Value           = s.ClickEffectDuration;

        ParticleImageLabel.Text       = string.IsNullOrEmpty(s.ClickParticleImagePath)
                                        ? "None (use colored dots)" : Path.GetFileName(s.ClickParticleImagePath);
        SparkCountSlider.Value        = s.SparkCount;
        SparkParticleSizeSlider.Value = s.SparkParticleSize;
        SparkSpreadSlider.Value       = s.SparkSpreadDeg;
        SparkSpeedSlider.Value        = s.SparkInitialSpeed;
        SparkGravitySlider.Value      = s.SparkGravity;

        RightClickCheck.IsChecked             = s.RightClickEnabled;
        RightClickPanel.IsEnabled             = s.RightClickEnabled;
        RightClickStyleCombo.SelectedIndex    = s.RightClickStyle;
        RightClickColorPicker.SelectedColor   = ColorHelper.ParseColor(s.RightClickColor);
        RightClickSizeSlider.Value            = s.RightClickSize;
        RightClickDurSlider.Value             = s.RightClickDuration;

        MiddleClickCheck.IsChecked            = s.MiddleClickEnabled;
        MiddleClickPanel.IsEnabled            = s.MiddleClickEnabled;
        MiddleClickStyleCombo.SelectedIndex   = s.MiddleClickStyle;
        MiddleClickColorPicker.SelectedColor  = ColorHelper.ParseColor(s.MiddleClickColor);
        MiddleClickSizeSlider.Value           = s.MiddleClickSize;
        MiddleClickDurSlider.Value            = s.MiddleClickDuration;

        DoubleClickCheck.IsChecked            = s.DoubleClickEnabled;
        DoubleClickPanel.IsEnabled            = s.DoubleClickEnabled;
        DoubleClickMultSlider.Value           = s.DoubleClickMultiplier;
        DoubleClickMsSlider.Value             = s.DoubleClickDetectMs;

        ClickSwapCheck.IsChecked    = s.ClickSwapEnabled;
        ClickSwapPanel.IsEnabled    = s.ClickSwapEnabled;
        ClickSwapImageLabel.Text    = string.IsNullOrEmpty(s.ClickSwapImagePath)
                                      ? "No file selected" : Path.GetFileName(s.ClickSwapImagePath);
        ClickSwapDurSlider.Value    = s.ClickSwapDurationMs;

        AutoHideCheck.IsChecked   = s.AutoHideEnabled;
        AutoHidePanel.IsEnabled   = s.AutoHideEnabled;
        AutoHideDelaySlider.Value = s.AutoHideDelayMs;

        UpdateLabels(s);
    }

    private void UpdateLabels(LaserSettings s)
    {
        DotSizeBox.Text        = $"{s.DotSize:F1}";
        DotGlowRadiusBox.Text  = $"{s.DotGlowRadius:F0}";
        TailLengthBox.Text     = $"{s.TailLengthMs:F0}";
        TailThicknessBox.Text  = $"{s.TailThickness:F1}";
        TailTaperBox.Text        = $"{s.TailTaperPower:F2}";
        TrailSmoothnessBox.Text  = $"{s.TrailSmoothness:F2}";
        TailGlowWidthBox.Text  = $"{s.TailGlowWidth:F1}";
        WaveAmpBox.Text          = $"{s.WaveAmplitude:F0}";
        WaveFreqBox.Text         = $"{s.WaveFrequency:F1}";
        PulseSpeedBox.Text       = $"{s.PulseSpeed:F1}";
        PulseIntensityBox.Text   = $"{s.PulseIntensity:F2}";
        SpinSpeedBox.Text              = $"{s.SpinSpeed:F0}";
        TrailPatternSpeedBox.Text      = $"{s.TrailPatternSpeed:F1}";
        TrailPatternIntensityBox.Text  = $"{s.TrailPatternIntensity:F2}";
        ClickEffectSizeBox.Text   = $"{s.ClickEffectSize:F0}";
        ClickEffectDurBox.Text    = $"{s.ClickEffectDuration:F0}";
        SparkCountBox.Text        = $"{s.SparkCount}";
        SparkParticleSizeBox.Text = $"{s.SparkParticleSize:F1}";
        SparkSpreadBox.Text       = $"{s.SparkSpreadDeg:F0}";
        SparkSpeedBox.Text        = $"{s.SparkInitialSpeed:F0}";
        SparkGravityBox.Text      = $"{s.SparkGravity:F0}";
        RightClickSizeBox.Text    = $"{s.RightClickSize:F0}";
        RightClickDurBox.Text     = $"{s.RightClickDuration:F0}";
        MiddleClickSizeBox.Text   = $"{s.MiddleClickSize:F0}";
        MiddleClickDurBox.Text    = $"{s.MiddleClickDuration:F0}";
        DoubleClickMultBox.Text   = $"{s.DoubleClickMultiplier:F1}";
        DoubleClickMsBox.Text     = $"{s.DoubleClickDetectMs:F0}";
        ClickSwapDurBox.Text      = $"{s.ClickSwapDurationMs:F0}";
        ClickSwapSizeSlider.Value = s.ClickSwapSize;
        ClickSwapSizeBox.Text     = $"{s.ClickSwapSize:F0}";
        AutoHideDelayBox.Text     = $"{s.AutoHideDelayMs:F0}";
        DrawModeClearMsBox.Text   = $"{s.DrawModeClearDoubleTapMs:F0}";
    }

    // ── Notify helper ─────────────────────────────────────────────────────────

    private void Notify()
    {
        if (_loading) return;
        DrawPreview();
        _onChanged(_settings);
    }

    // ── Dot handlers ──────────────────────────────────────────────────────────

    private void DotColor_Changed(object? sender, EventArgs e)
    {
        if (_loading) return;
        _settings.DotColor = ColorHelper.ToHex(DotColorPicker.SelectedColor);
        Notify();
    }

    private void DotSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.DotSize   = DotSizeSlider.Value;
        DotSizeBox.Text      = $"{_settings.DotSize:F1}";
        Notify();
    }

    private void DotGlow_Toggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.DotGlowEnabled  = DotGlowCheck.IsChecked == true;
        DotGlowPanel.IsEnabled    = _settings.DotGlowEnabled;
        Notify();
    }

    private void DotGlowColor_Changed(object? sender, EventArgs e)
    {
        if (_loading) return;
        _settings.DotGlowColor = ColorHelper.ToHex(DotGlowColorPicker.SelectedColor);
        Notify();
    }

    private void DotGlowRadius_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.DotGlowRadius  = DotGlowRadiusSlider.Value;
        DotGlowRadiusBox.Text    = $"{_settings.DotGlowRadius:F0}";
        Notify();
    }

    private void DotImage_Toggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.DotUseCustomImage = DotImageCheck.IsChecked == true;
        DotImagePanel.IsEnabled     = _settings.DotUseCustomImage;
        Notify();
    }

    private void DotImage_Browse(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select Dot Image",
            Filter = "Image Files (*.png;*.jpg;*.bmp;*.gif)|*.png;*.jpg;*.bmp;*.gif"
        };
        if (dlg.ShowDialog() != true) return;
        _settings.DotImagePath   = dlg.FileName;
        DotImagePathLabel.Text   = Path.GetFileName(dlg.FileName);
        SetDotImagePreview(dlg.FileName);
        Notify();
    }

    private void DotImage_Clear(object sender, RoutedEventArgs e)
    {
        _settings.DotImagePath           = "";
        DotImagePathLabel.Text           = "No file selected";
        DotImagePreviewBorder.Visibility = Visibility.Collapsed;
        DotImagePreview.Source           = null;
        Notify();
    }

    private void SetDotImagePreview(string path)
    {
        try
        {
            var bi = new System.Windows.Media.Imaging.BitmapImage();
            bi.BeginInit();
            bi.UriSource    = new Uri(path);
            bi.CacheOption  = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bi.CreateOptions= System.Windows.Media.Imaging.BitmapCreateOptions.None;
            bi.EndInit();
            bi.Freeze();
            DotImagePreview.Source           = bi;
            DotImagePreviewBorder.Visibility = Visibility.Visible;
        }
        catch { /* ignore unreadable files */ }
    }

    // ── Trail handlers ────────────────────────────────────────────────────────

    private void TailColor_Changed(object? sender, EventArgs e)
    {
        if (_loading) return;
        _settings.TailColor = ColorHelper.ToHex(TailColorPicker.SelectedColor);
        Notify();
    }
    private void TailRainbow_Toggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.TailRainbowMode = TailRainbowCheck.IsChecked == true;
        Notify();
    }
    private void TailGrad_Toggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.TailGradientEnabled = TailGradCheck.IsChecked == true;
        TailGradColorPicker.IsEnabled = _settings.TailGradientEnabled;
        Notify();
    }

    private void TailGradColor_Changed(object? sender, EventArgs e)
    {
        if (_loading) return;
        _settings.TailGradientEndColor = ColorHelper.ToHex(TailGradColorPicker.SelectedColor);
        Notify();
    }

    private void TailLength_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.TailLengthMs  = TailLengthSlider.Value;
        TailLengthBox.Text      = $"{_settings.TailLengthMs:F0}";
        Notify();
    }

    private void TailThickness_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.TailThickness   = TailThicknessSlider.Value;
        TailThicknessBox.Text     = $"{_settings.TailThickness:F1}";
        Notify();
    }

    private void TailTaper_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.TailTaperPower = TailTaperSlider.Value;
        TailTaperBox.Text        = $"{_settings.TailTaperPower:F2}";
        Notify();
    }

    private void TailFadeStyle_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.TailFadeStyle = TailFadeStyleCombo.SelectedIndex;
        Notify();
    }

    private void TrailSmoothness_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.TrailSmoothness  = TrailSmoothnessSlider.Value;
        TrailSmoothnessBox.Text    = $"{_settings.TrailSmoothness:F2}";
        Notify();
    }

    private void Butter_Toggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.ButterModeEnabled = ButterCheck.IsChecked == true;
        Notify();
    }

    private void TailGlow_Toggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.TailGlowEnabled = TailGlowCheck.IsChecked == true;
        TailGlowPanel.IsEnabled   = _settings.TailGlowEnabled;
        Notify();
    }

    private void TailGlowColor_Changed(object? sender, EventArgs e)
    {
        if (_loading) return;
        _settings.TailGlowColor = ColorHelper.ToHex(TailGlowColorPicker.SelectedColor);
        Notify();
    }

    private void TailGlowWidth_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.TailGlowWidth  = TailGlowWidthSlider.Value;
        TailGlowWidthBox.Text    = $"{_settings.TailGlowWidth:F1}";
        Notify();
    }

    // ── Per-setting reset handlers ────────────────────────────────────────────

    private void ResetDotColor(object s, RoutedEventArgs e)
    {
        _loading = true;
        DotColorPicker.SelectedColor = ColorHelper.ParseColor(_baseline.DotColor);
        _loading = false;
        _settings.DotColor = _baseline.DotColor;
        Notify();
    }

    private void ResetDotSize(object s, RoutedEventArgs e)
    {
        _loading = true; DotSizeSlider.Value = _baseline.DotSize; _loading = false;
        _settings.DotSize = _baseline.DotSize;
        DotSizeBox.Text   = $"{_baseline.DotSize:F1}";
        Notify();
    }

    private void ResetDotGlowEnabled(object s, RoutedEventArgs e)
    {
        _loading = true; DotGlowCheck.IsChecked = _baseline.DotGlowEnabled; _loading = false;
        _settings.DotGlowEnabled = _baseline.DotGlowEnabled;
        DotGlowPanel.IsEnabled   = _baseline.DotGlowEnabled;
        Notify();
    }

    private void ResetDotGlowColor(object s, RoutedEventArgs e)
    {
        _loading = true;
        DotGlowColorPicker.SelectedColor = ColorHelper.ParseColor(_baseline.DotGlowColor);
        _loading = false;
        _settings.DotGlowColor = _baseline.DotGlowColor;
        Notify();
    }

    private void ResetDotGlowRadius(object s, RoutedEventArgs e)
    {
        _loading = true; DotGlowRadiusSlider.Value = _baseline.DotGlowRadius; _loading = false;
        _settings.DotGlowRadius  = _baseline.DotGlowRadius;
        DotGlowRadiusBox.Text    = $"{_baseline.DotGlowRadius:F1}";
        Notify();
    }

    private void DotShape_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.DotShape = DotShapeCombo.SelectedIndex;
        Notify();
    }

    private void ResetDotShape(object s, RoutedEventArgs e)
    {
        _loading = true; DotShapeCombo.SelectedIndex = _baseline.DotShape; _loading = false;
        _settings.DotShape = _baseline.DotShape;
        Notify();
    }

    private void DotGlowBloom_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.DotGlowBloom = DotGlowBloomCombo.SelectedIndex;
        Notify();
    }

    private void ResetDotGlowBloom(object s, RoutedEventArgs e)
    {
        _loading = true; DotGlowBloomCombo.SelectedIndex = _baseline.DotGlowBloom; _loading = false;
        _settings.DotGlowBloom = _baseline.DotGlowBloom;
        Notify();
    }

    private void ResetDotImage(object s, RoutedEventArgs e)
    {
        _loading = true;
        DotImageCheck.IsChecked = _baseline.DotUseCustomImage;
        DotImagePanel.IsEnabled = _baseline.DotUseCustomImage;
        _loading = false;
        _settings.DotUseCustomImage      = _baseline.DotUseCustomImage;
        _settings.DotImagePath           = _baseline.DotImagePath;
        DotImagePathLabel.Text           = string.IsNullOrEmpty(_baseline.DotImagePath)
                                           ? "No file selected"
                                           : Path.GetFileName(_baseline.DotImagePath);
        DotImagePreviewBorder.Visibility = Visibility.Collapsed;
        DotImagePreview.Source           = null;
        if (!string.IsNullOrEmpty(_baseline.DotImagePath) && File.Exists(_baseline.DotImagePath))
            SetDotImagePreview(_baseline.DotImagePath);
        Notify();
    }

    private void ResetTailColor(object s, RoutedEventArgs e)
    {
        _loading = true;
        TailColorPicker.SelectedColor = ColorHelper.ParseColor(_baseline.TailColor);
        _loading = false;
        _settings.TailColor = _baseline.TailColor;
        Notify();
    }

    private void ResetTailRainbow(object s, RoutedEventArgs e)
    {
        _loading = true; TailRainbowCheck.IsChecked = _baseline.TailRainbowMode; _loading = false;
        _settings.TailRainbowMode = _baseline.TailRainbowMode;
        Notify();
    }

    private void TailImage_Toggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.TailImageMode = TailImageCheck.IsChecked == true;
        TailImagePanel.IsEnabled = _settings.TailImageMode;
        Notify();
    }

    private void TailImage_Browse(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select Trail Image",
            Filter = "Image Files (*.png;*.jpg;*.bmp;*.gif)|*.png;*.jpg;*.bmp;*.gif"
        };
        if (dlg.ShowDialog() != true) return;
        _settings.TailImagePath  = dlg.FileName;
        TailImagePathLabel.Text  = Path.GetFileName(dlg.FileName);
        SetTailImagePreview(dlg.FileName);
        Notify();
    }

    private void TailImage_Clear(object sender, RoutedEventArgs e)
    {
        _settings.TailImagePath            = "";
        TailImagePathLabel.Text            = "No file selected";
        TailImagePreviewBorder.Visibility  = Visibility.Collapsed;
        TailImagePreview.Source            = null;
        Notify();
    }

    private void SetTailImagePreview(string path)
    {
        try
        {
            var bi = new System.Windows.Media.Imaging.BitmapImage();
            bi.BeginInit();
            bi.UriSource     = new Uri(path);
            bi.CacheOption   = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bi.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.None;
            bi.EndInit();
            bi.Freeze();
            TailImagePreview.Source           = bi;
            TailImagePreviewBorder.Visibility = Visibility.Visible;
        }
        catch { }
    }

    private void ResetTailImage(object s, RoutedEventArgs e)
    {
        _loading = true;
        TailImageCheck.IsChecked = _baseline.TailImageMode;
        TailImagePanel.IsEnabled = _baseline.TailImageMode;
        _loading = false;
        _settings.TailImageMode           = _baseline.TailImageMode;
        _settings.TailImagePath           = _baseline.TailImagePath;
        TailImagePathLabel.Text           = string.IsNullOrEmpty(_baseline.TailImagePath)
                                            ? "No file selected"
                                            : Path.GetFileName(_baseline.TailImagePath);
        TailImagePreviewBorder.Visibility = Visibility.Collapsed;
        TailImagePreview.Source           = null;
        if (!string.IsNullOrEmpty(_baseline.TailImagePath) && File.Exists(_baseline.TailImagePath))
            SetTailImagePreview(_baseline.TailImagePath);
        Notify();
    }

    private void ResetTailGradEnabled(object s, RoutedEventArgs e)
    {
        _loading = true; TailGradCheck.IsChecked = _baseline.TailGradientEnabled; _loading = false;
        _settings.TailGradientEnabled  = _baseline.TailGradientEnabled;
        TailGradColorPicker.IsEnabled  = _baseline.TailGradientEnabled;
        Notify();
    }

    private void ResetTailGradColor(object s, RoutedEventArgs e)
    {
        _loading = true;
        TailGradColorPicker.SelectedColor = ColorHelper.ParseColor(_baseline.TailGradientEndColor);
        _loading = false;
        _settings.TailGradientEndColor = _baseline.TailGradientEndColor;
        Notify();
    }

    private void ResetTailLength(object s, RoutedEventArgs e)
    {
        _loading = true; TailLengthSlider.Value = _baseline.TailLengthMs; _loading = false;
        _settings.TailLengthMs = _baseline.TailLengthMs;
        TailLengthBox.Text     = $"{_baseline.TailLengthMs:F0}";
        Notify();
    }

    private void ResetTailThickness(object s, RoutedEventArgs e)
    {
        _loading = true; TailThicknessSlider.Value = _baseline.TailThickness; _loading = false;
        _settings.TailThickness = _baseline.TailThickness;
        TailThicknessBox.Text   = $"{_baseline.TailThickness:F1}";
        Notify();
    }

    private void ResetTailTaper(object s, RoutedEventArgs e)
    {
        _loading = true; TailTaperSlider.Value = _baseline.TailTaperPower; _loading = false;
        _settings.TailTaperPower = _baseline.TailTaperPower;
        TailTaperBox.Text        = $"{_baseline.TailTaperPower:F2}";
        Notify();
    }

    private void ResetTailFadeStyle(object s, RoutedEventArgs e)
    {
        _loading = true; TailFadeStyleCombo.SelectedIndex = _baseline.TailFadeStyle; _loading = false;
        _settings.TailFadeStyle = _baseline.TailFadeStyle;
        Notify();
    }

    private void ResetTrailSmoothness(object s, RoutedEventArgs e)
    {
        _loading = true; TrailSmoothnessSlider.Value = _baseline.TrailSmoothness; _loading = false;
        _settings.TrailSmoothness = _baseline.TrailSmoothness;
        TrailSmoothnessBox.Text   = $"{_baseline.TrailSmoothness:F2}";
        Notify();
    }

    private void ResetButterMode(object s, RoutedEventArgs e)
    {
        _loading = true; ButterCheck.IsChecked = _baseline.ButterModeEnabled; _loading = false;
        _settings.ButterModeEnabled = _baseline.ButterModeEnabled;
        Notify();
    }

    private void ResetTailGlowEnabled(object s, RoutedEventArgs e)
    {
        _loading = true; TailGlowCheck.IsChecked = _baseline.TailGlowEnabled; _loading = false;
        _settings.TailGlowEnabled = _baseline.TailGlowEnabled;
        TailGlowPanel.IsEnabled   = _baseline.TailGlowEnabled;
        Notify();
    }

    private void ResetTailGlowColor(object s, RoutedEventArgs e)
    {
        _loading = true;
        TailGlowColorPicker.SelectedColor = ColorHelper.ParseColor(_baseline.TailGlowColor);
        _loading = false;
        _settings.TailGlowColor = _baseline.TailGlowColor;
        Notify();
    }

    private void ResetTailGlowWidth(object s, RoutedEventArgs e)
    {
        _loading = true; TailGlowWidthSlider.Value = _baseline.TailGlowWidth; _loading = false;
        _settings.TailGlowWidth = _baseline.TailGlowWidth;
        TailGlowWidthBox.Text   = $"{_baseline.TailGlowWidth:F1}";
        Notify();
    }

    // ── Value TextBox handlers ────────────────────────────────────────────────

    private void ValueBox_GotFocus(object sender, RoutedEventArgs e) =>
        ((WpfTextBox)sender).SelectAll();

    private void ValueBox_KeyDown(object sender, WpfKeyArgs e)
    {
        if (e.Key == WpfKey.Enter) CommitValueBox((WpfTextBox)sender);
    }

    private void ValueBox_LostFocus(object sender, RoutedEventArgs e) =>
        CommitValueBox((WpfTextBox)sender);

    private void CommitValueBox(WpfTextBox box)
    {
        if (!double.TryParse(box.Text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v))
        {
            // revert to current setting on bad input
            if      (box == DotSizeBox)       box.Text = $"{_settings.DotSize:F1}";
            else if (box == DotGlowRadiusBox) box.Text = $"{_settings.DotGlowRadius:F0}";
            else if (box == TailLengthBox)    box.Text = $"{_settings.TailLengthMs:F0}";
            else if (box == TailThicknessBox) box.Text = $"{_settings.TailThickness:F1}";
            else if (box == TailTaperBox)     box.Text = $"{_settings.TailTaperPower:F2}";
            else if (box == TailGlowWidthBox) box.Text = $"{_settings.TailGlowWidth:F1}";
            return;
        }

        Slider slider; string fmt; Action<double> apply;
        if      (box == DotSizeBox)       { slider = DotSizeSlider;       fmt = "F1"; apply = x => _settings.DotSize        = x; }
        else if (box == DotGlowRadiusBox) { slider = DotGlowRadiusSlider; fmt = "F0"; apply = x => _settings.DotGlowRadius  = x; }
        else if (box == TailLengthBox)    { slider = TailLengthSlider;    fmt = "F0"; apply = x => _settings.TailLengthMs   = x; }
        else if (box == TailThicknessBox) { slider = TailThicknessSlider; fmt = "F1"; apply = x => _settings.TailThickness  = x; }
        else if (box == TailTaperBox)     { slider = TailTaperSlider;     fmt = "F2"; apply = x => _settings.TailTaperPower = x; }
        else if (box == TailGlowWidthBox) { slider = TailGlowWidthSlider; fmt = "F1"; apply = x => _settings.TailGlowWidth  = x; }
        else return;

        v = Math.Clamp(v, slider.Minimum, slider.Maximum);
        _loading = true; slider.Value = v; _loading = false;
        apply(v);
        box.Text = v.ToString(fmt);
        Notify();
    }

    // ── Profiles ──────────────────────────────────────────────────────────────

    private void LoadProfiles()
    {
        _profiles = SettingsService.LoadProfiles();
        RefreshProfileList();
    }

    private void RefreshProfileList()
    {
        ProfileList.ItemsSource = null;
        ProfileList.ItemsSource = _profiles.Select(p => p.Name).ToList();
    }

    private void LoadBuiltInPresets()
    {
        PresetList.ItemsSource = BuiltInPresets.All.Select(p => p.Name).ToList();
    }

    private void ProfileList_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileList.SelectedIndex >= 0 && ProfileList.SelectedIndex < _profiles.Count)
            ProfileNameBox.Text = _profiles[ProfileList.SelectedIndex].Name;
    }

    private void PresetList_Changed(object sender, SelectionChangedEventArgs e) { }

    private void Profile_Apply(object sender, RoutedEventArgs e)
    {
        var idx = ProfileList.SelectedIndex;
        if (idx < 0 || idx >= _profiles.Count) return;
        ApplyProfileSettings(_profiles[idx].Settings.Clone());
    }

    private void Preset_Apply(object sender, RoutedEventArgs e)
    {
        var idx = PresetList.SelectedIndex;
        if (idx < 0 || idx >= BuiltInPresets.All.Count) return;
        ApplyProfileSettings(BuiltInPresets.All[idx].Settings.Clone());
    }

    private void ApplyProfileSettings(LaserSettings s)
    {
        _baseline = s;          // the reference preset; reset buttons restore to this
        _settings = s.Clone();  // working copy the user edits from
        _loading  = true;
        LoadSettingsToUI(_settings);
        _loading  = false;
        Notify();
    }

    private void Profile_SaveCurrent(object sender, RoutedEventArgs e)
    {
        var name = ProfileNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) name = $"Profile {_profiles.Count + 1}";

        var existing = _profiles.FirstOrDefault(p => p.Name == name);
        if (existing is not null)
            existing.Settings = _settings.Clone();
        else
            _profiles.Add(new LaserProfile { Name = name, Settings = _settings.Clone() });

        SettingsService.SaveProfiles(_profiles);
        RefreshProfileList();
    }

    private void Profile_Delete(object sender, RoutedEventArgs e)
    {
        var idx = ProfileList.SelectedIndex;
        if (idx < 0 || idx >= _profiles.Count) return;
        _profiles.RemoveAt(idx);
        SettingsService.SaveProfiles(_profiles);
        RefreshProfileList();
    }

    private void Profile_Rename(object sender, RoutedEventArgs e)
    {
        var idx = ProfileList.SelectedIndex;
        if (idx < 0 || idx >= _profiles.Count) return;
        var name = ProfileNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        _profiles[idx].Name = name;
        SettingsService.SaveProfiles(_profiles);
        RefreshProfileList();
    }

    private void Profile_Export(object sender, RoutedEventArgs e)
    {
        var idx = ProfileList.SelectedIndex;
        if (idx < 0 || idx >= _profiles.Count) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = "Laser Profile (*.json)|*.json",
            FileName = _profiles[idx].Name
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var json = JsonSerializer.Serialize(_profiles[idx], new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dlg.FileName, json);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Profile_Import(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Laser Profile (*.json)|*.json" };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var json    = File.ReadAllText(dlg.FileName);
            var profile = JsonSerializer.Deserialize<LaserProfile>(json);
            if (profile is null) return;

            // Make name unique
            var baseName = profile.Name;
            var suffix   = 1;
            while (_profiles.Any(p => p.Name == profile.Name))
                profile.Name = $"{baseName} ({suffix++})";

            _profiles.Add(profile);
            SettingsService.SaveProfiles(_profiles);
            RefreshProfileList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Import failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Reset / Close ─────────────────────────────────────────────────────────

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Reset all settings to defaults?", "Reset",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        _settings = new LaserSettings();
        _loading  = true;
        LoadSettingsToUI(_settings);
        _loading  = false;
        Notify();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ── Live Preview ──────────────────────────────────────────────────────────

    private void DrawPreview()
    {
        if (!IsLoaded) return;

        PreviewCanvas.Children.Clear();
        var w = PreviewCanvas.ActualWidth;
        var h = PreviewCanvas.ActualHeight;
        if (w < 10 || h < 10) return;

        // Generate sine-wave path
        const int n = 60;
        var pts = Enumerable.Range(0, n + 1)
            .Select(i =>
            {
                var t = i / (double)n;
                return new WpfPoint(t * w, h / 2 + Math.Sin(t * Math.PI * 2.5) * (h * 0.35));
            }).ToList();

        // Render tapered ribbon
        var tailColor  = ColorHelper.ParseColor(_settings.TailColor);
        var endColor   = _settings.TailGradientEnabled
                         ? (MediaColor?)ColorHelper.ParseColor(_settings.TailGradientEndColor)
                         : null;
        var maxW       = _settings.TailThickness;
        var minW       = 0.5;
        var power      = _settings.TailTaperPower;

        for (var i = 1; i < pts.Count; i++)
        {
            var a    = pts[i - 1];
            var b    = pts[i];
            var prog = i / (double)(pts.Count - 1);
            var fade = Math.Clamp(Math.Pow(prog, power), 0, 1);
            var wA   = minW + (maxW - minW) * Math.Clamp(Math.Pow((i - 1.0) / (pts.Count - 1), power), 0, 1);
            var wB   = minW + (maxW - minW) * Math.Clamp(Math.Pow(prog, power), 0, 1);

            var dx = b.X - a.X; var dy = b.Y - a.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.01) continue;

            var nx = -dy / len; var ny = dx / len;
            var p1 = new WpfPoint(a.X + nx * wA * 0.5, a.Y + ny * wA * 0.5);
            var p2 = new WpfPoint(a.X - nx * wA * 0.5, a.Y - ny * wA * 0.5);
            var p3 = new WpfPoint(b.X - nx * wB * 0.5, b.Y - ny * wB * 0.5);
            var p4 = new WpfPoint(b.X + nx * wB * 0.5, b.Y + ny * wB * 0.5);

            MediaColor segColor;
            if (endColor is not null)
            {
                var lerped = ColorHelper.Lerp(tailColor, endColor.Value, 1.0 - prog);
                segColor   = MediaColor.FromArgb((byte)(lerped.A * fade), lerped.R, lerped.G, lerped.B);
            }
            else
            {
                segColor = MediaColor.FromArgb((byte)(tailColor.A * fade), tailColor.R, tailColor.G, tailColor.B);
            }

            var brush = new SolidColorBrush(segColor);
            brush.Freeze();
            var quad = new System.Windows.Shapes.Polygon
            {
                Fill   = brush,
                Points = new PointCollection { p1, p2, p3, p4 }
            };
            PreviewCanvas.Children.Add(quad);
        }

        // Draw dot head at the end
        var head     = pts[^1];
        var headBrush = new SolidColorBrush(ColorHelper.ParseColor(_settings.DotColor));
        headBrush.Freeze();
        var dot = new System.Windows.Shapes.Ellipse
        {
            Width  = _settings.DotSize * 2,
            Height = _settings.DotSize * 2,
            Fill   = headBrush,
        };
        Canvas.SetLeft(dot, head.X - _settings.DotSize);
        Canvas.SetTop (dot, head.Y - _settings.DotSize);
        PreviewCanvas.Children.Add(dot);
    }

    // ── Wave handlers ────────────────────────────────────────────────────────────────────────────────

    private void Wave_Toggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.WaveEnabled = WaveCheck.IsChecked == true;
        WavePanel.IsEnabled   = _settings.WaveEnabled;
        Notify();
    }

    private void WaveAmp_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.WaveAmplitude = WaveAmpSlider.Value;
        WaveAmpBox.Text         = $"{_settings.WaveAmplitude:F0}";
        Notify();
    }

    private void WaveFreq_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.WaveFrequency = WaveFreqSlider.Value;
        WaveFreqBox.Text        = $"{_settings.WaveFrequency:F1}";
        Notify();
    }

    private void ResetWaveEnabled(object s, RoutedEventArgs e)
    {
        _loading = true; WaveCheck.IsChecked = _baseline.WaveEnabled; _loading = false;
        _settings.WaveEnabled = _baseline.WaveEnabled;
        WavePanel.IsEnabled   = _baseline.WaveEnabled;
        Notify();
    }

    private void ResetWaveAmp(object s, RoutedEventArgs e)
    {
        _loading = true; WaveAmpSlider.Value = _baseline.WaveAmplitude; _loading = false;
        _settings.WaveAmplitude = _baseline.WaveAmplitude;
        WaveAmpBox.Text         = $"{_baseline.WaveAmplitude:F0}";
        Notify();
    }

    private void ResetWaveFreq(object s, RoutedEventArgs e)
    {
        _loading = true; WaveFreqSlider.Value = _baseline.WaveFrequency; _loading = false;
        _settings.WaveFrequency = _baseline.WaveFrequency;
        WaveFreqBox.Text        = $"{_baseline.WaveFrequency:F1}";
        Notify();
    }

    // ── Pulse handlers ──────────────────────────────────────────────────────────

    private void Pulse_Toggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.PulseEnabled = PulseCheck.IsChecked == true;
        PulsePanel.IsEnabled   = _settings.PulseEnabled;
        Notify();
    }

    private void PulseSpeed_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.PulseSpeed = PulseSpeedSlider.Value;
        PulseSpeedBox.Text    = $"{_settings.PulseSpeed:F1}";
        Notify();
    }

    private void PulseIntensity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.PulseIntensity = PulseIntensitySlider.Value;
        PulseIntensityBox.Text    = $"{_settings.PulseIntensity:F2}";
        Notify();
    }

    private void ResetPulseEnabled(object sender, RoutedEventArgs e)
    {
        _loading = true; PulseCheck.IsChecked = _baseline.PulseEnabled; _loading = false;
        _settings.PulseEnabled = _baseline.PulseEnabled;
        PulsePanel.IsEnabled   = _baseline.PulseEnabled;
        Notify();
    }

    private void ResetPulseSpeed(object sender, RoutedEventArgs e)
    {
        _loading = true; PulseSpeedSlider.Value = _baseline.PulseSpeed; _loading = false;
        _settings.PulseSpeed = _baseline.PulseSpeed;
        PulseSpeedBox.Text    = $"{_baseline.PulseSpeed:F1}";
        Notify();
    }

    private void ResetPulseIntensity(object sender, RoutedEventArgs e)
    {
        _loading = true; PulseIntensitySlider.Value = _baseline.PulseIntensity; _loading = false;
        _settings.PulseIntensity = _baseline.PulseIntensity;
        PulseIntensityBox.Text    = $"{_baseline.PulseIntensity:F2}";
        Notify();
    }

    // ── Spin handlers ─────────────────────────────────────────────────────────

    private void Spin_Toggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.SpinEnabled = SpinCheck.IsChecked == true;
        SpinPanel.IsEnabled   = _settings.SpinEnabled;
        Notify();
    }

    private void SpinSpeed_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.SpinSpeed = SpinSpeedSlider.Value;
        SpinSpeedBox.Text    = $"{_settings.SpinSpeed:F0}";
        Notify();
    }

    private void SpinDir_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.SpinCW = SpinDirCombo.SelectedIndex == 0;
        Notify();
    }

    private void ResetSpinEnabled(object sender, RoutedEventArgs e)
    {
        _loading = true; SpinCheck.IsChecked = _baseline.SpinEnabled; _loading = false;
        _settings.SpinEnabled = _baseline.SpinEnabled;
        SpinPanel.IsEnabled   = _baseline.SpinEnabled;
        Notify();
    }

    private void ResetSpinSpeed(object sender, RoutedEventArgs e)
    {
        _loading = true; SpinSpeedSlider.Value = _baseline.SpinSpeed; _loading = false;
        _settings.SpinSpeed = _baseline.SpinSpeed;
        SpinSpeedBox.Text    = $"{_baseline.SpinSpeed:F0}";
        Notify();
    }

    // ── Trail pattern handlers ────────────────────────────────────────────────

    private void TrailPattern_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.TrailPattern      = TrailPatternCombo.SelectedIndex;
        TrailPatternPanel.IsEnabled = _settings.TrailPattern > 0;
        Notify();
    }

    private void TrailPatternSpeed_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.TrailPatternSpeed = TrailPatternSpeedSlider.Value;
        TrailPatternSpeedBox.Text    = $"{_settings.TrailPatternSpeed:F1}";
        Notify();
    }

    private void TrailPatternIntensity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.TrailPatternIntensity = TrailPatternIntensitySlider.Value;
        TrailPatternIntensityBox.Text    = $"{_settings.TrailPatternIntensity:F2}";
        Notify();
    }

    private void ResetTrailPattern(object sender, RoutedEventArgs e)
    {
        _loading = true; TrailPatternCombo.SelectedIndex = _baseline.TrailPattern; _loading = false;
        _settings.TrailPattern      = _baseline.TrailPattern;
        TrailPatternPanel.IsEnabled = _baseline.TrailPattern > 0;
        Notify();
    }

    private void ResetTrailPatternSpeed(object sender, RoutedEventArgs e)
    {
        _loading = true; TrailPatternSpeedSlider.Value = _baseline.TrailPatternSpeed; _loading = false;
        _settings.TrailPatternSpeed = _baseline.TrailPatternSpeed;
        TrailPatternSpeedBox.Text    = $"{_baseline.TrailPatternSpeed:F1}";
        Notify();
    }

    private void ResetTrailPatternIntensity(object sender, RoutedEventArgs e)
    {
        _loading = true; TrailPatternIntensitySlider.Value = _baseline.TrailPatternIntensity; _loading = false;
        _settings.TrailPatternIntensity = _baseline.TrailPatternIntensity;
        TrailPatternIntensityBox.Text    = $"{_baseline.TrailPatternIntensity:F2}";
        Notify();
    }

    // ── Draw mode handlers ────────────────────────────────────────────────────

    private static readonly int[] DrawModeVKeys =
    {
        0xA2, 0xA3, 0xA4, 0xA5, 0xA0, 0xA1, 0x14, 0x09,
        0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B
    };

    private static int VKeyToComboIndex(int vkey)
    {
        var idx = Array.IndexOf(DrawModeVKeys, vkey);
        return idx >= 0 ? idx : 0;
    }

    private void DrawMode_Toggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.DrawModeEnabled = DrawModeCheck.IsChecked == true;
        DrawModePanel.IsEnabled   = _settings.DrawModeEnabled;
        Notify();
    }

    private void DrawModeKey_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var idx = DrawModeKeyCombo.SelectedIndex;
        if (idx >= 0 && idx < DrawModeVKeys.Length)
            _settings.DrawModeVKey = DrawModeVKeys[idx];
        Notify();
    }

    private void DrawModeClearMs_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.DrawModeClearDoubleTapMs = (int)Math.Round(DrawModeClearMsSlider.Value);
        DrawModeClearMsBox.Text             = $"{_settings.DrawModeClearDoubleTapMs:F0}";
        Notify();
    }

    private void ClearDrawnLines_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current.MainWindow is MainWindow mw)
            mw.ClearDrawnLines();
    }

    private void ResetDrawModeEnabled(object sender, RoutedEventArgs e)
    {
        _loading = true; DrawModeCheck.IsChecked = _baseline.DrawModeEnabled; _loading = false;
        _settings.DrawModeEnabled = _baseline.DrawModeEnabled;
        DrawModePanel.IsEnabled   = _baseline.DrawModeEnabled;
        Notify();
    }

    private void ResetDrawModeKey(object sender, RoutedEventArgs e)
    {
        _loading = true; DrawModeKeyCombo.SelectedIndex = VKeyToComboIndex(_baseline.DrawModeVKey); _loading = false;
        _settings.DrawModeVKey = _baseline.DrawModeVKey;
        Notify();
    }

    private void ResetDrawModeClearMs(object sender, RoutedEventArgs e)
    {
        _loading = true; DrawModeClearMsSlider.Value = _baseline.DrawModeClearDoubleTapMs; _loading = false;
        _settings.DrawModeClearDoubleTapMs = _baseline.DrawModeClearDoubleTapMs;
        DrawModeClearMsBox.Text             = $"{_baseline.DrawModeClearDoubleTapMs:F0}";
        Notify();
    }

    // ── Click effect handlers ──────────────────────────────────────────────────

    private void ClickEffect_Toggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.ClickEffectEnabled = ClickEffectCheck.IsChecked == true;
        ClickEffectPanel.IsEnabled   = _settings.ClickEffectEnabled;
        Notify();
    }

    private void ClickEffectStyle_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.ClickEffectStyle = ClickEffectStyleCombo.SelectedIndex;
        Notify();
    }

    private void ClickEffectColor_Changed(object? sender, EventArgs e)
    {
        if (_loading) return;
        _settings.ClickEffectColor = ColorHelper.ToHex(ClickEffectColorPicker.SelectedColor);
        Notify();
    }

    private void ClickEffectSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.ClickEffectSize = ClickEffectSizeSlider.Value;
        ClickEffectSizeBox.Text    = $"{_settings.ClickEffectSize:F0}";
        Notify();
    }

    private void ClickEffectDur_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.ClickEffectDuration = ClickEffectDurSlider.Value;
        ClickEffectDurBox.Text         = $"{_settings.ClickEffectDuration:F0}";
        Notify();
    }

    private void ResetClickEffect(object sender, RoutedEventArgs e)
    {
        _loading = true; ClickEffectCheck.IsChecked = _baseline.ClickEffectEnabled; _loading = false;
        _settings.ClickEffectEnabled = _baseline.ClickEffectEnabled;
        ClickEffectPanel.IsEnabled   = _baseline.ClickEffectEnabled;
        Notify();
    }

    private void ResetClickEffectSize(object sender, RoutedEventArgs e)
    {
        _loading = true; ClickEffectSizeSlider.Value = _baseline.ClickEffectSize; _loading = false;
        _settings.ClickEffectSize = _baseline.ClickEffectSize;
        ClickEffectSizeBox.Text    = $"{_baseline.ClickEffectSize:F0}";
        Notify();
    }

    private void ResetClickEffectDur(object sender, RoutedEventArgs e)
    {
        _loading = true; ClickEffectDurSlider.Value = _baseline.ClickEffectDuration; _loading = false;
        _settings.ClickEffectDuration = _baseline.ClickEffectDuration;
        ClickEffectDurBox.Text         = $"{_baseline.ClickEffectDuration:F0}";
        Notify();
    }

    // ── Right-click handlers ────────────────────────────────────────────────

    private void RightClick_Toggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.RightClickEnabled = RightClickCheck.IsChecked == true;
        RightClickPanel.IsEnabled   = _settings.RightClickEnabled;
        Notify();
    }

    private void RightClickStyle_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.RightClickStyle = RightClickStyleCombo.SelectedIndex;
        Notify();
    }

    private void RightClickColor_Changed(object? sender, EventArgs e)
    {
        if (_loading) return;
        _settings.RightClickColor = ColorHelper.ToHex(RightClickColorPicker.SelectedColor);
        Notify();
    }

    private void RightClickSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.RightClickSize = RightClickSizeSlider.Value;
        RightClickSizeBox.Text    = $"{_settings.RightClickSize:F0}";
        Notify();
    }

    private void RightClickDur_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.RightClickDuration = RightClickDurSlider.Value;
        RightClickDurBox.Text         = $"{_settings.RightClickDuration:F0}";
        Notify();
    }

    private void ResetRightClick(object sender, RoutedEventArgs e)
    {
        _loading = true; RightClickCheck.IsChecked = _baseline.RightClickEnabled; _loading = false;
        _settings.RightClickEnabled = _baseline.RightClickEnabled;
        RightClickPanel.IsEnabled   = _baseline.RightClickEnabled;
        Notify();
    }

    private void ResetRightClickSize(object sender, RoutedEventArgs e)
    {
        _loading = true; RightClickSizeSlider.Value = _baseline.RightClickSize; _loading = false;
        _settings.RightClickSize = _baseline.RightClickSize;
        RightClickSizeBox.Text    = $"{_baseline.RightClickSize:F0}";
        Notify();
    }

    private void ResetRightClickDur(object sender, RoutedEventArgs e)
    {
        _loading = true; RightClickDurSlider.Value = _baseline.RightClickDuration; _loading = false;
        _settings.RightClickDuration = _baseline.RightClickDuration;
        RightClickDurBox.Text         = $"{_baseline.RightClickDuration:F0}";
        Notify();
    }

    // ── Middle-click handlers ──────────────────────────────────────────────

    private void MiddleClick_Toggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.MiddleClickEnabled = MiddleClickCheck.IsChecked == true;
        MiddleClickPanel.IsEnabled   = _settings.MiddleClickEnabled;
        Notify();
    }

    private void MiddleClickStyle_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.MiddleClickStyle = MiddleClickStyleCombo.SelectedIndex;
        Notify();
    }

    private void MiddleClickColor_Changed(object? sender, EventArgs e)
    {
        if (_loading) return;
        _settings.MiddleClickColor = ColorHelper.ToHex(MiddleClickColorPicker.SelectedColor);
        Notify();
    }

    private void MiddleClickSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.MiddleClickSize = MiddleClickSizeSlider.Value;
        MiddleClickSizeBox.Text    = $"{_settings.MiddleClickSize:F0}";
        Notify();
    }

    private void MiddleClickDur_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.MiddleClickDuration = MiddleClickDurSlider.Value;
        MiddleClickDurBox.Text         = $"{_settings.MiddleClickDuration:F0}";
        Notify();
    }

    private void ResetMiddleClick(object sender, RoutedEventArgs e)
    {
        _loading = true; MiddleClickCheck.IsChecked = _baseline.MiddleClickEnabled; _loading = false;
        _settings.MiddleClickEnabled = _baseline.MiddleClickEnabled;
        MiddleClickPanel.IsEnabled   = _baseline.MiddleClickEnabled;
        Notify();
    }

    private void ResetMiddleClickSize(object sender, RoutedEventArgs e)
    {
        _loading = true; MiddleClickSizeSlider.Value = _baseline.MiddleClickSize; _loading = false;
        _settings.MiddleClickSize = _baseline.MiddleClickSize;
        MiddleClickSizeBox.Text    = $"{_baseline.MiddleClickSize:F0}";
        Notify();
    }

    private void ResetMiddleClickDur(object sender, RoutedEventArgs e)
    {
        _loading = true; MiddleClickDurSlider.Value = _baseline.MiddleClickDuration; _loading = false;
        _settings.MiddleClickDuration = _baseline.MiddleClickDuration;
        MiddleClickDurBox.Text         = $"{_baseline.MiddleClickDuration:F0}";
        Notify();
    }

    // ── Double-click handlers ─────────────────────────────────────────────

    private void DoubleClick_Toggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.DoubleClickEnabled = DoubleClickCheck.IsChecked == true;
        DoubleClickPanel.IsEnabled   = _settings.DoubleClickEnabled;
        Notify();
    }

    private void DoubleClickMult_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.DoubleClickMultiplier = DoubleClickMultSlider.Value;
        DoubleClickMultBox.Text          = $"{_settings.DoubleClickMultiplier:F1}";
        Notify();
    }

    private void DoubleClickMs_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.DoubleClickDetectMs = (int)Math.Round(DoubleClickMsSlider.Value);
        DoubleClickMsBox.Text          = $"{_settings.DoubleClickDetectMs:F0}";
        Notify();
    }

    private void ResetDoubleClick(object sender, RoutedEventArgs e)
    {
        _loading = true; DoubleClickCheck.IsChecked = _baseline.DoubleClickEnabled; _loading = false;
        _settings.DoubleClickEnabled = _baseline.DoubleClickEnabled;
        DoubleClickPanel.IsEnabled   = _baseline.DoubleClickEnabled;
        Notify();
    }

    private void ResetDoubleClickMult(object sender, RoutedEventArgs e)
    {
        _loading = true; DoubleClickMultSlider.Value = _baseline.DoubleClickMultiplier; _loading = false;
        _settings.DoubleClickMultiplier = _baseline.DoubleClickMultiplier;
        DoubleClickMultBox.Text          = $"{_baseline.DoubleClickMultiplier:F1}";
        Notify();
    }

    private void ResetDoubleClickMs(object sender, RoutedEventArgs e)
    {
        _loading = true; DoubleClickMsSlider.Value = _baseline.DoubleClickDetectMs; _loading = false;
        _settings.DoubleClickDetectMs = _baseline.DoubleClickDetectMs;
        DoubleClickMsBox.Text          = $"{_baseline.DoubleClickDetectMs:F0}";
        Notify();
    }

    // ── Auto-hide handlers ─────────────────────────────────────────────────────

    private void AutoHide_Toggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.AutoHideEnabled = AutoHideCheck.IsChecked == true;
        AutoHidePanel.IsEnabled   = _settings.AutoHideEnabled;
        Notify();
    }

    private void AutoHideDelay_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.AutoHideDelayMs = AutoHideDelaySlider.Value;
        AutoHideDelayBox.Text      = $"{_settings.AutoHideDelayMs:F0}";
        Notify();
    }

    private void ResetAutoHide(object sender, RoutedEventArgs e)
    {
        _loading = true; AutoHideCheck.IsChecked = _baseline.AutoHideEnabled; _loading = false;
        _settings.AutoHideEnabled = _baseline.AutoHideEnabled;
        AutoHidePanel.IsEnabled   = _baseline.AutoHideEnabled;
        Notify();
    }

    private void ResetAutoHideDelay(object sender, RoutedEventArgs e)
    {
        _loading = true; AutoHideDelaySlider.Value = _baseline.AutoHideDelayMs; _loading = false;
        _settings.AutoHideDelayMs = _baseline.AutoHideDelayMs;
        AutoHideDelayBox.Text      = $"{_baseline.AutoHideDelayMs:F0}";
        Notify();
    }

    // ── Particle image handlers ──────────────────────────────────────────────────

    private void ParticleImage_Browse(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select Particle Image",
            Filter = "Image Files (*.png;*.jpg;*.bmp;*.gif)|*.png;*.jpg;*.bmp;*.gif"
        };
        if (dlg.ShowDialog() != true) return;
        _settings.ClickParticleImagePath = dlg.FileName;
        ParticleImageLabel.Text          = Path.GetFileName(dlg.FileName);
        Notify();
    }

    private void ParticleImage_Clear(object sender, RoutedEventArgs e)
    {
        _settings.ClickParticleImagePath = "";
        ParticleImageLabel.Text          = "None (use colored dots)";
        Notify();
    }

    // ── Spark property handlers ─────────────────────────────────────────────────

    private void SparkCount_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.SparkCount = (int)Math.Round(SparkCountSlider.Value);
        SparkCountBox.Text    = $"{_settings.SparkCount}";
        Notify();
    }

    private void SparkParticleSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.SparkParticleSize  = SparkParticleSizeSlider.Value;
        SparkParticleSizeBox.Text     = $"{_settings.SparkParticleSize:F1}";
        Notify();
    }

    private void SparkSpread_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.SparkSpreadDeg = SparkSpreadSlider.Value;
        SparkSpreadBox.Text       = $"{_settings.SparkSpreadDeg:F0}";
        Notify();
    }

    private void SparkSpeed_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.SparkInitialSpeed = SparkSpeedSlider.Value;
        SparkSpeedBox.Text           = $"{_settings.SparkInitialSpeed:F0}";
        Notify();
    }

    private void SparkGravity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.SparkGravity = SparkGravitySlider.Value;
        SparkGravityBox.Text    = $"{_settings.SparkGravity:F0}";
        Notify();
    }

    private void ResetSparkCount(object sender, RoutedEventArgs e)
    {
        _loading = true; SparkCountSlider.Value = _baseline.SparkCount; _loading = false;
        _settings.SparkCount = _baseline.SparkCount;
        SparkCountBox.Text    = $"{_baseline.SparkCount}";
        Notify();
    }

    private void ResetSparkParticleSize(object sender, RoutedEventArgs e)
    {
        _loading = true; SparkParticleSizeSlider.Value = _baseline.SparkParticleSize; _loading = false;
        _settings.SparkParticleSize  = _baseline.SparkParticleSize;
        SparkParticleSizeBox.Text     = $"{_baseline.SparkParticleSize:F1}";
        Notify();
    }

    private void ResetSparkSpread(object sender, RoutedEventArgs e)
    {
        _loading = true; SparkSpreadSlider.Value = _baseline.SparkSpreadDeg; _loading = false;
        _settings.SparkSpreadDeg = _baseline.SparkSpreadDeg;
        SparkSpreadBox.Text       = $"{_baseline.SparkSpreadDeg:F0}";
        Notify();
    }

    private void ResetSparkSpeed(object sender, RoutedEventArgs e)
    {
        _loading = true; SparkSpeedSlider.Value = _baseline.SparkInitialSpeed; _loading = false;
        _settings.SparkInitialSpeed = _baseline.SparkInitialSpeed;
        SparkSpeedBox.Text           = $"{_baseline.SparkInitialSpeed:F0}";
        Notify();
    }

    private void ResetSparkGravity(object sender, RoutedEventArgs e)
    {
        _loading = true; SparkGravitySlider.Value = _baseline.SparkGravity; _loading = false;
        _settings.SparkGravity = _baseline.SparkGravity;
        SparkGravityBox.Text    = $"{_baseline.SparkGravity:F0}";
        Notify();
    }

    // ── Click cursor swap handlers ───────────────────────────────────────────────

    private void ClickSwap_Toggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.ClickSwapEnabled = ClickSwapCheck.IsChecked == true;
        ClickSwapPanel.IsEnabled   = _settings.ClickSwapEnabled;
        Notify();
    }

    private void ClickSwapImage_Browse(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select Click Swap Image",
            Filter = "Image Files (*.png;*.jpg;*.bmp;*.gif)|*.png;*.jpg;*.bmp;*.gif"
        };
        if (dlg.ShowDialog() != true) return;
        _settings.ClickSwapImagePath = dlg.FileName;
        ClickSwapImageLabel.Text     = Path.GetFileName(dlg.FileName);
        Notify();
    }

    private void ClickSwapImage_Clear(object sender, RoutedEventArgs e)
    {
        _settings.ClickSwapImagePath = "";
        ClickSwapImageLabel.Text     = "No file selected";
        Notify();
    }

    private void ClickSwapDur_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.ClickSwapDurationMs = ClickSwapDurSlider.Value;
        ClickSwapDurBox.Text           = $"{_settings.ClickSwapDurationMs:F0}";
        Notify();
    }

    private void ResetClickSwap(object sender, RoutedEventArgs e)
    {
        _loading = true; ClickSwapCheck.IsChecked = _baseline.ClickSwapEnabled; _loading = false;
        _settings.ClickSwapEnabled = _baseline.ClickSwapEnabled;
        ClickSwapPanel.IsEnabled   = _baseline.ClickSwapEnabled;
        Notify();
    }

    private void ResetClickSwapDur(object sender, RoutedEventArgs e)
    {
        _loading = true; ClickSwapDurSlider.Value = _baseline.ClickSwapDurationMs; _loading = false;
        _settings.ClickSwapDurationMs = _baseline.ClickSwapDurationMs;
        ClickSwapDurBox.Text           = $"{_baseline.ClickSwapDurationMs:F0}";
        Notify();
    }

    private void ClickSwapSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.ClickSwapSize = ClickSwapSizeSlider.Value;
        ClickSwapSizeBox.Text   = $"{_settings.ClickSwapSize:F0}";
        Notify();
    }

    private void ResetClickSwapSize(object sender, RoutedEventArgs e)
    {
        _loading = true; ClickSwapSizeSlider.Value = _baseline.ClickSwapSize; _loading = false;
        _settings.ClickSwapSize = _baseline.ClickSwapSize;
        ClickSwapSizeBox.Text   = $"{_baseline.ClickSwapSize:F0}";
        Notify();
    }
}
