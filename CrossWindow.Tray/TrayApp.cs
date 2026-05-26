using System.Diagnostics;
using System.Windows;
using CrossWindow.Core.Services;
using CrossWindow.Core.Win32;
using Microsoft.Win32;
using Drawing  = System.Drawing;
using WinForms = System.Windows.Forms;

namespace CrossWindow.Tray;

public sealed class TrayApp : System.Windows.Application
{
    private WinForms.NotifyIcon        _tray        = null!;
    private HotkeyThread               _hotkeys     = null!;
    private WinForms.ToolStripMenuItem _pauseItem   = null!;
    private WinForms.ToolStripMenuItem _startupItem = null!;
    private Drawing.Icon?              _activeIcon;
    private Drawing.Icon?              _pausedIcon;
    private Drawing.Icon?              _swapIcon;

    private enum TrayIconState { Active, Paused, Swap }

    private const string RegValueName = "CrossWindow";
    private const string RegRunPath   = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    // ─── Startup / Shutdown ───────────────────────────────────────────────────────

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _activeIcon = CreateTrayIcon(TrayIconState.Active);
        _pausedIcon = CreateTrayIcon(TrayIconState.Paused);
        _swapIcon   = CreateTrayIcon(TrayIconState.Swap);

        var monitors = new MonitorManager();
        var windows  = new WindowManager();
        var engine   = new MovementEngine(monitors, windows);
        _hotkeys     = new HotkeyThread(engine);

        _hotkeys.SwapModeChanged += active =>
        {
            Dispatcher.Invoke(() =>
            {
                _tray.Icon = active ? _swapIcon : _activeIcon;
                _tray.Text = active ? "CrossWindow (swap mode)" : "CrossWindow";
                _tray.ShowBalloonTip(1500, "CrossWindow",
                    active ? "Swap mode ON — Move hotkeys now swap" : "Swap mode OFF",
                    WinForms.ToolTipIcon.None);
            });
        };

        BuildTray();

        int count = _hotkeys.Start();

        if (IsFirstRun())
        {
            _tray.ShowBalloonTip(6000, "CrossWindow — ready",
                "Move: Ctrl+Alt+Arrows  |  Snap: Ctrl+Shift+Win+Arrows  |  Swap mode: Ctrl+Shift+Alt+S",
                WinForms.ToolTipIcon.Info);
            MarkFirstRunDone();
        }
        else
        {
            _tray.ShowBalloonTip(2500, "CrossWindow", $"{count} hotkeys active", WinForms.ToolTipIcon.None);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeys.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _activeIcon?.Dispose();
        _pausedIcon?.Dispose();
        _swapIcon?.Dispose();
        base.OnExit(e);
    }

    // ─── Tray Construction ────────────────────────────────────────────────────────

    private void BuildTray()
    {
        var menu = new WinForms.ContextMenuStrip();

        // Header (non-interactive label)
        menu.Items.Add(new WinForms.ToolStripMenuItem("CrossWindow") { Enabled = false });
        menu.Items.Add(new WinForms.ToolStripSeparator());

        // Pause / Resume
        _pauseItem = new WinForms.ToolStripMenuItem("Pause");
        _pauseItem.Click += OnTogglePause;
        menu.Items.Add(_pauseItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        // Edit Config
        var editItem = new WinForms.ToolStripMenuItem("Edit Config");
        editItem.Click += (_, _) =>
        {
            HotkeyConfig.EnsureDefaultsWritten();
            Process.Start(new ProcessStartInfo(HotkeyConfig.ConfigPath) { UseShellExecute = true });
        };
        menu.Items.Add(editItem);

        // Reload Config
        var reloadItem = new WinForms.ToolStripMenuItem("Reload Config");
        reloadItem.Click += OnReloadConfig;
        menu.Items.Add(reloadItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        // Start with Windows
        _startupItem = new WinForms.ToolStripMenuItem("Start with Windows")
        {
            Checked      = IsStartupEnabled(),
            CheckOnClick = true
        };
        _startupItem.CheckedChanged += OnStartupToggled;
        menu.Items.Add(_startupItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        // Exit
        var exitItem = new WinForms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => Shutdown();
        menu.Items.Add(exitItem);

        _tray = new WinForms.NotifyIcon
        {
            Text             = "CrossWindow",
            Icon             = _activeIcon,
            ContextMenuStrip = menu,
            Visible          = true
        };

        // Double-click toggles pause
        _tray.DoubleClick += OnTogglePause;
    }

    // ─── Menu Handlers ────────────────────────────────────────────────────────────

    private void OnTogglePause(object? sender, EventArgs e)
    {
        if (_hotkeys.IsRunning)
        {
            _hotkeys.Stop();
            _pauseItem.Text  = "Resume";
            _tray.Icon       = _pausedIcon;
            _tray.Text       = "CrossWindow (paused)";
        }
        else
        {
            int count        = _hotkeys.Start();
            _pauseItem.Text  = "Pause";
            _tray.Icon       = _activeIcon;
            _tray.Text       = "CrossWindow";
            _tray.ShowBalloonTip(1500, "CrossWindow", $"{count} hotkeys active", WinForms.ToolTipIcon.None);
        }
    }

    private void OnReloadConfig(object? sender, EventArgs e)
    {
        _hotkeys.Stop();
        int count = _hotkeys.Start();
        // Reset icon/tooltip — swap mode is cleared by Stop() so always return to active state
        _tray.Icon = _activeIcon;
        _tray.Text = "CrossWindow";
        _tray.ShowBalloonTip(1500, "CrossWindow", $"Reloaded — {count} hotkeys active", WinForms.ToolTipIcon.None);
    }

    // ─── Start with Windows ───────────────────────────────────────────────────────

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegRunPath, writable: false);
        return key?.GetValue(RegValueName) != null;
    }

    private void OnStartupToggled(object? sender, EventArgs e)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegRunPath, writable: true);
        if (key == null) return;

        if (_startupItem.Checked)
        {
            var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            key.SetValue(RegValueName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(RegValueName, throwOnMissingValue: false);
        }
    }

    // ─── First-run detection ──────────────────────────────────────────────────────

    private const string RegAppPath     = @"SOFTWARE\CrossWindow";
    private const string RegFirstRunKey = "FirstRunDone";

    private static bool IsFirstRun()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegAppPath, writable: false);
        return key?.GetValue(RegFirstRunKey) == null;
    }

    private static void MarkFirstRunDone()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegAppPath, writable: true);
        key?.SetValue(RegFirstRunKey, "1");
    }

    // ─── Icon ─────────────────────────────────────────────────────────────────────

    private static Drawing.Icon CreateTrayIcon(TrayIconState state)
    {
        using var bmp = new Drawing.Bitmap(32, 32);
        using var g   = Drawing.Graphics.FromImage(bmp);
        g.Clear(Drawing.Color.Transparent);

        var color = state switch
        {
            TrayIconState.Paused => Drawing.Color.FromArgb(100, 100, 110),          // gray
            TrayIconState.Swap   => Drawing.Color.FromArgb(255, 182, 39),            // gold
            _                    => Drawing.Color.FromArgb(255, 31, 107),            // --cw-pink
        };

        using var brush = new Drawing.SolidBrush(color);

        // 2×2 grid — represents a monitor layout
        const int p   = 3;   // outer padding
        const int s   = 11;  // square size
        const int gap = 2;   // gap between squares

        g.FillRectangle(brush, p,         p,         s, s);
        g.FillRectangle(brush, p + s + gap, p,         s, s);
        g.FillRectangle(brush, p,         p + s + gap, s, s);
        g.FillRectangle(brush, p + s + gap, p + s + gap, s, s);

        // GetHicon allocates a GDI handle; clone the Icon so we own the copy,
        // then destroy the original handle to avoid leaking GDI objects.
        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var temp = Drawing.Icon.FromHandle(hIcon);
            return (Drawing.Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }
}
