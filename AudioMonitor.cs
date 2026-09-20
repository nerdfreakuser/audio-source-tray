using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace AudioSourceTray;

sealed class AudioMonitor : IDisposable
{
    private const float PeakThreshold = 0.008f;

    private MMDeviceEnumerator? _enumerator;
    private readonly Dictionary<string, MMDevice> _devices = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastDeviceScanUtc = DateTime.MinValue;
    private DateTime _lastSessionRefreshUtc = DateTime.MinValue;
    private string? _defaultName;
    private DateTime _lastDefaultUtc = DateTime.MinValue;

    public IReadOnlyList<string> DescribeSessions()
    {
        var lines = new List<string>();
        try
        {
            EnsureDevices(force: true);
            foreach (var device in _devices.Values)
            {
                try
                {
                    lines.Add($"Device: {device.FriendlyName} [{device.State}]");
                    var manager = device.AudioSessionManager;
                    manager.RefreshSessions();
                    var sessions = manager.Sessions;
                    lines.Add($"  sessions: {sessions.Count}");
                    for (var i = 0; i < sessions.Count; i++)
                    {
                        using var session = sessions[i];
                        try
                        {
                            var pid = (int)session.GetProcessID;
                            var peak = session.AudioMeterInformation.MasterPeakValue;
                            var state = session.State;
                            var muted = session.SimpleAudioVolume.Mute;
                            var system = session.IsSystemSoundsSession;
                            var display = session.DisplayName;
                            var proc = "unknown";
                            if (!system && pid != 0)
                            {
                                try
                                {
                                    using var process = System.Diagnostics.Process.GetProcessById(pid);
                                    proc = process.ProcessName;
                                }
                                catch
                                {
                                    proc = $"pid:{pid}";
                                }
                            }
                            else if (system)
                            {
                                proc = "System sounds";
                            }

                            lines.Add($"  - {proc} pid={pid} state={state} mute={muted} peak={peak:0.000} display={display}");
                        }
                        catch (Exception ex)
                        {
                            lines.Add($"  - session {i} error: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    lines.Add($"  error: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            lines.Add("enumerator error: " + ex.Message);
        }

        return lines;
    }

    public AudioSnapshot Capture()
    {
        MediaSessions.KickRefresh();

        try
        {
            EnsureDevices(force: false);
            var sources = new List<AudioSource>();
            var defaultName = DefaultDeviceName();
            var refreshSessions = DateTime.UtcNow - _lastSessionRefreshUtc > TimeSpan.FromSeconds(3);
            if (refreshSessions)
            {
                _lastSessionRefreshUtc = DateTime.UtcNow;
            }

            foreach (var device in _devices.Values)
            {
                try
                {
                    var devicePeak = 0f;
                    try
                    {
                        devicePeak = device.AudioMeterInformation.MasterPeakValue;
                    }
                    catch
                    {
                        devicePeak = PeakThreshold;
                    }

                    if (devicePeak < PeakThreshold)
                    {
                        continue;
                    }

                    CollectDevice(device, sources, refreshSessions);
                }
                catch
                {
                    // Device may disappear mid-poll.
                }
            }

            var merged = Merge(sources);
            foreach (var media in MediaSessions.Unmatched(merged))
            {
                var detail = media.Artist is null ? media.Title : $"{media.Title} · {media.Artist}";
                merged.Add(new AudioSource(
                    AppName: FriendlyAumid(media.AppUserModelId),
                    ProcessName: media.AppUserModelId,
                    ProcessId: 0,
                    DeviceName: defaultName ?? "Playback device",
                    Peak: 0,
                    Detail: detail,
                    FromMeter: false));
            }

            merged.Sort((a, b) => b.Peak.CompareTo(a.Peak));
            return new AudioSnapshot(merged, defaultName);
        }
        catch
        {
            return AudioSnapshot.Empty;
        }
    }

    private void EnsureDevices(bool force)
    {
        if (!force && _devices.Count > 0 && DateTime.UtcNow - _lastDeviceScanUtc < TimeSpan.FromSeconds(8))
        {
            return;
        }

        _enumerator ??= new MMDeviceEnumerator();
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            var id = device.ID;
            if (_devices.ContainsKey(id))
            {
                // Duplicate wrapper for an endpoint we already keep alive.
                // Do not touch AudioSessionManager on this instance.
                device.Dispose();
                continue;
            }

            _ = device.AudioSessionManager;
            _devices[id] = device;
        }

        _lastDeviceScanUtc = DateTime.UtcNow;
    }

    private string? DefaultDeviceName()
    {
        if (_defaultName is not null && DateTime.UtcNow - _lastDefaultUtc < TimeSpan.FromSeconds(8))
        {
            return _defaultName;
        }

        try
        {
            _enumerator ??= new MMDeviceEnumerator();
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _defaultName = device.FriendlyName;
            _lastDefaultUtc = DateTime.UtcNow;
            return _defaultName;
        }
        catch
        {
            return _defaultName;
        }
    }

    private static void CollectDevice(MMDevice device, List<AudioSource> sources, bool refreshSessions)
    {
        var manager = device.AudioSessionManager;
        if (refreshSessions)
        {
            manager.RefreshSessions();
        }

        var sessions = manager.Sessions;
        for (var i = 0; i < sessions.Count; i++)
        {
            using var session = sessions[i];
            try
            {
                var muted = session.SimpleAudioVolume.Mute;
                if (muted)
                {
                    continue;
                }

                var peak = session.AudioMeterInformation.MasterPeakValue;
                var active = session.State == AudioSessionState.AudioSessionStateActive;
                var system = session.IsSystemSoundsSession;
                var pid = (int)session.GetProcessID;
                string processName = system || pid == 0 ? "System sounds" : "unknown";

                if (!system && pid != 0)
                {
                    try
                    {
                        using var process = System.Diagnostics.Process.GetProcessById(pid);
                        processName = process.ProcessName;
                    }
                    catch
                    {
                        processName = "unknown";
                    }
                }

                var appName = ProcessLookup.AppName(pid, processName, system);
                var mediaDetail = MediaSessions.MatchDetail(appName, processName);

                if (peak < PeakThreshold && !(active && mediaDetail is not null))
                {
                    continue;
                }

                var detail = mediaDetail ?? CleanDetail(ProcessLookup.TitleFor(pid, processName), appName, processName);

                sources.Add(new AudioSource(
                    AppName: appName,
                    ProcessName: processName,
                    ProcessId: pid,
                    DeviceName: device.FriendlyName,
                    Peak: peak,
                    Detail: detail,
                    FromMeter: peak >= PeakThreshold));
            }
            catch
            {
                // Session expired while reading.
            }
        }
    }

    private static List<AudioSource> Merge(List<AudioSource> sources)
    {
        var grouped = new Dictionary<string, AudioSource>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            var key = $"{source.ProcessName}|{source.DeviceName}|{source.AppName}";
            if (!grouped.TryGetValue(key, out var existing))
            {
                grouped[key] = source;
                continue;
            }

            grouped[key] = existing with
            {
                Peak = Math.Max(existing.Peak, source.Peak),
                Detail = PreferDetail(existing.Detail, source.Detail),
                FromMeter = existing.FromMeter || source.FromMeter,
            };
        }

        return [.. grouped.Values];
    }

    private static string? PreferDetail(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return right;
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return left;
        }

        return right.Length > left.Length ? right : left;
    }

    private static string? CleanDetail(string? windowTitle, string appName, string processName)
    {
        if (string.IsNullOrWhiteSpace(windowTitle))
        {
            return null;
        }

        var title = windowTitle.Trim();
        if (title.Equals(appName, StringComparison.OrdinalIgnoreCase)
            || title.Equals(processName, StringComparison.OrdinalIgnoreCase)
            || title.Equals(appName + ".exe", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return title;
    }

    private static string FriendlyAumid(string aumid)
    {
        if (string.IsNullOrWhiteSpace(aumid))
        {
            return "Media app";
        }

        var leaf = aumid.Split('\\', '/', '!')[^1];
        leaf = ProcessLookup.StripExe(leaf);
        var plus = leaf.IndexOf('+');
        if (plus >= 0)
        {
            leaf = leaf[(plus + 1)..];
        }

        var underscore = leaf.IndexOf('_');
        if (underscore > 0)
        {
            leaf = leaf[..underscore];
        }

        return string.IsNullOrWhiteSpace(leaf) ? "Media app" : leaf;
    }

    public void Dispose()
    {
        foreach (var device in _devices.Values)
        {
            try
            {
                device.Dispose();
            }
            catch
            {
                // COM teardown on exit.
            }
        }

        _devices.Clear();
        _enumerator?.Dispose();
        _enumerator = null;
    }
}
