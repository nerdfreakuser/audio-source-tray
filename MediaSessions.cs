using Windows.Media.Control;

namespace AudioSourceTray;

static class MediaSessions
{
    private static GlobalSystemMediaTransportControlsSessionManager? _manager;
    private static Task? _refresh;
    private static IReadOnlyList<MediaNowPlaying> _latest = [];
    private static DateTime _lastAttemptUtc = DateTime.MinValue;

    public static IReadOnlyList<MediaNowPlaying> Latest => _latest;

    public static void KickRefresh()
    {
        if (_refresh is { IsCompleted: false })
        {
            return;
        }

        if (DateTime.UtcNow - _lastAttemptUtc < TimeSpan.FromSeconds(1.5))
        {
            return;
        }

        _lastAttemptUtc = DateTime.UtcNow;
        _refresh = RefreshAsync();
    }

    private static async Task RefreshAsync()
    {
        try
        {
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var playing = new List<MediaNowPlaying>();
            foreach (var session in _manager.GetSessions())
            {
                var playback = session.GetPlaybackInfo();
                if (playback.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    continue;
                }

                var props = await session.TryGetMediaPropertiesAsync();
                if (props is null || string.IsNullOrWhiteSpace(props.Title))
                {
                    continue;
                }

                var artist = string.IsNullOrWhiteSpace(props.Artist) ? null : props.Artist.Trim();
                playing.Add(new MediaNowPlaying(
                    session.SourceAppUserModelId ?? "",
                    props.Title.Trim(),
                    artist));
            }

            _latest = playing;
        }
        catch
        {
            // SMTC is optional enrichment; WASAPI remains the source of truth.
        }
    }

    public static string? MatchDetail(string appName, string processName)
    {
        var process = ProcessLookup.StripExe(processName);
        foreach (var media in _latest)
        {
            if (!Matches(media.AppUserModelId, appName, process))
            {
                continue;
            }

            return media.Artist is null ? media.Title : $"{media.Title} · {media.Artist}";
        }

        return null;
    }

    public static IEnumerable<MediaNowPlaying> Unmatched(IReadOnlyList<AudioSource> sources)
    {
        foreach (var media in _latest)
        {
            var matched = sources.Any(source =>
                Matches(media.AppUserModelId, source.AppName, ProcessLookup.StripExe(source.ProcessName)));
            if (!matched)
            {
                yield return media;
            }
        }
    }

    private static bool Matches(string aumid, string appName, string processName)
    {
        if (string.IsNullOrWhiteSpace(aumid))
        {
            return false;
        }

        if (aumid.Contains(processName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var compact = appName.Replace(" ", "", StringComparison.Ordinal);
        return compact.Length > 2 && aumid.Contains(compact, StringComparison.OrdinalIgnoreCase);
    }
}
