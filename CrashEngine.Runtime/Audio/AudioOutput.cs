using System.Runtime.Versioning;
using CrashEngine.Platform.Windows;

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
        return OperatingSystem.IsWindows() ? new WaveOutAudioOutput(mixer) : null;
    }
}

[SupportedOSPlatform("windows")]
internal sealed class WaveOutAudioOutput(SoundMixer mixer) : IAudioOutput
{
    private readonly WaveOutPlayer _player = new(SoundMixer.SampleRate, mixer.Render);

    public string? Error => _player.Error;
    public int BuffersWritten => _player.BuffersWritten;
    public void Dispose() => _player.Dispose();
}
