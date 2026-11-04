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
    private bool          _showVertexColorPicker;
    private List<Entity>  _vertexColorPickerTargets = new();
    private string?       _vertexColorPickerLevel;
    private string  _vertexColorPickerLevelFilter = "";
    private readonly Dictionary<string, VertexColorPickerLevelCache> _vertexColorPickerCache = new();

    private sealed class VertexColorPickerLevelCache
    {
        public List<(uint Id, System.Numerics.Vector4 Avg, Texture2D? Thumb, int W, int H)> Entries { get; } = new();
    }

    private void DrawVertexColorPickerWindow()
    {
        if (!_showVertexColorPicker) return;
        var targets = _vertexColorPickerTargets;
        if (targets.Count == 0) { _showVertexColorPicker = false; return; }

        ImGui.SetNextWindowSize(new Vector2(640f, 480f), ImGuiCond.FirstUseEver);
        var title = targets.Count == 1 ? "Copy Vertex Colors From Level##vcpicker"
                                        : $"Copy Vertex Colors To {targets.Count} Selected Tiles From Level##vcpicker";
        if (!ImGui.Begin(title, ref _showVertexColorPicker))
        {
            ImGui.End();
            return;
        }

        if (_vertexColorPickerLevel is null)
        {
            ImGui.TextDisabled("Pick a level to browse its scenery meshes' vertex colors:");
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint("##vcPickerLevelFilter", "Search levels...", ref _vertexColorPickerLevelFilter, 128);
            ImGui.BeginChild("##vcPickerLevelList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
            foreach (var lvl in GetSwapLevelList())
            {
                if (!string.IsNullOrWhiteSpace(_vertexColorPickerLevelFilter) &&
                    !lvl.Contains(_vertexColorPickerLevelFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (ImGui.Selectable(lvl))
                    _vertexColorPickerLevel = lvl;
            }
            ImGui.EndChild();
        }
        else
        {
            if (ImGui.Button("< Back to level list##vcpickerback"))
                _vertexColorPickerLevel = null;

            if (_vertexColorPickerLevel is not null)
            {
                ImGui.SameLine();
                ImGui.TextDisabled(_vertexColorPickerLevel);

                var cache = GetVertexColorPickerLevelCache(_vertexColorPickerLevel);
                ImGui.BeginChild("##vcPickerList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
                if (cache is null)
                    ImGui.TextDisabled("Failed to load this level's scenery meshes.");
                else if (cache.Entries.Count == 0)
                    ImGui.TextDisabled("This level has no scenery meshes with vertex color data.");
                else
                {
                    float avail = ImGui.GetContentRegionAvail().X;
                    const float cell = 96f;
                    int perRow = Math.Max(1, (int)(avail / (cell + 12f)));
                    int col = 0;
                    foreach (var (id, avg, thumb, w, h) in cache.Entries)
                    {
                        ImGui.PushID((int)id);
                        ImGui.BeginGroup();
                        if (thumb is not null)
                            ImGui.Image((nint)thumb.GlId, new Vector2(cell, cell), new Vector2(0f, 1f), new Vector2(1f, 0f));
                        else
                            ImGui.ColorButton($"##swatch{id:X8}", avg, ImGuiColorEditFlags.NoTooltip, new Vector2(cell, cell));
                        if (ImGui.IsItemClicked())
                        {
                            CopyVertexColorsFromLevel(targets, id, _vertexColorPickerLevel);
                            _showVertexColorPicker = false;
                        }
                        if (ImGui.IsItemHovered())
                            MaybeTooltip($"mesh {id:X8}{(thumb is not null ? $"  ({w}x{h})" : "")}\n" +
                                              $"avg vertex color: ({avg.X:F2}, {avg.Y:F2}, {avg.Z:F2})");
                        ImGui.TextDisabled(thumb is not null ? $"{w}x{h}" : "no texture");
                        ImGui.EndGroup();
                        ImGui.PopID();

                        col++;
                        if (col < perRow) ImGui.SameLine(); else col = 0;
                    }
                }
                ImGui.EndChild();
            }
        }

        ImGui.End();
    }

    private VertexColorPickerLevelCache? GetVertexColorPickerLevelCache(string levelPath)
    {
        if (_vertexColorPickerCache.TryGetValue(levelPath, out var cached)) return cached;

        var basePath = levelPath.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase)
            ? levelPath[..^4] : levelPath;
        var result = new VertexColorPickerLevelCache();
        var gl = Engine.Instance.GL;

        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            using var sm2Stream = pkg.OpenByPath($"{basePath}.sm2");
            if (sm2Stream is null) { _vertexColorPickerCache[levelPath] = result; return result; }

            var sm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2();
            using var reader = new BinaryReader(sm2Stream);
            sm2.Read(reader, (int)sm2Stream.Length);

            var gfx = sm2.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION);
            var meshSec = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_MESHES_SECTION);
            if (meshSec is null) { _vertexColorPickerCache[levelPath] = result; return result; }

            var matSec = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_MATERIALS_SECTION);
            var texSec = gfx?.GetItem<PS2AnyTexturesSection>((uint)TwinConstants.GRAPHICS_TEXTURES_SECTION);
            var thumbCache = new Dictionary<uint, (Texture2D? Thumb, int W, int H)>();

            (Texture2D? Thumb, int W, int H) ResolveThumb(
                Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyRigidModel rm)
            {
                foreach (var matId in rm.Materials)
                {
                    if (matSec?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyMaterial>(matId)
                        is not { } mat) continue;
                    foreach (var sh in mat.Shaders)
                    {
                        if (thumbCache.TryGetValue(sh.TextureId, out var cachedThumb)) return cachedThumb;
                        if (texSec?.GetItem<PS2AnyTexture>(sh.TextureId) is not { } tex) continue;
                        var thumb = ChunkImporter.DecodeTexture(gl, tex);
                        int w = tex.ImageWidthPower  > 0 ? (1 << tex.ImageWidthPower)  : 1;
                        int h = tex.ImageHeightPower > 0 ? (1 << tex.ImageHeightPower) : 1;
                        var entry = (thumb, w, h);
                        thumbCache[sh.TextureId] = entry;
                        if (thumb is not null) return entry;
                    }
                }
                return (null, 0, 0);
            }

            for (int i = 0; i < meshSec.GetItemsAmount(); i++)
            {
                if (meshSec.GetItem(i) is not Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyRigidModel rm) continue;
                if (meshSec.GetItem(i) is not BaseTwinItem bi) continue;
                var model = ResolveSceneryModel(gfx, bi.GetID());
                if (model is null) continue;

                float r = 0, g = 0, b = 0, a = 0; int n = 0;
                foreach (var sub in model.SubModels)
                {
                    try { sub.CalculateData(); } catch { continue; }
                    if (sub.Colors is null) continue;
                    foreach (var c in sub.Colors) { r += c.X; g += c.Y; b += c.Z; a += c.W; n++; }
                }
                if (n == 0) continue;

                var (thumb, tw, th) = ResolveThumb(rm);
                result.Entries.Add((bi.GetID(), new System.Numerics.Vector4(r / n, g / n, b / n, a / n), thumb, tw, th));
            }
        }
        catch (Exception ex)
        {
            _browser.Log($"Copy Vertex Colors: failed to load '{levelPath}': {ex.Message}");
            return null;
        }

        _vertexColorPickerCache[levelPath] = result;
        return result;
    }

    private void CopyVertexColorsFromLevel(IReadOnlyList<Entity> targets, uint sourceMeshId, string levelPath)
    {
        if (targets.Count == 0) return;

        var cache = GetVertexColorPickerLevelCache(levelPath);
        if (cache is null || !cache.Entries.Any(e => e.Id == sourceMeshId))
        { _browser.Log("Copy Vertex Colors: source mesh not found in the picked level."); return; }

        var basePath = levelPath.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? levelPath[..^4] : levelPath;
        Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyModel? srcModel;
        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            using var sm2Stream = pkg.OpenByPath($"{basePath}.sm2")
                ?? throw new FileNotFoundException($"'{basePath}.sm2' not found.");
            var srcSm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2();
            using var reader = new BinaryReader(sm2Stream);
            srcSm2.Read(reader, (int)sm2Stream.Length);
            var srcGfx = srcSm2.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION);
            srcModel = ResolveSceneryModel(srcGfx, sourceMeshId);
        }
        catch (Exception ex) { _browser.Log($"Copy Vertex Colors: {ex.Message}"); return; }
        if (srcModel is null) { _browser.Log("Copy Vertex Colors: couldn't re-resolve the source mesh's model data."); return; }

        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkSource?.Sm2 is null) { _browser.Log("Copy Vertex Colors: chunk has no scenery (sm2) data."); return; }

        bool broadcastAny = false;
        int ok = 0, skipped = 0;
        var touchedMeshIds = new HashSet<uint>();
        foreach (var meshChild in targets)
        {
            var (success, meshId, broadcast) = CopyVertexColorsToTile(meshChild, srcModel, chunkSource, sourceMeshId, targets.Count > 1);
            if (success) { ok++; if (meshId is not null) touchedMeshIds.Add(meshId.Value); broadcastAny |= broadcast; }
            else skipped++;
        }

        if (targets.Count == 1)
        {
            if (ok == 1)
                _browser.Log($"Copy Vertex Colors: copied vertex color(s) from mesh {sourceMeshId:X8} " +
                              $"in '{levelPath}'{(broadcastAny ? " [vertex counts differed — broadcast the source's average color]" : "")}. " +
                              "Save Chunk + Build ISO to persist.");
        }
        else
        {
            _browser.Log($"Copy Vertex Colors (batch): applied to {ok}/{targets.Count} selected tile(s) " +
                          $"({touchedMeshIds.Count} distinct underlying mesh type(s)) from mesh {sourceMeshId:X8} " +
                          $"in '{levelPath}'{(broadcastAny ? " [some tiles: vertex counts differed — broadcast the source's average color]" : "")}." +
                          (skipped > 0 ? $" {skipped} tile(s) skipped (see log lines above)." : "") +
                          " Save Chunk + Build ISO to persist.");
        }
    }

    private (bool Success, uint? MeshId, bool Broadcast) CopyVertexColorsToTile(
        Entity meshChild,
        Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyModel srcModel,
        ChunkSource chunkSource,
        uint sourceMeshId,
        bool quiet)
    {
        void Log(string msg) { if (!quiet) _browser.Log(msg); }

        var parent = meshChild.Parent;
        var tile = parent?.Get<SceneryTile>();
        if (parent is null || tile is null)
        { Log("Copy Vertex Colors: this mesh has no scenery-tile parent to resolve."); return (false, null, false); }

        var destGfx = chunkSource.Sm2.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION);

        var destMeshIds = ResolveSceneryMeshIds(destGfx, tile.SourceId, tile.IsLod);
        if (destMeshIds.Count == 0)
        { Log("Copy Vertex Colors: couldn't resolve this tile's LOD mesh."); return (false, null, false); }

        int subIndex = parent.Children.ToList().IndexOf(meshChild);
        uint? firstMeshId = destMeshIds[0];

        bool anyOk = false, anyBroadcast = false;
        foreach (var destMeshId in destMeshIds)
        {
            var destModel = ResolveSceneryModel(destGfx, destMeshId);
            if (destModel is null) continue;
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
                MeshDecoder.RefreshSceneryMeshVertexData(Engine.Instance.GL, chunkSource.SceneryTables, destMeshId, destModel);

            anyOk = true;
            anyBroadcast |= broadcast;
        }

        if (!anyOk)
        { Log("Copy Vertex Colors: couldn't apply colors to any detail level of this tile."); return (false, firstMeshId, false); }

        return (true, firstMeshId, anyBroadcast);
    }
}
