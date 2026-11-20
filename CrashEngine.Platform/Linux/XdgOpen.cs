using System.Diagnostics;
using System.Runtime.Versioning;

namespace CrashEngine.Platform.Linux;

// Amedo 2026-11-20
[SupportedOSPlatform("linux")]
internal static class XdgOpen
{
    public static void ShowFolder(string folder)
    {
        var psi = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
        psi.ArgumentList.Add(folder);
        Process.Start(psi)?.Dispose();
    }
}
