using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace CrashEngine.Platform.Linux;

// Amedo 2026-11-20
[SupportedOSPlatform("linux")]
internal static class ShippedLibraries
{
    private static readonly string[] Files = ["libminizip.so.1"];

    public static void Load()
    {
        foreach (var file in Files)
        {
            var path = Path.Combine(AppContext.BaseDirectory, file);
            if (File.Exists(path)) NativeLibrary.TryLoad(path, out _);
        }
    }
}
