using System.Windows;
using LaserCursorAppPro.Models;
using LaserCursorAppPro.Options;
using LaserCursorAppPro.Services;
using Forms = System.Windows.Forms;
// Disambiguate Application so WPF partial class matches correctly
using Application = System.Windows.Application;

namespace LaserCursorAppPro;

public partial class App : Application
{
    private MainWindow?                 _mainWindow;
    private Forms.NotifyIcon?           _trayIcon;
    private Forms.ToolStripMenuItem?    _toggleItem;
    private Forms.ToolStripMenuItem?    _butterItem;
    private LaserSettings               _settings     = new();
    private bool                        _laserEnabled = true;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings   = SettingsService.LoadSettings();
        _mainWindow = new MainWindow();
        _mainWindow.ApplySettings(_settings);
        _mainWindow.Show();

        BuildTray();
    }

    private void BuildTray()
    {
        _toggleItem = new Forms.ToolStripMenuItem("Toggle Laser",  null, (_, _) => ToggleLaser());
        _butterItem = new Forms.ToolStripMenuItem("Butter Mode",   null, (_, _) => ToggleButterMode())
            { Checked = _settings.ButterModeEnabled };
        var optionsItem = new Forms.ToolStripMenuItem("Options\u2026", null, (_, _) => OpenOptions());
        var exitItem    = new Forms.ToolStripMenuItem("Exit",          null, (_, _) => Shutdown());

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_toggleItem);
        menu.Items.Add(_butterItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(optionsItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new Forms.NotifyIcon
        {
            Icon              = System.Drawing.SystemIcons.Application,
            Text              = "Laser Cursor Pro",
            ContextMenuStrip  = menu,
            Visible           = true,
        };
        _trayIcon.DoubleClick += (_, _) => ToggleLaser();

        UpdateTrayText();
    }

    private void ToggleLaser()
    {
        _laserEnabled = !_laserEnabled;
        if (_laserEnabled)
            _mainWindow?.Show();
        else
            _mainWindow?.Hide();
        UpdateTrayText();
    }

    private void ToggleButterMode()
    {
        _settings.ButterModeEnabled = !_settings.ButterModeEnabled;
        _mainWindow?.ApplySettings(_settings);
        SettingsService.SaveSettings(_settings);
        UpdateTrayText();
    }

    private void OpenOptions()
    {
        var win = new OptionsWindow(_settings, s =>
        {
            _settings = s;
            _mainWindow?.ApplySettings(_settings);
            SettingsService.SaveSettings(_settings);
            UpdateTrayText();
        });
        win.Show();
    }

    private void UpdateTrayText()
    {
        if (_trayIcon is null) return;
        var state  = _laserEnabled ? "ON" : "OFF";
        var butter = _settings.ButterModeEnabled ? " [Butter]" : "";
        _trayIcon.Text = $"Laser Cursor Pro \u2014 {state}{butter}";
        if (_butterItem is not null)
            _butterItem.Checked = _settings.ButterModeEnabled;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SettingsService.SaveSettings(_settings);
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}

