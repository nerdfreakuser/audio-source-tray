using System.Runtime.InteropServices;

namespace AudioSourceTray;

static class Program
{
    private const string MutexName = @"Local\AudioSourceTray.SingleInstance";

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--once", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(a, "--verbose", StringComparison.OrdinalIgnoreCase)))
        {
            AttachConsole(-1);
            using var monitor = new AudioMonitor();
            if (args.Any(a => string.Equals(a, "--verbose", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var line in monitor.DescribeSessions())
                {
                    Console.WriteLine(line);
                }

                Console.WriteLine("---");
            }

            _ = monitor.Capture();
            Thread.Sleep(400);
            var snapshot = monitor.Capture();
            Console.WriteLine(snapshot.DefaultDevice is null
                ? "Default device: (none)"
                : $"Default device: {snapshot.DefaultDevice}");
            if (snapshot.Sources.Count == 0)
            {
                Console.WriteLine("No audio playing.");
                return 0;
            }

            foreach (var source in snapshot.Sources)
            {
                Console.WriteLine($"{source.AppName} | {source.Detail ?? "-"} | {source.DeviceName} | peak={source.Peak:0.000}");
            }

            return 0;
        }

        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool created);
        if (!created)
        {
            return 0;
        }

        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AudioSourceTray");
        Directory.CreateDirectory(logDir);
        var logFile = Path.Combine(logDir, "error.log");

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log(logFile, e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log(logFile, e.ExceptionObject as Exception);
        try
        {
            Application.Run(new TrayApplicationContext());
        }
        catch (Exception ex)
        {
            Log(logFile, ex);
            throw;
        }

        return 0;
    }

    private static void Log(string path, Exception? ex)
    {
        if (ex is null)
        {
            return;
        }

        try
        {
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}");
        }
        catch
        {
            // Last-resort logging should never take the process down.
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);
}
