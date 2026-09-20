namespace AudioSourceTray;

sealed record AudioSource(
    string AppName,
    string ProcessName,
    int ProcessId,
    string DeviceName,
    float Peak,
    string? Detail,
    bool FromMeter,
    float Volume,
    bool VolumeAdjustable);

sealed record AudioSnapshot(IReadOnlyList<AudioSource> Sources, string? DefaultDevice)
{
    public static readonly AudioSnapshot Empty = new([], null);

    public bool IsPlaying => Sources.Count > 0;
}

sealed record MediaNowPlaying(string AppUserModelId, string Title, string? Artist);
