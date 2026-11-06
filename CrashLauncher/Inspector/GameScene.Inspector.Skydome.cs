using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyGraphicsSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.PS2AnyGraphicsSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void DrawInspectorSkydome(Entity e)
    {
        if (e.Get<CrashEngine.Importer.SkydomeMarker>() is { } skyMarker)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Skydome##sky", ImGuiTreeNodeFlags.DefaultOpen))
            {
                float skyBrightness = skyMarker.Brightness;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.SliderFloat("Brightness##sky", ref skyBrightness, 0f, 3f))
                {
                    skyMarker.Brightness = skyBrightness;
                    var chunkRootForSky = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
                    if (chunkRootForSky is not null) ApplySkydomeBrightness(chunkRootForSky, skyMarker);
                }

                DrawSkydomeUniquePicker();
            }
        }
    }

    private void SwapSkydome(string levelPath)
    {
        var basePath = levelPath.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase)
            ? levelPath[..^4] : levelPath;
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkSource?.Sm2 is null) { _browser.Log("Swap Skydome: current chunk has no scenery (sm2) data."); return; }

        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            pkg.ShadowDir = SavedChunksDir;
            using var stream = pkg.OpenByPath($"{basePath}.sm2");
            if (stream is null) { _browser.Log($"Swap Skydome: '{basePath}.sm2' not found."); return; }

            var sourceSm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2();
            using (var reader = new BinaryReader(stream))
                sourceSm2.Read(reader, (int)stream.Length);

            if (!MeshDecoder.TransplantSkydome(Engine.Instance.GL, chunkSource.Sm2, chunkSource.TexCache, sourceSm2, out string log))
            {
                _browser.Log($"Swap Skydome: {log}");
                return;
            }
            MeshDecoder.RebuildSkydomeDraws(Engine.Instance.GL, chunkSource.Sm2, chunkSource.TexCache);
            _browser.Log($"Swap Skydome: {log} Not undoable — Reload Level discards it. Save Chunk + Build ISO to keep it.");
        }
        catch (Exception ex) { _browser.Log($"Swap Skydome: {ex.Message}"); }
    }

    private void DeleteSkydome()
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        var gfx     = chunkSource?.Sm2?.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION);
        var skySec  = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_SKYDOMES_SECTION);
        if (chunkRoot is null || skySec is null)
        { _browser.Log("Delete Skydome: chunk has no skydome section."); return; }

        var ids = new List<uint>();
        for (int i = 0; i < skySec.GetItemsAmount(); i++)
            if (skySec.GetItem(i) is { } item) ids.Add(item.GetID());
        if (ids.Count == 0) { _browser.Log("Delete Skydome: this chunk has no skydome to begin with."); return; }
        foreach (var id in ids)
            skySec.RemoveItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnySkydome>(id);

        var scenery = chunkSource?.Sm2?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery>((uint)TwinConstants.SCENERY_SECENERY_ITEM);
        if (scenery is not null) scenery.SkydomeID = 0;

        RenderPipeline.SkydomeDraws.Clear();
        var skyEnt = AllEntities(chunkRoot).FirstOrDefault(x => x.Has<CrashEngine.Importer.SkydomeMarker>());
        if (skyEnt is not null)
            foreach (var child in skyEnt.Children.ToList())
                skyEnt.RemoveChild(child);

        _browser.Log($"Deleted {ids.Count} skydome item(s) (in-memory only). " +
                      "Reload Level from Disc to undo. Save Chunk + Build ISO to persist.");
    }
}
