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
    private bool          _showGroundColorPicker;
    private string        _groundColorPickerFilter = "";

    private void DrawGroundColorPickerWindow()
    {
        if (!_showGroundColorPicker) return;
        ImGui.SetNextWindowSize(new Vector2(420f, 480f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Copy All Ground Colors From Level##groundcolorpicker", ref _showGroundColorPicker))
        {
            ImGui.End();
            return;
        }

        ImGui.TextDisabled("Pick a level — every scenery mesh sharing the exact same underlying\n" +
                            "mesh id with the current level gets its vertex colors copied over,\n" +
                            "automatically, across the whole level. No manual selection needed.");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##groundColorPickerFilter", "Search levels...", ref _groundColorPickerFilter, 128);
        ImGui.BeginChild("##groundColorPickerList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        foreach (var lvl in GetSwapLevelList())
        {
            if (!string.IsNullOrWhiteSpace(_groundColorPickerFilter) &&
                !lvl.Contains(_groundColorPickerFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (ImGui.Selectable(lvl))
            {
                CopyAllGroundColorsFromLevel(lvl);
                _showGroundColorPicker = false;
            }
        }
        ImGui.EndChild();
        ImGui.End();
    }

    private void CopyAllGroundColorsFromLevel(string levelPath)
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkRoot is null || chunkSource?.Sm2 is null)
        { _browser.Log("Copy All Ground Colors: chunk has no scenery (sm2) data."); return; }

        var destGfx = chunkSource.Sm2.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION);

        var basePath = levelPath.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? levelPath[..^4] : levelPath;
        PS2AnyGraphicsSection? srcGfx;
        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            using var sm2Stream = pkg.OpenByPath($"{basePath}.sm2")
                ?? throw new FileNotFoundException($"'{basePath}.sm2' not found.");
            var srcSm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2();
            using var reader = new BinaryReader(sm2Stream);
            srcSm2.Read(reader, (int)sm2Stream.Length);
            srcGfx = srcSm2.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION);
        }
        catch (Exception ex) { _browser.Log($"Copy All Ground Colors: {ex.Message}"); return; }
        if (srcGfx is null) { _browser.Log("Copy All Ground Colors: source level has no scenery (sm2) data."); return; }

        var srcMeshSec = srcGfx.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_MESHES_SECTION);
        var srcMeshIds = new HashSet<uint>();
        if (srcMeshSec is not null)
            for (int i = 0; i < srcMeshSec.GetItemsAmount(); i++)
                if (srcMeshSec.GetItem(i) is BaseTwinItem bi) srcMeshIds.Add(bi.GetID());
        if (srcMeshIds.Count == 0)
        { _browser.Log("Copy All Ground Colors: source level has no scenery meshes."); return; }

        int tilesMatched = 0, tilesSkipped = 0, levelsCopied = 0;
        var touchedMeshIds = new HashSet<uint>();
        bool anyBroadcast = false;

        foreach (var meshChild in AllEntities(chunkRoot).Where(x => x.Has<CrashEngine.Importer.MeshRenderer>()))
        {
            var parent = meshChild.Parent;
            var tile = parent?.Get<SceneryTile>();
            if (parent is null || tile is null) continue;

            var destMeshIds = ResolveSceneryMeshIds(destGfx, tile.SourceId, tile.IsLod);
            int subIndex = parent.Children.ToList().IndexOf(meshChild);
            bool matchedAny = false;

            foreach (var meshId in destMeshIds)
            {
                if (!srcMeshIds.Contains(meshId)) continue;

                var destModel = ResolveSceneryModel(destGfx, meshId);
                var srcModel  = ResolveSceneryModel(srcGfx, meshId);
                if (destModel is null || srcModel is null) continue;
                if (subIndex < 0 || subIndex >= destModel.SubModels.Count) continue;

                var destSub = destModel.SubModels[subIndex];
                var srcSub = subIndex < srcModel.SubModels.Count ? srcModel.SubModels[subIndex]
                           : srcModel.SubModels.Count > 0 ? srcModel.SubModels[0] : null;
                if (srcSub is not null) { try { srcSub.CalculateData(); } catch { } }
                if (destSub.Colors is null || srcSub?.Colors is null || srcSub.Colors.Count == 0) continue;

                bool broadcast = srcSub.Colors.Count != destSub.Colors.Count;
                if (!broadcast)
                {
                    for (int i = 0; i < destSub.Colors.Count; i++) destSub.Colors[i] = srcSub.Colors[i];
                }
                else
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    foreach (var c in srcSub.Colors) { r += c.X; g += c.Y; b += c.Z; a += c.W; }
                    int n = srcSub.Colors.Count;
                    var avg = new TwinVec4(r / n, g / n, b / n, a / n);
                    for (int i = 0; i < destSub.Colors.Count; i++) destSub.Colors[i] = avg;
                }

                destSub.Compile();
                if (chunkSource.SceneryTables is not null)
                    MeshDecoder.RefreshSceneryMeshVertexData(Engine.Instance.GL, chunkSource.SceneryTables, meshId, destModel);

                matchedAny = true;
                levelsCopied++;
                touchedMeshIds.Add(meshId);
                anyBroadcast |= broadcast;
            }

            if (matchedAny) tilesMatched++; else tilesSkipped++;
        }

        _browser.Log($"Copy All Ground Colors: matched {tilesMatched} live tile(s) " +
                      $"({touchedMeshIds.Count} distinct mesh id(s), {levelsCopied} detail level(s) copied) " +
                      $"against '{levelPath}' by shared mesh id — {tilesSkipped} tile(s) had no matching " +
                      "mesh id in that level and were left untouched" +
                      (anyBroadcast ? " [some: vertex counts differed — broadcast the source's average color]" : "") +
                      ". Save Chunk + Build ISO to persist.");
    }
}
