using System.Reflection;
using CrashEngine.Platform.Linux;

namespace CrashEngine.Platform;

// Amedo 2026-11-20
public static class PlatformLibraries
{
    public static void PrepareAssimp(Assembly assimpNet)
    {
        if (!OperatingSystem.IsLinux()) return;
        ShippedLibraries.Load();
        LibdlRedirect.Apply(assimpNet);
    }
}
