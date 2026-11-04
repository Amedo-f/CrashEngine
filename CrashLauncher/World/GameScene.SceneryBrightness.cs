using CrashEngine.Assets;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Stealth;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinChunkLink = Twinsanity.TwinsanityInterchange.Common.TwinChunkLink;
using PS2AnyLink = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink;
using TwinIntegerRotation = Twinsanity.TwinsanityInterchange.Common.TwinIntegerRotation;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyTexture = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture;
using PS2AnyGraphicsSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.PS2AnyGraphicsSection;
using PS2AnyTexturesSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics.PS2AnyTexturesSection;
using ITwinTexture = Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture;
using ITwinItem = Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem;
using TwinColor = Twinsanity.TwinsanityInterchange.Common.Color;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTrigger = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyTrigger;
using PS2AnyCamera = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyCamera;
using PS2AnyPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPosition;
using PS2AnyPath = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPath;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using PS2AnyTwinsanitySM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2;
using TwinCollisionTriangle = Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle;
using TwinGroupInformation = Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using PS2AnyParticleData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;
using TwinParticleEmitter = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;
using ITwinObject = Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.ITwinObject;

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

            lighting.SceneryBaseColors = new Dictionary<object, List<TwinVec4>>();
            foreach (var e in AllEntities(sceneryRoot).Where(x => x.Has<CrashEngine.Importer.MeshRenderer>()))
            {
                var tile = e.Parent?.Get<SceneryTile>();
                if (tile is null) continue;
                foreach (var meshId in ResolveSceneryMeshIds(gfx, tile.SourceId, tile.IsLod))
                {
                    if (ResolveSceneryModel(gfx, meshId) is not { } model) continue;
                    foreach (var subItem in model.SubModels)
                    {
                        if (subItem is not Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems.PS2SubModel sub) continue;
                        if (sub.Colors is null || lighting.SceneryBaseColors.ContainsKey(sub)) continue;
                        // Amedo 2026-09-21
                        float unbake = lighting.SceneryBakedBrightness > 0.001f ? lighting.SceneryBakedBrightness : 1f;
                        lighting.SceneryBaseColors[sub] = sub.Colors.Select(c => new TwinVec4(c.X / unbake, c.Y / unbake, c.Z / unbake, c.W)).ToList();
                    }
                }
            }
        }

        foreach (var (subObj, baseColors) in lighting.SceneryBaseColors)
        {
            var sub = (Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems.PS2SubModel)subObj;
            for (int i = 0; i < sub.Colors.Count && i < baseColors.Count; i++)
            {
                var b = baseColors[i];
                sub.Colors[i] = new TwinVec4(
                    Math.Clamp(b.X * lighting.SceneryBrightness, 0f, 1f),
                    Math.Clamp(b.Y * lighting.SceneryBrightness, 0f, 1f),
                    Math.Clamp(b.Z * lighting.SceneryBrightness, 0f, 1f), b.W);
            }
            sub.Compile();
        }

        if (chunkSource?.SceneryTables is not null)
        {
            var refreshed = new HashSet<uint>();
            foreach (var e in AllEntities(sceneryRoot).Where(x => x.Has<CrashEngine.Importer.MeshRenderer>()))
            {
                var tile = e.Parent?.Get<SceneryTile>();
                if (tile is null) continue;
                foreach (var meshId in ResolveSceneryMeshIds(gfx, tile.SourceId, tile.IsLod))
                {
                    if (!refreshed.Add(meshId)) continue;
                    if (ResolveSceneryModel(gfx, meshId) is { } model)
                        MeshDecoder.RefreshSceneryMeshVertexData(Engine.Instance.GL, chunkSource.SceneryTables, meshId, model);
                }
            }
        }

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

        if (marker.BaseColors is null)
        {
            marker.BaseColors = new Dictionary<object, List<TwinVec4>>();
            for (int i = 0; i < skySec.GetItemsAmount(); i++)
            {
                if (skySec.GetItem(i) is not Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnySkydome sky) continue;
                foreach (var meshId in sky.Meshes)
                {
                    if (ResolveSceneryModel(gfx, meshId) is not { } model) continue;
                    foreach (var subItem in model.SubModels)
                    {
                        if (subItem is not Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems.PS2SubModel sub) continue;
                        if (sub.Colors is null || marker.BaseColors.ContainsKey(sub)) continue;
                        marker.BaseColors[sub] = sub.Colors.Select(c => new TwinVec4(c.X, c.Y, c.Z, c.W)).ToList();
                    }
                }
            }
        }

        foreach (var (subObj, baseColors) in marker.BaseColors)
        {
            var sub = (Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems.PS2SubModel)subObj;
            for (int i = 0; i < sub.Colors.Count && i < baseColors.Count; i++)
            {
                var b = baseColors[i];
                sub.Colors[i] = new TwinVec4(
                    Math.Clamp(b.X * marker.Brightness, 0f, 1f),
                    Math.Clamp(b.Y * marker.Brightness, 0f, 1f),
                    Math.Clamp(b.Z * marker.Brightness, 0f, 1f), b.W);
            }
            sub.Compile();
        }

        if (chunkSource.SceneryTables is not null)
        {
            var refreshed = new HashSet<uint>();
            for (int i = 0; i < skySec.GetItemsAmount(); i++)
            {
                if (skySec.GetItem(i) is not Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnySkydome sky) continue;
                foreach (var meshId in sky.Meshes)
                {
                    if (!refreshed.Add(meshId)) continue;
                    if (ResolveSceneryModel(gfx, meshId) is { } model)
                        MeshDecoder.RefreshSceneryMeshVertexData(Engine.Instance.GL, chunkSource.SceneryTables, meshId, model);
                }
            }
        }

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
            {
                var c = sub.Colors[i];
                sub.Colors[i] = new TwinVec4(
                    Math.Clamp(c.X * brightness, 0f, 1f),
                    Math.Clamp(c.Y * brightness, 0f, 1f),
                    Math.Clamp(c.Z * brightness, 0f, 1f), c.W);
            }
            sub.Compile();
        }
    }
}
