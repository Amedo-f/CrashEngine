using CrashEngine.Core;
using CrashEngine.Importer;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyGraphicsSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.PS2AnyGraphicsSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void ApplySceneryBrightness(Entity chunkRoot, WorldLightingSettings lighting)
    {
        var sceneryRoot = chunkRoot.Children.FirstOrDefault(c => c.Name == "Scenery");
        if (sceneryRoot is null) return;

        var chunkSource = chunkRoot.Get<ChunkSource>();
        var gfx = chunkSource?.Sm2?.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION);
        if (gfx is null) return;

        if (lighting.SceneryBaseColors is null)
        {
            if (chunkSource?.Sm2 is not null)
            {
                int n = MeshDecoder.PrivatizeSharedSceneryMeshIds(chunkSource.Sm2, _extractedRoot, out var remap, out var privatizeLog);
                _browser.Log($"Scenery Brightness: {privatizeLog}");
                if (n > 0)
                {
                    foreach (var e in AllEntities(sceneryRoot))
                    {
                        var t = e.Get<SceneryTile>();
                        if (t is not null && !t.IsLod && remap.TryGetValue(t.SourceId, out var newId))
                            t.SourceId = newId;
                    }

                    if (chunkSource.SceneryTables is not null)
                        foreach (var (oldId, newId) in remap)
                            MeshDecoder.AliasSceneryMeshTableEntry(chunkSource.SceneryTables, oldId, newId);
                }
            }

            float unbake = lighting.SceneryBakedBrightness > 0.001f ? lighting.SceneryBakedBrightness : 1f;
            lighting.SceneryBaseColors = CaptureBaseColors(gfx, SceneryTileMeshIds(gfx, sceneryRoot), unbake);
        }

        ScaleBaseColors(lighting.SceneryBaseColors, lighting.SceneryBrightness);
        RefreshSceneryModels(chunkSource, gfx, SceneryTileMeshIds(gfx, sceneryRoot));

        lighting.SceneryBakedBrightness = lighting.SceneryBrightness; // Amedo 2026-09-21

        _browser.Log($"Scenery brightness set to {lighting.SceneryBrightness:F2}x ({lighting.SceneryBaseColors.Count} real submodel(s), " +
                      "every LOD detail level included). 3D preview updates immediately, no reload needed. " +
                      "Save Chunk to keep it — the slider number is remembered per level (persists on reload).");
    }

    private void ApplySkydomeBrightness(Entity chunkRoot, SkydomeMarker marker)
    {
        var chunkSource = chunkRoot.Get<ChunkSource>();
        var gfx = chunkSource?.Sm2?.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION);
        var skySec = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_SKYDOMES_SECTION);
        if (chunkSource?.Sm2 is null || gfx is null || skySec is null)
        { _browser.Log("Skydome Brightness: this level has no skydome data."); return; }

        marker.BaseColors ??= CaptureBaseColors(gfx, SkydomeMeshIds(skySec), marker.BakedBrightness > 0.001f ? marker.BakedBrightness : 1f);
        ScaleBaseColors(marker.BaseColors, marker.Brightness);
        RefreshSceneryModels(chunkSource, gfx, SkydomeMeshIds(skySec));
        marker.BakedBrightness = marker.Brightness;

        _browser.Log($"Skydome brightness set to {marker.Brightness:F2}x ({marker.BaseColors.Count} real submodel(s)). " +
                      "3D preview updates immediately, no reload needed. Save Chunk to keep it, then Build ISO + test.");
    }

    private void ApplyToLinkedScenes(Entity chunkRoot, WorldLightingSettings lighting)
    {
        var chunkSource = chunkRoot.Get<ChunkSource>();
        var links = chunkSource?.Sm2?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink>((uint)TwinConstants.SCENERY_LINK_ITEM);
        if (links is null || links.LinksList.Count == 0)
        { _browser.Log("Apply to Linked Scenes: this level has no Links -- nothing to apply to."); return; }

        var outDir = SavedChunksDir;
        int levelsUpdated = 0, levelsSkipped = 0;

        foreach (var link in links.LinksList)
        {
            try
            {
                Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2 foreignSm2;
                using (var pkg = PackageReader.Open(_extractedRoot))
                using (var stream = pkg.OpenByPath($"{link.Path}.sm2"))
                {
                    if (stream is null) { levelsSkipped++; continue; }
                    foreignSm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2();
                    using var reader = new BinaryReader(stream);
                    foreignSm2.Read(reader, (int)stream.Length);
                }

                var foreignGfx = foreignSm2.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION);
                var foreignScenery = foreignSm2.GetItem<PS2AnyScenery>((uint)TwinConstants.SCENERY_SECENERY_ITEM);
                if (foreignGfx is null || foreignScenery is null) { levelsSkipped++; continue; }

                var touchedSubs = new HashSet<object>();
                foreach (var entry in foreignScenery.Sceneries)
                {
                    foreach (var meshId in entry.MeshIDs)
                        ScaleForeignSceneryModel(foreignGfx, meshId, lighting.SceneryBrightness, touchedSubs);
                    var foreignLodSec = foreignGfx.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_LODS_SECTION);
                    foreach (var lodId in entry.LodIDs)
                    {
                        if (foreignLodSec?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyLOD>(lodId) is not { } lod) continue;
                        foreach (var meshId in lod.Meshes)
                            ScaleForeignSceneryModel(foreignGfx, meshId, lighting.SceneryBrightness, touchedSubs);
                    }
                }

                foreignScenery.FogColor = (uint)Math.Clamp(lighting.FogColorIndex, 0, CrashEngine.Importer.MeshDecoder.FogColors.Length - 1);

                if (foreignScenery.AmbientLights.Count > 0)
                {
                    var c = lighting.AmbientColor * lighting.Intensity;
                    foreignScenery.AmbientLights[0].Color = new TwinVec4(c.X, c.Y, c.Z, 0f);
                }
                int dirN = Math.Min(lighting.Directional.Count, foreignScenery.DirectionalLights.Count);
                for (int i = 0; i < dirN; i++)
                {
                    var (col, dir) = lighting.Directional[i];
                    var boosted = col * lighting.Intensity;
                    var light = foreignScenery.DirectionalLights[i];
                    light.Color = new TwinVec4(boosted.X, boosted.Y, boosted.Z, 0f);
                    light.UnkVec3 = new TwinVec4(-dir.X, dir.Y, dir.Z, 0f);
                }

                var destPath = Path.Combine(outDir, link.Path.Replace('/', '\\') + ".sm2");
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                var tempPath = destPath + ".tmp";
                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                using (var writer = new BinaryWriter(fs))
                    foreignSm2.Write(writer);
                File.Move(tempPath, destPath, overwrite: true);
                levelsUpdated++;
            }
            catch (Exception ex)
            {
                _browser.Log($"Apply to Linked Scenes: '{link.Path}' failed: {ex.Message}");
                levelsSkipped++;
            }
        }

        _browser.Log($"Apply to Linked Scenes: updated {levelsUpdated} linked level(s) on disk" +
                      (levelsSkipped > 0 ? $" ({levelsSkipped} skipped)" : "") +
                      $" with brightness {lighting.SceneryBrightness:F2}x + current World Lighting + fog = " +
                      $"{CrashEngine.Importer.MeshDecoder.FogColorNames[Math.Clamp(lighting.FogColorIndex, 0, CrashEngine.Importer.MeshDecoder.FogColorNames.Length - 1)]}. " +
                      "These are REAL saves to those levels' own files -- no need to open them yourself.");
    }

    private static void ScaleForeignSceneryModel(PS2AnyGraphicsSection foreignGfx, uint meshId, float brightness, HashSet<object> touched)
    {
        var meshSec = foreignGfx.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_MESHES_SECTION);
        var modelSec = foreignGfx.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_MODELS_SECTION);
        var rm = meshSec?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyRigidModel>(meshId);
        var model = rm is not null ? modelSec?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyModel>(rm.Model) : null;
        if (model is null) return;

        foreach (var subItem in model.SubModels)
        {
            if (subItem is not Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems.PS2SubModel sub) continue;
            sub.CalculateData();
            if (sub.Colors is null || !touched.Add(sub)) continue;
            for (int i = 0; i < sub.Colors.Count; i++)
                sub.Colors[i] = ScaleColor(sub.Colors[i], brightness);
            sub.Compile();
        }
    }

    private static Dictionary<object, List<TwinVec4>> CaptureBaseColors(PS2AnyGraphicsSection gfx, IEnumerable<uint> meshIds, float unbake)
    {
        var baseColors = new Dictionary<object, List<TwinVec4>>();
        foreach (var meshId in meshIds)
        {
            if (ResolveSceneryModel(gfx, meshId) is not { } model) continue;
            foreach (var subItem in model.SubModels)
            {
                if (subItem is not Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems.PS2SubModel sub) continue;
                if (sub.Colors is null || baseColors.ContainsKey(sub)) continue;
                baseColors[sub] = sub.Colors.Select(c => new TwinVec4(c.X / unbake, c.Y / unbake, c.Z / unbake, c.W)).ToList();
            }
        }
        return baseColors;
    }

    private static void ScaleBaseColors(Dictionary<object, List<TwinVec4>> baseColors, float brightness)
    {
        foreach (var (subObj, colors) in baseColors)
        {
            var sub = (Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems.PS2SubModel)subObj;
            for (int i = 0; i < sub.Colors.Count && i < colors.Count; i++)
                sub.Colors[i] = ScaleColor(colors[i], brightness);
            sub.Compile();
        }
    }

    private static TwinVec4 ScaleColor(TwinVec4 c, float brightness) => new(
        Math.Clamp(c.X * brightness, 0f, 1f),
        Math.Clamp(c.Y * brightness, 0f, 1f),
        Math.Clamp(c.Z * brightness, 0f, 1f), c.W);
}
