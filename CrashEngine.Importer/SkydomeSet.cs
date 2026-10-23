using CrashEngine.Core;
using CrashEngine.Renderer;

namespace CrashEngine.Importer;

// Amedo 2026-09-29
public sealed class SkydomeSet : Component
{
    public readonly Dictionary<uint, List<(GpuMesh Mesh, Material Mat)>> Draws = new();
}
