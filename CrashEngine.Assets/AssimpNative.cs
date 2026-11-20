using Assimp;
using CrashEngine.Platform;

namespace CrashEngine.Assets;

// Amedo 2026-11-20
internal static class AssimpNative
{
    static AssimpNative() => PlatformLibraries.PrepareAssimp(typeof(AssimpContext).Assembly);

    public static AssimpContext NewContext() => new();
}
