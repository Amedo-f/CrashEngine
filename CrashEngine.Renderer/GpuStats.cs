namespace CrashEngine.Renderer;

// Amedo 2026-09-30
public static class GpuStats
{
    public static int Draws;
    public static long Triangles;
    public static int Textures, Meshes;
    public static long TextureBytes, MeshBytes;
    public static long MeshPoolBytes; // Amedo 2026-10-01
    public static int TextureStressScale = 1;
    public const int TextureStressMaxSide = 4096;

    static GpuStats() => CrashEngine.Core.Engine.GpuContextStarted += ForgetContext;

    private static void ForgetContext()
    {
        Textures = Meshes = 0;
        TextureBytes = MeshBytes = MeshPoolBytes = 0;
    }

    public static void BeginFrame()
    {
        Draws = 0;
        Triangles = 0;
    }
}
