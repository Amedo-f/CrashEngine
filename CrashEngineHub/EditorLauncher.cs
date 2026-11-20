using System.Diagnostics;
using CrashEngine.Install;
using CrashEngine.Platform;

namespace CrashEngineHub;

// Amedo 2026-11-12
public static class EditorLauncher
{
    public static string? Start(params string[] args)
    {
        var exe = AppLocator.Find(AppLocator.EditorExe, "CrashLauncher");
        if (exe is null) return $"The editor ({AppLocator.EditorExe}) was not found next to the Hub."; // Amedo 2026-11-20
        try
        {
            var psi = new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe)!, UseShellExecute = false };
            foreach (var a in args) psi.ArgumentList.Add(a);
            Process.Start(psi)?.Dispose();
            return null;
        }
        catch (Exception ex)
        {
            return $"Could not start the editor: {ex.Message}";
        }
    }

    public static void ShowFolder(string folder)
    {
        try { PlatformFiles.ShowFolder(folder); } // Amedo 2026-11-20
        catch (Exception ex) { Console.WriteLine($"[hub] open folder: {ex.Message}"); }
    }
}
