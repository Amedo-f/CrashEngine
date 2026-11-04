namespace CrashEngine.Runtime.Audio;

// Amedo 2026-10-26
public interface IAudioOutput : IDisposable
{
    string? Error { get; }
    int BuffersWritten { get; }
}

// Amedo 2026-10-26
public static class AudioOutput
{
    public static Func<SoundMixer, IAudioOutput>? Factory { get; set; }

    public static IAudioOutput? Create(SoundMixer mixer)
    {
        if (Factory is { } factory) return factory(mixer);
        return OperatingSystem.IsWindows() ? new WaveOutPlayer(mixer) : null;
    }
}
