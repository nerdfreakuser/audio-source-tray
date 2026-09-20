using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AudioSourceTray;

static class ProcessLookup
{
    private static readonly Dictionary<string, string> FriendlyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chrome"] = "Google Chrome",
        ["msedge"] = "Microsoft Edge",
        ["msedgewebview2"] = "Edge WebView",
        ["firefox"] = "Firefox",
        ["spotify"] = "Spotify",
        ["discord"] = "Discord",
        ["vlc"] = "VLC",
        ["steam"] = "Steam",
        ["steamwebhelper"] = "Steam",
        ["brave"] = "Brave",
        ["opera"] = "Opera",
        ["wmplayer"] = "Windows Media Player",
        ["microsoft.media.player"] = "Media Player",
        ["audacity"] = "Audacity",
        ["obs64"] = "OBS Studio",
        ["obs32"] = "OBS Studio",
        ["zoom"] = "Zoom",
        ["teams"] = "Microsoft Teams",
        ["ms-teams"] = "Microsoft Teams",
        ["slack"] = "Slack",
        ["telegram"] = "Telegram",
        ["whatsapp"] = "WhatsApp",
        ["itunes"] = "iTunes",
        ["foobar2000"] = "foobar2000",
        ["musicbee"] = "MusicBee",
        ["aimp"] = "AIMP",
        ["winamp"] = "Winamp",
        ["mpc-hc64"] = "MPC-HC",
        ["mpc-be64"] = "MPC-BE",
        ["code"] = "Visual Studio Code",
        ["devenv"] = "Visual Studio",
        ["explorer"] = "File Explorer",
        ["shellhost"] = "Windows",
        ["gamebar"] = "Xbox Game Bar",
        ["applicationframehost"] = "Windows app",
    };

    private static readonly Dictionary<int, string> AppNameCache = [];
    private static readonly Dictionary<int, (string? Title, DateTime Utc)> TitleCache = [];

    public static string AppName(int processId, string processName, bool systemSounds)
    {
        if (systemSounds || processId == 0)
        {
            return "System sounds";
        }

        if (AppNameCache.TryGetValue(processId, out var cached))
        {
            return cached;
        }

        var key = StripExe(processName);
        if (FriendlyNames.TryGetValue(key, out var mapped))
        {
            AppNameCache[processId] = mapped;
            return mapped;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            var description = process.MainModule?.FileVersionInfo.FileDescription;
            if (!string.IsNullOrWhiteSpace(description))
            {
                var name = description.Trim();
                AppNameCache[processId] = name;
                return name;
            }
        }
        catch
        {
            // Elevated or protected process.
        }

        var fallback = string.IsNullOrWhiteSpace(key) ? "Unknown app" : key;
        AppNameCache[processId] = fallback;
        return fallback;
    }

    public static string? TitleFor(int processId, string processName)
    {
        if (processId == 0)
        {
            return null;
        }

        if (TitleCache.TryGetValue(processId, out var cached) && DateTime.UtcNow - cached.Utc < TimeSpan.FromSeconds(2))
        {
            return cached.Title;
        }

        string? title = null;
        try
        {
            using var exact = Process.GetProcessById(processId);
            if (!string.IsNullOrWhiteSpace(exact.MainWindowTitle))
            {
                title = exact.MainWindowTitle.Trim();
            }
        }
        catch
        {
            // Process may have exited.
        }

        if (title is not null)
        {
            TitleCache[processId] = (title, DateTime.UtcNow);
            return title;
        }

        if (string.Equals(processName, "unknown", StringComparison.OrdinalIgnoreCase)
            || string.Equals(processName, "System sounds", StringComparison.OrdinalIgnoreCase))
        {
            TitleCache[processId] = (null, DateTime.UtcNow);
            return null;
        }

        Process[]? peers = null;
        try
        {
            peers = Process.GetProcessesByName(StripExe(processName));
            var foregroundPid = 0;
            var foreground = GetForegroundWindow();
            if (foreground != IntPtr.Zero)
            {
                GetWindowThreadProcessId(foreground, out uint pid);
                foregroundPid = (int)pid;
            }

            foreach (var peer in peers)
            {
                if (peer.Id == foregroundPid && !string.IsNullOrWhiteSpace(peer.MainWindowTitle))
                {
                    title = peer.MainWindowTitle.Trim();
                    TitleCache[processId] = (title, DateTime.UtcNow);
                    return title;
                }
            }

            title = peers
                .Select(peer => peer.MainWindowTitle)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .OrderByDescending(text => text.Length)
                .FirstOrDefault()
                ?.Trim();
            TitleCache[processId] = (title, DateTime.UtcNow);
            return title;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (peers is not null)
            {
                foreach (var peer in peers)
                {
                    peer.Dispose();
                }
            }
        }
    }

    public static string StripExe(string processName)
        => processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
