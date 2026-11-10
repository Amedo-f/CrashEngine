using System.Runtime.InteropServices;

namespace CrashEngine.Platform.Windows;

public static class WinMmSound
{
    [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool PlaySound(byte[]? data, IntPtr hMod, uint flags);

    private const uint SND_MEMORY    = 0x0004;
    private const uint SND_ASYNC     = 0x0001;
    private const uint SND_NODEFAULT = 0x0002;

    public static bool PlayWav(byte[] wav) => PlaySound(wav, IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);

    public static void Stop() => PlaySound(null, IntPtr.Zero, 0);
}
