namespace CrossWindow.Tray;

internal static class Program
{
    [System.STAThread]
    internal static void Main()
    {
        // Single-instance guard — second launch shows a message and exits cleanly.
        // Global\ prefix makes the mutex cross-session (covers Run-As and Fast User Switching).
        using var mutex = new System.Threading.Mutex(true, @"Global\CrossWindowTrayApp", out bool created);
        if (!created)
        {
            System.Windows.Forms.MessageBox.Show(
                "CrossWindow is already running.\n\nCheck the system tray.",
                "CrossWindow",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information);
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try
            {
                var logDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "CrossWindow");
                System.IO.Directory.CreateDirectory(logDir);
                var logPath = System.IO.Path.Combine(logDir, "crash.log");
                System.IO.File.AppendAllText(logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.ExceptionObject}\n\n");
            }
            catch { }
        };

        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        var app = new TrayApp();
        app.Run();
    }
}
