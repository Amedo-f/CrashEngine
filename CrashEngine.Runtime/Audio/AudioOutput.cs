using CrashEngine.Platform;

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
        return SoundOutput.OpenStream(SoundMixer.SampleRate, mixer.Render) is { } stream ? new SystemAudioOutput(stream) : null; // Amedo 2026-11-15
    }
}

// Amedo 2026-11-15
internal sealed class SystemAudioOutput(ISoundStream stream) : IAudioOutput
{
    public string? Error => stream.Error;
    public int BuffersWritten => stream.BuffersWritten;
    public void Dispose() => stream.Dispose();
}
