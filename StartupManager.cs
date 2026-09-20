using Microsoft.Win32;

namespace AudioSourceTray;

static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AudioSourceTray";
    private const string SettingsPath = @"Software\AudioSourceTray";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
        {
            key.SetValue(ValueName, QuotedExecutable());
        }
        else if (key.GetValue(ValueName) is not null)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public static void EnableOnFirstRun()
    {
        using var settings = Registry.CurrentUser.CreateSubKey(SettingsPath);
        if (settings.GetValue("Initialized") is not null)
        {
            return;
        }

        SetEnabled(true);
        settings.SetValue("Initialized", 1);
    }

    private static string QuotedExecutable()
        => $"\"{Application.ExecutablePath}\"";
}
