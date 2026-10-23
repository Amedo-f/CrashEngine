using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Runtime.AgentLab;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;

namespace CrashEngine.Runtime;

// Amedo 2026-09-29
public sealed partial class SceneSimulation
{
    public static float FogFadeSeconds { get; set; } = 1f;

    private WorldChunk? _lookChunk;
    private bool _lookInstant = true;
    private static readonly IReadOnlyList<(GpuMesh Mesh, Material Mat)> NoSky = Array.Empty<(GpuMesh, Material)>();

    private PS2AnyScenery? SceneryOf(WorldChunk? chunk) =>
        (ReferenceEquals(chunk, _anchorChunk) ? _anchorSm2 : chunk?.Sm2)?.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM);

    private void UpdateLook_19B280()
    {
        var chunk = World.Player?.Chunk ?? World.RootChunk;
        if (ReferenceEquals(chunk, _lookChunk)) return;
        var from = _lookChunk;
        _lookChunk = chunk;
        bool anchor = ReferenceEquals(chunk, _anchorChunk);
        var scenery = SceneryOf(chunk);
        uint sky = scenery?.SkydomeID ?? 0;
        RenderPipeline.SkydomeOverride = anchor || sky == SceneryOf(_anchorChunk)?.SkydomeID ? null : SkydomeOf_1B9BC0(sky);
        var rotation = anchor ? Matrix4x4.Identity : chunk.ToAnchor;
        rotation.Translation = Vector3.Zero;
        RenderPipeline.SkydomeRotation = rotation;

        GameLightingDraw.FadeSeconds = FogFadeSeconds; // Amedo 2026-09-30
        if (RenderPipeline.Instance is { } rp)
        {
            rp.FadeFog(_lookInstant ? 0f : FogFadeSeconds);
            uint fog = scenery?.FogColor ?? uint.MaxValue;
            if (anchor || fog >= MeshDecoder.FogColors.Length) rp.FogOverride = null;
            else
            {
                rp.FogOverride = (int)fog;
                rp.FogOverrideColor = MeshDecoder.FogColors[fog];
            }
        }
        _lookInstant = false;
        if (from is not null)
            _log?.Invoke($"[sky] {from.Name} -> {chunk.Name}: skydome 0x{sky:X8}{(RenderPipeline.SkydomeOverride is { } o ? $" ({o.Count} draw(s))" : " (editor)")}, fog {scenery?.FogColor.ToString() ?? "-"}");
    }

    private IReadOnlyList<(GpuMesh Mesh, Material Mat)> SkydomeOf_1B9BC0(uint id)
    {
        if (id == 0) return NoSky;
        foreach (var s in _streamed.Values)
            if (s.Chunk.Host is { } host && All(host).Select(x => x.Get<SkydomeSet>()).FirstOrDefault(x => x is not null) is { } set
                && set.Draws.TryGetValue(id, out var draws))
                return draws;
        return NoSky;
    }

    private void ResetLook()
    {
        _lookChunk = null;
        _lookInstant = true;
        GameLightingDraw.SkipFades(); // Amedo 2026-09-30
        RenderPipeline.SkydomeOverride = null;
        RenderPipeline.SkydomeRotation = Matrix4x4.Identity;
        if (RenderPipeline.Instance is { } rp)
        {
            rp.FadeFog(0f);
            rp.FogOverride = null;
        }
    }
}
