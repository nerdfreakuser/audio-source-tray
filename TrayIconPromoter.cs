using Microsoft.Win32;

namespace AudioSourceTray;

static class TrayIconPromoter
{
    private const string Root = @"Control Panel\NotifyIconSettings";

    public static void PromoteThisApp()
    {
        try
        {
            var exe = Application.ExecutablePath;
            using var root = Registry.CurrentUser.OpenSubKey(Root);
            if (root is null)
            {
                return;
            }

            foreach (var name in root.GetSubKeyNames())
            {
                using var sub = Registry.CurrentUser.OpenSubKey($@"{Root}\{name}", writable: true);
                if (sub?.GetValue("ExecutablePath") is not string path)
                {
                    continue;
                }

                if (!path.Equals(exe, StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("AudioSourceTray.exe", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                sub.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
            }
        }
        catch
        {
            // Promotion is best-effort; the icon still works from the overflow.
        }
    }
}
