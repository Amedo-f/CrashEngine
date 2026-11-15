using CrashEngine.Platform.Linux;
using CrashEngine.Platform.Windows;

namespace CrashEngine.Platform;

// Amedo 2026-11-15
public interface ISoundStream : IDisposable
{
    string? Error { get; }
    int BuffersWritten { get; }
}

// Amedo 2026-11-15
public static class SoundOutput
{
    public static ISoundStream? OpenStream(uint sampleRate, Action<short[], int> render)
    {
        if (OperatingSystem.IsWindows()) return new WaveOutPlayer(sampleRate, render);
        if (OperatingSystem.IsLinux()) return new PulseAudioPlayer(sampleRate, render);
        return null;
    }

    public static bool PlayWav(byte[] wav)
    {
        if (OperatingSystem.IsWindows()) return WinMmSound.PlayWav(wav);
        if (OperatingSystem.IsLinux()) return PulseWavPlayer.Play(wav);
        return false;
    }

    public static void StopWav()
    {
        if (OperatingSystem.IsWindows()) WinMmSound.Stop();
        else if (OperatingSystem.IsLinux()) PulseWavPlayer.Stop();
    }
}
