using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace CrashEngine.Platform.Linux;

// Amedo 2026-11-20
[SupportedOSPlatform("linux")]
internal static class LibdlRedirect
{
    private const string OldName = "libdl.so", CurrentName = "libdl.so.2";
    private static readonly HashSet<Assembly> Done = new();

    public static void Apply(Assembly assembly)
    {
        lock (Done)
            if (!Done.Add(assembly)) return;
        NativeLibrary.SetDllImportResolver(assembly, (name, _, _) =>
            name == OldName && NativeLibrary.TryLoad(CurrentName, out var handle) ? handle : IntPtr.Zero);
    }
}
