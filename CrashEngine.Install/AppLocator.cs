using CrashEngine.Platform;

namespace CrashEngine.Install;

// Amedo 2026-11-12
public static class AppLocator
{
    public static readonly string EditorExe = PlatformInfo.ExeFile("CrashLauncher"), HubExe = PlatformInfo.ExeFile("CrashEngine Hub"); // Amedo 2026-11-20

    // Amedo 2026-11-15
#if DEBUG
    private const string OwnConfiguration = "Debug";
#else
    private const string OwnConfiguration = "Release";
#endif

    public static string? Beside(string exe)
    {
        var path = Path.Combine(AppContext.BaseDirectory, exe);
        return File.Exists(path) ? path : null;
    }

    public static string? Find(string exe, string projectFolder)
    {
        if (Beside(exe) is { } beside) return beside;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var built = new[] { OwnConfiguration, OwnConfiguration == "Debug" ? "Release" : "Debug" } // Amedo 2026-11-15
                .Select(c => Path.Combine(dir.FullName, projectFolder, "bin", c, "net9.0", exe))
                .FirstOrDefault(File.Exists);
            if (built is not null) return built;
        }
        return null;
    }
}
