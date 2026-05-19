using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LaserCursorAppPro.Helpers;
using LaserCursorAppPro.Models;
using LaserCursorAppPro.Services;
using MediaColor  = System.Windows.Media.Color;
using MessageBox  = System.Windows.MessageBox;
using WpfPoint    = System.Windows.Point;
using WpfTextBox  = System.Windows.Controls.TextBox;
using WpfKey      = System.Windows.Input.Key;
using WpfKeyArgs  = System.Windows.Input.KeyEventArgs;

namespace LaserCursorAppPro.Options;

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
        DotGlowCheck.IsChecked          = s.DotGlowEnabled;
        DotGlowColorPicker.SelectedColor= ColorHelper.ParseColor(s.DotGlowColor);
        DotGlowRadiusSlider.Value       = s.DotGlowRadius;
        DotGlowPanel.IsEnabled          = s.DotGlowEnabled;

        TailColorPicker.SelectedColor    = ColorHelper.ParseColor(s.TailColor);
        TailGradCheck.IsChecked          = s.TailGradientEnabled;
        TailGradColorPicker.SelectedColor= ColorHelper.ParseColor(s.TailGradientEndColor);
        TailGradColorPicker.IsEnabled    = s.TailGradientEnabled;
        TailLengthSlider.Value           = s.TailLengthMs;
        TailThicknessSlider.Value        = s.TailThickness;
        TailTaperSlider.Value            = s.TailTaperPower;
        ButterCheck.IsChecked            = s.ButterModeEnabled;

        TailGlowCheck.IsChecked          = s.TailGlowEnabled;
        TailGlowColorPicker.SelectedColor= ColorHelper.ParseColor(s.TailGlowColor);
        TailGlowWidthSlider.Value        = s.TailGlowWidth;
        TailGlowPanel.IsEnabled          = s.TailGlowEnabled;

        UpdateLabels(s);
    }

    private void UpdateLabels(LaserSettings s)
    {
        DotSizeBox.Text        = $"{s.DotSize:F1}";
        DotGlowRadiusBox.Text  = $"{s.DotGlowRadius:F0}";
        TailLengthBox.Text     = $"{s.TailLengthMs:F0}";
        TailThicknessBox.Text  = $"{s.TailThickness:F1}";
        TailTaperBox.Text      = $"{s.TailTaperPower:F2}";
        TailGlowWidthBox.Text  = $"{s.TailGlowWidth:F1}";
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

    // ── Trail handlers ────────────────────────────────────────────────────────

    private void TailColor_Changed(object? sender, EventArgs e)
    {
        if (_loading) return;
        _settings.TailColor = ColorHelper.ToHex(TailColorPicker.SelectedColor);
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

    private void ResetTailColor(object s, RoutedEventArgs e)
    {
        _loading = true;
        TailColorPicker.SelectedColor = ColorHelper.ParseColor(_baseline.TailColor);
        _loading = false;
        _settings.TailColor = _baseline.TailColor;
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
            Filter   = "Laser Profile (*.lasercfg)|*.lasercfg",
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
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Laser Profile (*.lasercfg)|*.lasercfg" };
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
}
