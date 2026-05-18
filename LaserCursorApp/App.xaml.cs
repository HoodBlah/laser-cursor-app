using System.Windows;
using Forms = System.Windows.Forms;

namespace LaserCursorApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
	private Forms.NotifyIcon? trayIcon;
	private MainWindow? mainWindow;
	private Forms.ToolStripMenuItem? butterModeItem;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		ShutdownMode = ShutdownMode.OnExplicitShutdown;

		mainWindow = new MainWindow();
		MainWindow = mainWindow;
		mainWindow.Show();

		InitializeTrayIcon();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		if (trayIcon is not null)
		{
			trayIcon.Visible = false;
			trayIcon.Dispose();
			trayIcon = null;
		}

		base.OnExit(e);
	}

	private void InitializeTrayIcon()
	{
		trayIcon = new Forms.NotifyIcon
		{
			Icon = System.Drawing.SystemIcons.Information,
			Visible = true,
			Text = "Laser Cursor"
		};

		var contextMenu = new Forms.ContextMenuStrip();

		var toggleItem = new Forms.ToolStripMenuItem("Toggle Laser", null, (_, _) =>
		{
			if (mainWindow is null)
			{
				return;
			}

			mainWindow.ToggleEnabled();
			UpdateTrayText();
		});

		butterModeItem = new Forms.ToolStripMenuItem("Butter Mode", null, (_, _) =>
		{
			if (mainWindow is null)
			{
				return;
			}

			mainWindow.ToggleButterMode();
			UpdateTrayText();
		})
		{
			CheckOnClick = false
		};

		var exitItem = new Forms.ToolStripMenuItem("Exit", null, (_, _) =>
		{
			mainWindow?.Close();
			Shutdown();
		});

		contextMenu.Items.Add(toggleItem);
		contextMenu.Items.Add(butterModeItem);
		contextMenu.Items.Add(new Forms.ToolStripSeparator());
		contextMenu.Items.Add(exitItem);

		trayIcon.ContextMenuStrip = contextMenu;
		trayIcon.DoubleClick += (_, _) =>
		{
			if (mainWindow is null)
			{
				return;
			}

			mainWindow.ToggleEnabled();
			UpdateTrayText();
		};

		UpdateTrayText();
	}

	private void UpdateTrayText()
	{
		if (trayIcon is null || mainWindow is null)
		{
			return;
		}

		trayIcon.Text = mainWindow.IsLaserEnabled ? "Laser Cursor: On" : "Laser Cursor: Off";

		if (butterModeItem is not null)
		{
			butterModeItem.Checked = mainWindow.IsButterModeEnabled;
			butterModeItem.Text = mainWindow.IsButterModeEnabled ? "Butter Mode: On" : "Butter Mode: Off";
		}
	}
}

