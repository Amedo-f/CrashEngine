using System.Diagnostics;
using System.Runtime.Versioning;

namespace CrashEngine.Platform.Windows;

// Amedo 2026-11-20
[SupportedOSPlatform("windows")]
internal static class WinExplorer
{
    public static void ShowFolder(string folder) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true })?.Dispose();
}
