using WindroseServerManager.Forms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // These MUST be called before any Win32 window is created
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        // Write any startup crash to a plain text file before the logger exists
        string crashLog = Path.Combine(AppContext.BaseDirectory, "startup_crash.log");
        try
        {
            Application.SetColorMode(SystemColorMode.Dark);

            Application.ThreadException += (s, e) =>
            {
                string msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ThreadException: {e.Exception}\n";
                try { File.AppendAllText(crashLog, msg); } catch { }
                MessageBox.Show($"Unhandled error: {e.Exception.Message}\n\nDetails written to:\n{crashLog}",
                    "Windrose Server Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                string msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UnhandledException: {e.ExceptionObject}\n";
                try { File.AppendAllText(crashLog, msg); } catch { }
                MessageBox.Show($"Fatal error: {e.ExceptionObject}\n\nDetails written to:\n{crashLog}",
                    "Windrose Server Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            File.WriteAllText(crashLog, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] STARTUP CRASH\n{ex}\n");
            MessageBox.Show($"Failed to start:\n\n{ex.Message}\n\nFull details in:\n{crashLog}",
                "Windrose Server Manager — Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
