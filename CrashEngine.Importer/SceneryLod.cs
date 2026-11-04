using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Renderer;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;

namespace CrashEngine.Importer;

public sealed class SceneryLod : Component
{
    public static bool Original { get; set; }

    private readonly Entity[] _levels;
    private readonly uint _minDrawDistance, _maxDrawDistance;
    private readonly uint[] _modelsDrawDistances;
    private int _shown = -2;
    private static int _logged;

    private SceneryLod(Entity[] levels, PS2AnyLOD lod)
    {
        _levels = levels;
        _minDrawDistance = (uint)lod.MinDrawDistance;
        _maxDrawDistance = (uint)lod.MaxDrawDistance;
        _modelsDrawDistances = Array.ConvertAll(lod.ModelsDrawDistances, d => (uint)d);
    }

    public static SceneryLod Attach(Entity tile, PS2AnyLOD lod, IReadOnlyDictionary<uint, List<(GpuMesh, Material)>> meshById)
    {
        var levels = new Entity[lod.Meshes.Count];
        for (int i = 0; i < levels.Length; i++)
        {
            levels[i] = new Entity($"lod{i}_{lod.Meshes[i]:X8}");
            if (meshById.TryGetValue(lod.Meshes[i], out var entry))
                foreach (var (gpuMesh, material) in entry)
                {
                    var child = new Entity($"lod{i}_mesh");
                    child.Add(new MeshRenderer { Mesh = gpuMesh, Material = material });
                    levels[i].AddChild(child);
                }
            tile.AddChild(levels[i]);
        }
        if (_logged++ < 3)
            Console.WriteLine($"[lod] {tile.Name}: {lod.Meshes.Count} levels, squared distance {(uint)lod.MinDrawDistance}..{(uint)lod.MaxDrawDistance}, switch at {string.Join(", ", lod.ModelsDrawDistances.Select(d => (uint)d))}");
        return tile.Add(new SceneryLod(levels, lod));
    }

    public override void OnUpdate()
    {
        if (RenderPipeline.Instance?.Camera is not { } camera) return;
        float squared = GameMath.DistanceSq(camera.Transform.Position, Entity.Transform.World.Translation);
        int level = SelectLevel_1C1A10((uint)GameMath.Trunc(squared));
        if (level == _shown) return;
        _shown = level;
        for (int i = 0; i < _levels.Length; i++) _levels[i].Active = i == level;
    }

    private int SelectLevel_1C1A10(uint distance)
    {
        if (distance < _minDrawDistance || _maxDrawDistance < distance) return -1;
        int level = 0;
        int last = Math.Min(_levels.Length - 1, _modelsDrawDistances.Length);
        while (level < last && distance >= _modelsDrawDistances[level]) level++;
        return level;
    }
}
