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
    private void DrawTexturePickerWindow()
    {
        if (!_showTexturePicker) return;
        var mat = _texturePickerTargetMat;
        if (mat is null) { _showTexturePicker = false; return; }

        ImGui.SetNextWindowSize(new Vector2(640f, 480f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Import Texture From Level##texpicker", ref _showTexturePicker))
        {
            ImGui.End();
            return;
        }

        if (_texturePickerLevel is null)
        {
            ImGui.TextDisabled("Pick a level to browse its real textures:");
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint("##texPickerLevelFilter", "Search levels...", ref _texturePickerLevelFilter, 128);
            ImGui.BeginChild("##texPickerLevelList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
            foreach (var lvl in GetSwapLevelList())
            {
                if (!string.IsNullOrWhiteSpace(_texturePickerLevelFilter) &&
                    !lvl.Contains(_texturePickerLevelFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (ImGui.Selectable(lvl))
                    _texturePickerLevel = lvl;
            }
            ImGui.EndChild();
        }
        else
        {
            if (ImGui.Button("< Back to level list##texpickerback"))
            {
                _texturePickerLevel = null;
                _texturePickerTexFilter = "";
            }

            if (_texturePickerLevel is not null)
            {
                ImGui.SameLine();
                ImGui.TextDisabled(_texturePickerLevel);

                ImGui.SetNextItemWidth(-1f);
                ImGui.InputTextWithHint("##texPickerTexFilter", "Filter by size (e.g. 128x128)...", ref _texturePickerTexFilter, 64);

                var cache = GetTexturePickerLevelCache(_texturePickerLevel);
                ImGui.BeginChild("##texPickerGrid", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
                if (cache is null)
                    ImGui.TextDisabled("Failed to load this level's textures.");
                else if (cache.Entries.Count == 0)
                    ImGui.TextDisabled("This level has no textures.");
                else
                {
                    float avail = ImGui.GetContentRegionAvail().X;
                    const float cell = 96f;
                    int perRow = Math.Max(1, (int)(avail / (cell + 12f)));
                    int col = 0;
                    foreach (var entry in cache.Entries)
                    {
                        if (!string.IsNullOrWhiteSpace(_texturePickerTexFilter) &&
                            !$"{entry.W}x{entry.H}".Contains(_texturePickerTexFilter, StringComparison.OrdinalIgnoreCase))
                            continue;

                        ImGui.PushID((int)entry.Id ^ (entry.IsScenery ? unchecked((int)0x9E3779B9) : 0));
                        ImGui.BeginGroup();
                        ImGui.Image((nint)entry.Thumb.GlId, new Vector2(cell, cell), new Vector2(0f, 1f), new Vector2(1f, 0f));
                        if (ImGui.IsItemClicked())
                        {
                            ImportTextureFromLevel(mat, entry.Id, entry.IsScenery, _texturePickerLevel);
                            _showTexturePicker = false;
                        }
                        ImGui.TextDisabled($"{entry.W}x{entry.H}{(entry.IsScenery ? " (scenery)" : "")}");
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

    private TexturePickerLevelCache? GetTexturePickerLevelCache(string levelPath)
    {
        if (_texturePickerCache.TryGetValue(levelPath, out var cached)) return cached;

        var basePath = levelPath.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase)
            ? levelPath[..^4] : levelPath;
        var result = new TexturePickerLevelCache();
        var gl = Engine.Instance.GL;

        void CollectFrom(PS2AnyGraphicsSection? gfx, bool isScenery)
        {
            var texSec = gfx?.GetItem<PS2AnyTexturesSection>((uint)TwinConstants.GRAPHICS_TEXTURES_SECTION);
            if (texSec is null) return;
            for (int i = 0; i < texSec.GetItemsAmount(); i++)
            {
                if (texSec.GetItem(i) is not PS2AnyTexture t) continue;
                var thumb = ChunkImporter.DecodeTexture(gl, t);
                if (thumb is null) continue;
                int w = t.ImageWidthPower  > 0 ? (1 << t.ImageWidthPower)  : 1;
                int h = t.ImageHeightPower > 0 ? (1 << t.ImageHeightPower) : 1;
                result.Entries.Add((t.GetID(), thumb, w, h, isScenery));
            }
        }

        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);

            using (var rm2Stream = pkg.OpenByPath($"{basePath}.rm2"))
            {
                if (rm2Stream is not null)
                {
                    var rm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2();
                    using var reader = new BinaryReader(rm2Stream);
                    rm2.Read(reader, (int)rm2Stream.Length);
                    result.Rm2 = rm2;
                    CollectFrom(rm2.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.LEVEL_GRAPHICS_SECTION), false);
                }
            }

            using (var sm2Stream = pkg.OpenByPath($"{basePath}.sm2"))
            {
                if (sm2Stream is not null)
                {
                    var sm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2();
                    using var reader = new BinaryReader(sm2Stream);
                    sm2.Read(reader, (int)sm2Stream.Length);
                    result.Sm2 = sm2;
                    CollectFrom(sm2.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION), true);
                }
            }
        }
        catch (Exception ex)
        {
            _browser.Log($"Import From Level: failed to load '{levelPath}': {ex.Message}");
            return null;
        }

        _texturePickerCache[levelPath] = result;
        return result;
    }

    private void ImportTextureFromLevel(Material mat, uint sourceId, bool sourceIsScenery, string levelPath)
    {
        if (mat.Albedo is null) { _browser.Log("Import From Level: this material has no texture to replace."); return; }

        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkSource is null) return;

        if (!TraceTextureId(chunkSource, mat.Albedo, out uint destId, out bool destIsScenery, out bool destIsGlobal))
        { _browser.Log("Import From Level: couldn't trace this texture back to a source id."); return; }

        var destTex = FindTextureById(chunkSource, destId, destIsScenery, destIsGlobal, out var destSource);
        bool fromGlobal = destSource == TextureSource.GlobalRm2;
        if (destTex is null) { _browser.Log($"Import From Level: destination PS2 texture {destId:X8} not found."); return; }

        var cache = GetTexturePickerLevelCache(levelPath);
        var srcGfx = sourceIsScenery
            ? cache?.Sm2?.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION)
            : cache?.Rm2?.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.LEVEL_GRAPHICS_SECTION);
        var srcTexSec = srcGfx?.GetItem<PS2AnyTexturesSection>((uint)TwinConstants.GRAPHICS_TEXTURES_SECTION);
        var srcTex = srcTexSec?.GetItem<PS2AnyTexture>(sourceId);
        if (srcTex is null) { _browser.Log($"Import From Level: source texture {sourceId:X8} not found."); return; }

        try
        {
            srcTex.CalculateData();
            if (srcTex.Colors.Count == 0) { _browser.Log("Import From Level: source texture has no decodable pixel data."); return; }

            int sw = srcTex.ImageWidthPower  > 0 ? (1 << srcTex.ImageWidthPower)  : 1;
            int sh = srcTex.ImageHeightPower > 0 ? (1 << srcTex.ImageHeightPower) : 1;

            var srcRgba = new byte[sw * sh * 4];
            int idx = 0;
            foreach (TwinColor c in srcTex.Colors)
            {
                srcRgba[idx++] = c.R; srcRgba[idx++] = c.G; srcRgba[idx++] = c.B; srcRgba[idx++] = c.A;
            }

            int dw = destTex.ImageWidthPower  > 0 ? (1 << destTex.ImageWidthPower)  : sw;
            int dh = destTex.ImageHeightPower > 0 ? (1 << destTex.ImageHeightPower) : sh;
            var rgba = (dw == sw && dh == sh) ? srcRgba : ResizeNearest(srcRgba, sw, sh, dw, dh);

            var colors = new List<TwinColor>(dw * dh);
            for (int i = 0; i < dw * dh; i++)
                colors.Add(new TwinColor(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]));

            destTex.FromBitmap(colors, dw, destTex.TexFun, destTex.TextureFormat);
            if (fromGlobal) chunkSource.GlobalRm2Dirty = true;
            mat.Albedo.UpdateData(rgba, (uint)dw, (uint)dh);

            _browser.Log($"Import From Level: copied texture {sourceId:X8} ({sw}x{sh}) from '{levelPath}' → " +
                          $"{destId:X8} ({dw}x{dh}), format {destTex.TextureFormat}" +
                          (dw != sw || dh != sh ? "  [resized to match the destination's own slot]" : "") +
                          (fromGlobal ? "  [shared/global object — will also write Startup\\Default.rm2 on Save Chunk]" : ""));

            if (sourceIsScenery && destIsScenery)
            {
                var srcMeshId = FindSceneryMeshIdUsingTexture(srcGfx, sourceId);
                if (srcMeshId is null)
                {
                    _browser.Log("Import From Level: texture copied, but no scenery mesh in that level " +
                                  "uses it — couldn't also pull along vertex colors.");
                }
                else
                {
                    var liveTargets = FindLiveSceneryTilesUsingTexture(chunkSource, destId);
                    if (liveTargets.Count > 0)
                        CopyVertexColorsFromLevel(liveTargets, srcMeshId.Value, levelPath);
                    else
                        _browser.Log("Import From Level: texture copied, but couldn't find any live " +
                                      "scenery tile using it to also copy vertex colors onto.");
                }
            }
        }
        catch (Exception ex) { _browser.Log($"Import From Level failed: {ex.Message}"); }
    }

    private List<Entity> FindLiveSceneryTilesUsingTexture(ChunkSource chunkSource, uint textureId)
    {
        var result = new List<Entity>();
        if (chunkSource.Sm2 is null) return result;

        var gfx = chunkSource.Sm2.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION);
        var meshSec = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_MESHES_SECTION);
        var matSec  = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_MATERIALS_SECTION);
        if (meshSec is null || matSec is null) return result;

        var meshIdsUsingTexture = new HashSet<uint>();
        for (int i = 0; i < meshSec.GetItemsAmount(); i++)
        {
            if (meshSec.GetItem(i) is not Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyRigidModel rm) continue;
            if (meshSec.GetItem(i) is not BaseTwinItem bi) continue;
            foreach (var matId in rm.Materials)
            {
                if (matSec.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyMaterial>(matId)
                    is not { } mat) continue;
                if (mat.Shaders.Any(sh => sh.TextureId == textureId)) { meshIdsUsingTexture.Add(bi.GetID()); break; }
            }
        }
        if (meshIdsUsingTexture.Count == 0) return result;

        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        if (chunkRoot is null) return result;

        foreach (var meshChild in AllEntities(chunkRoot).Where(x => x.Has<CrashEngine.Importer.MeshRenderer>()))
        {
            var tile = meshChild.Parent?.Get<SceneryTile>();
            if (tile is null) continue;
            if (ResolveSceneryMeshIds(gfx, tile.SourceId, tile.IsLod).Any(meshIdsUsingTexture.Contains))
                result.Add(meshChild);
        }
        return result;
    }

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

    private bool   _showConditionPicker;
    private Entity? _conditionPickerTargetEntity;
    private string _conditionPickerFilter = "";

    private List<(Entity Entity, string Kind, uint SourceId)> GetConditionSources(uint targetInstanceId, Entity excludeSelf)
    {
        var result = new List<(Entity, string, uint)>();
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        if (chunkRoot is null) return result;
        ushort target = (ushort)targetInstanceId;
        foreach (var cand in AllEntities(chunkRoot))
        {
            if (cand == excludeSelf) continue;
            if (cand.Get<InstanceData>() is { } candInst && candInst.Source.Instances.Contains(target))
                result.Add((cand, "Instance", candInst.Source.GetID()));
            else if (cand.Get<CrashEngine.Importer.TriggerMarker>() is { } candTrig &&
                     candTrig.Source.Trigger.Instances.Contains(target))
                result.Add((cand, "Trigger", candTrig.Source.GetID()));
        }
        return result;
    }

    private void DrawConditionPickerWindow()
    {
        if (!_showConditionPicker) return;
        var targetEnt = _conditionPickerTargetEntity;
        var targetInst = targetEnt?.Get<InstanceData>();
        if (targetEnt is null || targetInst is null) { _showConditionPicker = false; return; }
        uint targetId = targetInst.Source.GetID();

        ImGui.SetNextWindowSize(new Vector2(460f, 480f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin($"Add Condition For '{targetEnt.Name}'##condpicker", ref _showConditionPicker))
        {
            ImGui.End();
            return;
        }

        ImGui.TextDisabled($"Pick any Trigger or Instance in this level — it will message\n" +
                            $"'{targetEnt.Name}' once its own condition fires (adds this\n" +
                            "object's id to the PICKED one's own linked-instances list —\n" +
                            "same direction the real dock→door link uses). Yellow [ ] = not\n" +
                            "yet linked to this object; green [x] = already linked.");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##condPickerFilter", "Search...", ref _conditionPickerFilter, 128);

        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        ImGui.BeginChild("##condPickerList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        if (chunkRoot is not null)
        {
            int idx = 0;
            ushort targetU16 = (ushort)targetId;
            foreach (var cand in AllEntities(chunkRoot))
            {
                if (cand == targetEnt) continue;

                string kind; uint candId; bool already;
                if (cand.Get<InstanceData>() is { } candInst)
                {
                    kind = "Instance"; candId = candInst.Source.GetID();
                    already = candInst.Source.Instances.Contains(targetU16);
                }
                else if (cand.Get<CrashEngine.Importer.TriggerMarker>() is { } candTrig)
                {
                    kind = "Trigger"; candId = candTrig.Source.GetID();
                    already = candTrig.Source.Trigger.Instances.Contains(targetU16);
                }
                else continue;

                if (!string.IsNullOrWhiteSpace(_conditionPickerFilter) &&
                    !cand.Name.Contains(_conditionPickerFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                ImGui.PushID(idx++);
                ImGui.TextColored(already ? new Vector4(0.5f, 1f, 0.5f, 1f) : new Vector4(1f, 0.85f, 0.2f, 1f),
                                   already ? "[x]" : "[ ]");
                ImGui.SameLine();
                if (ImGui.Selectable($"[{kind}] {cand.Name}  (0x{candId:X4})"))
                {
                    if (!already)
                    {
                        if (cand.Get<InstanceData>() is { } ci) ci.Source.Instances.Add(targetU16);
                        else if (cand.Get<CrashEngine.Importer.TriggerMarker>() is { } ct) ct.Source.Trigger.Instances.Add(targetU16);
                        _browser.Log($"Conditions: '{cand.Name}' will now message '{targetEnt.Name}' once its " +
                                      "own condition fires (added to its own linked-instances list). " +
                                      "Save Chunk to persist, then test in an emulator — not verified live yet.");
                    }
                    _showConditionPicker = false;
                }
                ImGui.PopID();
            }
        }
        ImGui.EndChild();
        ImGui.End();
    }

    private static uint? FindSceneryMeshIdUsingTexture(PS2AnyGraphicsSection? gfx, uint textureId)
    {
        var meshSec = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_MESHES_SECTION);
        var matSec  = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_MATERIALS_SECTION);
        if (meshSec is null || matSec is null) return null;

        for (int i = 0; i < meshSec.GetItemsAmount(); i++)
        {
            if (meshSec.GetItem(i) is not Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyRigidModel rm) continue;
            if (meshSec.GetItem(i) is not BaseTwinItem bi) continue;
            foreach (var matId in rm.Materials)
            {
                if (matSec.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyMaterial>(matId)
                    is not { } mat) continue;
                foreach (var sh in mat.Shaders)
                    if (sh.TextureId == textureId) return bi.GetID();
            }
        }
        return null;
    }

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

    private static Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyModel? ResolveSceneryModel(
        PS2AnyGraphicsSection? gfx, uint meshId)
    {
        var meshSec = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_MESHES_SECTION);
        if (meshSec?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyRigidModel>(meshId) is not { } rm)
            return null;
        var modelSec = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_MODELS_SECTION);
        return modelSec?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyModel>(rm.Model);
    }

    private static uint? ResolveSceneryMeshId(PS2AnyGraphicsSection? gfx, uint sourceId, bool isLod)
    {
        if (!isLod) return sourceId;
        var lodSec = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_LODS_SECTION);
        if (lodSec?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyLOD>(sourceId) is not { } lod
            || lod.Meshes.Count == 0)
            return null;
        return lod.Meshes[0];
    }

    private static List<uint> ResolveSceneryMeshIds(PS2AnyGraphicsSection? gfx, uint sourceId, bool isLod)
    {
        if (!isLod) return new List<uint> { sourceId };
        var lodSec = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_LODS_SECTION);
        if (lodSec?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyLOD>(sourceId) is not { } lod)
            return new List<uint>();
        return lod.Meshes.Distinct().ToList();
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

    private bool   _showCrashPicker;
    private string _crashPickerFilter = "";
    private bool   _showScriptsBrowser;
    private string _scriptsFilter = "";
    private void DrawCrashPickerWindow()
    {
        if (!_showCrashPicker) return;
        ImGui.SetNextWindowSize(new Vector2(480f, 420f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Add Crash (full, with scripts)##crashpicker", ref _showCrashPicker))
        {
            ImGui.End();
            return;
        }
        ImGui.TextDisabled("Pick a level that already has a real, working Crash to copy from\n" +
                            "(confirmed identical between beach/gpa09 — any real level works).");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##crashPickerFilter", "Search levels...", ref _crashPickerFilter, 128);
        ImGui.BeginChild("##crashPickerList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        foreach (var lvl in GetSwapLevelList())
        {
            if (!string.IsNullOrWhiteSpace(_crashPickerFilter) &&
                !lvl.Contains(_crashPickerFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (ImGui.Selectable(lvl))
            {
                AddFullCrashTransplant(lvl);
                _showCrashPicker = false;
            }
        }
        ImGui.EndChild();
        ImGui.End();
    }

    private void DrawScriptsBrowserWindow()
    {
        if (!_showScriptsBrowser) return;
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        ImGui.SetNextWindowSize(new Vector2(560f, 480f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Scripts (CODE_BEHAVIOURS_SECTION)##scriptsbrowser", ref _showScriptsBrowser))
        {
            ImGui.End();
            return;
        }
        if (chunkSource?.Rm2 is null) { ImGui.TextDisabled("No level loaded."); ImGui.End(); return; }
        var code   = chunkSource.Rm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION);
        var behSec = code?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
        if (behSec is null) { ImGui.TextDisabled("This chunk has no CODE_BEHAVIOURS_SECTION."); ImGui.End(); return; }

        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##scriptsFilter", "Search by name or hex id...", ref _scriptsFilter, 128);
        ImGui.BeginChild("##scriptsList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        for (int i = 0; i < behSec.GetItemsAmount(); i++)
        {
            var item = behSec.GetItem(i);
            var id = item.GetID();
            string label = item is PS2BehaviourGraph g && !string.IsNullOrEmpty(g.Name)
                ? g.Name : (item is TwinBehaviourStarter ? "(starter, no name)" : "(unnamed)");
            string typeStr = item is TwinBehaviourStarter ? "Starter" : item is PS2BehaviourGraph ? "Graph" : item.GetType().Name;
            if (!string.IsNullOrWhiteSpace(_scriptsFilter) &&
                !label.Contains(_scriptsFilter, StringComparison.OrdinalIgnoreCase) &&
                !$"{id:X4}".Contains(_scriptsFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            ImGui.Text($"0x{id:X4}  [{typeStr,-7}]  {label}");
        }
        ImGui.EndChild();
        ImGui.End();
    }

    private void AutoAddCrashFromTemplate(string sourceLevelPath)
    {
        var chunkSource = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
        if (chunkSource?.Rm2 is null) { _browser.Log("New Scene: can't auto-add Crash -- no chunk loaded."); return; }
        if (HasCrashInstance(chunkSource.Rm2)) { _browser.Log("New Scene: Crash already present -- auto-add skipped."); return; }
        AddFullCrashTransplant(sourceLevelPath, System.Numerics.Vector3.Zero);
        if (HasCrashInstance(chunkSource.Rm2))
        {
            SaveChunk();
            _browser.Log("New Scene: auto-transplanted Crash from beach at the origin and saved. The scene is ready to build/edit.");
        }
    }

    private static bool HasCrashInstance(PS2AnyTwinsanityRM2 rm2)
    {
        for (uint lid = (uint)TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= (uint)TwinConstants.LEVEL_LAYOUT_8_SECTION; lid++)
        {
            var inst = rm2.GetItem<BaseTwinSection>(lid)?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
            if (inst is null) continue;
            for (int i = 0; i < inst.GetItemsAmount(); i++)
                if (inst.GetItem(i) is PS2AnyInstance pi && pi.StateFlags == 0x7D2E) return true;
        }
        return false;
    }

    private void AddFullCrashTransplant(string sourceLevelPath, System.Numerics.Vector3? spawnOverride = null)
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkRoot is null || chunkSource?.Rm2 is null) { _browser.Log("Add Crash: no level currently loaded."); return; }

        var basePath = sourceLevelPath.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase)
            ? sourceLevelPath[..^4] : sourceLevelPath;

        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            using var stream = pkg.OpenByPath($"{basePath}.rm2")
                ?? throw new FileNotFoundException($"'{basePath}.rm2' not found in the package.");
            var sourceRm2 = new PS2AnyTwinsanityRM2();
            using (var reader = new BinaryReader(stream))
                sourceRm2.Read(reader, (int)stream.Length);

            var rec = MeshDecoder.TransplantObjectFull(Engine.Instance.GL, chunkSource.Rm2, chunkSource.MeshTables,
                chunkSource.TexCache, sourceRm2, 0x0000, out string log);
            if (rec is null) { _browser.Log($"Add Crash: {log}"); return; }
            chunkSource.FullTransplants[rec.RootObjectId] = rec;

            var section = AllEntities(chunkRoot).Select(e => e.Get<InstanceData>()?.Section).FirstOrDefault(s => s is not null);
            if (section is null)
            {
                var layout = chunkSource.Rm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_LAYOUT_1_SECTION);
                section = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
                if (section is null) { _browser.Log("Add Crash: this level has no instances section to add to."); return; }
            }

            PS2AnyInstance? srcInst = null;
            for (int lid = 0; lid <= 7 && srcInst is null; lid++)
            {
                var srcLayout = sourceRm2.GetItem<BaseTwinSection>((uint)lid);
                var srcInstSec = srcLayout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
                if (srcInstSec is null) continue;
                for (int i = 0; i < srcInstSec.GetItemsAmount(); i++)
                {
                    var cand = (PS2AnyInstance)srcInstSec.GetItem(i);
                    if (cand.ObjectId == 0x0000) { srcInst = cand; break; }
                }
            }

            var srcPositions = srcInst?.Positions ?? new List<ushort>();
            var srcPaths     = srcInst?.Paths ?? new List<ushort>();
            var copiedPositions = MeshDecoder.CopyInstanceLayoutRefs<PS2AnyPosition>(
                sourceRm2, chunkSource.Rm2, section, srcPositions,
                (int)TwinConstants.LAYOUT_POSITIONS_SECTION, out int posNotFound);
            var copiedPaths = MeshDecoder.CopyInstanceLayoutRefs<PS2AnyPath>(
                sourceRm2, chunkSource.Rm2, section, srcPaths,
                (int)TwinConstants.LAYOUT_PATHS_SECTION, out int pathNotFound);
            EnsurePositionMarkersExist(chunkRoot, chunkSource.Rm2, srcPositions);

            var spawnPos = spawnOverride ?? _camera?.Transform.Position ?? Vector3.Zero;
            var inst = new PS2AnyInstance
            {
                Position              = new TwinVec4(spawnPos.X, spawnPos.Y, spawnPos.Z, 1f),
                RotationX             = new TwinIntegerRotation(),
                RotationY             = new TwinIntegerRotation(),
                RotationZ             = new TwinIntegerRotation(),
                ObjectId              = (ushort)rec.RootObjectId,
                RefListIndex          = srcInst?.RefListIndex ?? -1,
                OnSpawnHeaderScriptID = srcInst?.OnSpawnHeaderScriptID ?? 0xFFFF,
                StateFlags            = srcInst?.StateFlags ?? 0,
                InstancesRelated      = 0, Instances = new List<ushort>(),
                PositionsRelated      = srcInst?.PositionsRelated ?? 0, Positions = copiedPositions,
                PathsRelated          = srcInst?.PathsRelated ?? 0,     Paths     = copiedPaths,
                ParamList1            = srcInst is not null ? new List<uint>(srcInst.ParamList1) : new List<uint>(),
                ParamList2            = srcInst is not null ? new List<float>(srcInst.ParamList2) : new List<float>(),
                ParamList3            = srcInst is not null ? new List<uint>(srcInst.ParamList3) : new List<uint>(),
            };
            if (srcInst is not null && srcInst.Instances.Count > 0)
                _browser.Log($"Add Crash: source instance also references {srcInst.Instances.Count} " +
                              "other placed Instance id(s) — these are NOT copied/remapped (left empty); " +
                              "each would itself need its own full object transplant, out of scope here.");
            if (posNotFound > 0)
                _browser.Log($"Add Crash: {posNotFound} of {srcPositions.Count} referenced Position(s) not found in the source level — skipped.");
            if (pathNotFound > 0)
                _browser.Log($"Add Crash: {pathNotFound} of {srcPaths.Count} referenced Path(s) not found in the source level — skipped.");
            if (copiedPositions.Count > 0 || copiedPaths.Count > 0)
                _browser.Log($"Add Crash: copied {copiedPositions.Count} Position(s) and {copiedPaths.Count} Path(s) referenced by the source instance.");
            var instRoot = chunkRoot.Children.FirstOrDefault(c => c.Name == "Instances") ?? chunkRoot;
            var newEntity = AddOrReuseInstance(section, inst, instRoot, chunkSource);

            SelectClicked(newEntity, false);
            _browser.Log($"Add Crash: {log} Placed at the camera position. Not on the Undo stack yet, but " +
                          "Delete Selected now fully removes it (object graph + OGIs/Animations/Behaviours/" +
                          "Sounds/graphics) once its last instance is gone — nothing orphaned. Save Chunk " +
                          "to keep it, then Build ISO + test.");

            const uint TextMasterId = 0x0332;
            var srcObjSecForTm = sourceRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
            if (srcObjSecForTm is not null && srcObjSecForTm.ContainsItem(TextMasterId) &&
                !chunkSource.Rm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)!
                    .GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION)!.ContainsItem(TextMasterId))
            {
                var tmRec = MeshDecoder.TransplantObjectFull(Engine.Instance.GL, chunkSource.Rm2, chunkSource.MeshTables,
                    chunkSource.TexCache, sourceRm2, TextMasterId, out string tmLog);
                if (tmRec is not null)
                {
                    chunkSource.FullTransplants[tmRec.RootObjectId] = tmRec;
                    PS2AnyInstance? tmSrcInst = null;
                    for (int lid = 0; lid <= 7 && tmSrcInst is null; lid++)
                    {
                        var srcLayout = sourceRm2.GetItem<BaseTwinSection>((uint)lid);
                        var srcInstSec = srcLayout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
                        if (srcInstSec is null) continue;
                        for (int i = 0; i < srcInstSec.GetItemsAmount(); i++)
                        {
                            var cand = (PS2AnyInstance)srcInstSec.GetItem(i);
                            if (cand.ObjectId == TextMasterId) { tmSrcInst = cand; break; }
                        }
                    }
                    var tmInst = new PS2AnyInstance
                    {
                        Position              = tmSrcInst?.Position ?? new TwinVec4(spawnPos.X, spawnPos.Y, spawnPos.Z, 1f),
                        RotationX             = new TwinIntegerRotation(),
                        RotationY             = new TwinIntegerRotation(),
                        RotationZ             = new TwinIntegerRotation(),
                        ObjectId              = (ushort)tmRec.RootObjectId,
                        RefListIndex          = tmSrcInst?.RefListIndex ?? -1,
                        OnSpawnHeaderScriptID = tmSrcInst?.OnSpawnHeaderScriptID ?? 0xFFFF,
                        StateFlags            = tmSrcInst?.StateFlags ?? 0,
                        InstancesRelated      = 0, Instances = new List<ushort>(),
                        PositionsRelated      = 0, Positions = new List<ushort>(),
                        PathsRelated          = 0, Paths     = new List<ushort>(),
                        ParamList1            = tmSrcInst is not null ? new List<uint>(tmSrcInst.ParamList1) : new List<uint>(),
                        ParamList2            = tmSrcInst is not null ? new List<float>(tmSrcInst.ParamList2) : new List<float>(),
                        ParamList3            = tmSrcInst is not null ? new List<uint>(tmSrcInst.ParamList3) : new List<uint>(),
                    };
                    var tmEntity = AddOrReuseInstance(section, tmInst, instRoot, chunkSource);
                    _browser.Log($"Add Crash: also added act_UTIL_TEXTMASTER ({tmLog})");
                }
                else _browser.Log($"Add Crash: act_UTIL_TEXTMASTER transplant failed: {tmLog}");
            }
        }
        catch (Exception ex) { _browser.Log($"Add Crash failed: {ex.Message}"); }
    }

    private bool                     _showObjectTransplantLevelPicker;
    private string                   _objectTransplantLevelFilter = "";
    private bool                     _showObjectTransplantObjectPicker;
    private string                   _objectTransplantSelectedLevel = "";
    private string                   _objectTransplantObjectFilter = "";
    private List<(uint Id, string Name, ITwinObject.ObjectType Type)>? _objectTransplantObjectList;

    private void DrawObjectTransplantLevelPickerWindow()
    {
        if (!_showObjectTransplantLevelPicker) return;
        ImGui.SetNextWindowSize(new Vector2(480f, 420f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Add Object: pick a source level##objtransplantlevel", ref _showObjectTransplantLevelPicker))
        {
            ImGui.End();
            return;
        }
        ImGui.TextDisabled("Pick the level to copy an object from (full script/OGI/animation/sound transplant).");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##objTransplantLevelFilter", "Search levels...", ref _objectTransplantLevelFilter, 128);
        ImGui.BeginChild("##objTransplantLevelList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        foreach (var lvl in GetSwapLevelList())
        {
            if (!string.IsNullOrWhiteSpace(_objectTransplantLevelFilter) &&
                !lvl.Contains(_objectTransplantLevelFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (ImGui.Selectable(lvl))
            {
                _objectTransplantSelectedLevel = lvl;
                _objectTransplantObjectList = LoadObjectListForLevel(lvl);
                _showObjectTransplantLevelPicker = false;
                _showObjectTransplantObjectPicker = true;
            }
        }
        ImGui.EndChild();
        ImGui.End();
    }

    private List<(uint Id, string Name, ITwinObject.ObjectType Type)> LoadObjectListForLevel(string levelPath)
    {
        var result = new List<(uint, string, ITwinObject.ObjectType)>();
        var seenIds = new HashSet<uint>();
        try
        {
            var basePath = levelPath.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? levelPath[..^4] : levelPath;
            using var pkg = PackageReader.Open(_extractedRoot);
            using var stream = pkg.OpenByPath($"{basePath}.rm2");
            if (stream is not null)
            {
                var srcRm2 = new PS2AnyTwinsanityRM2();
                using (var reader = new BinaryReader(stream)) srcRm2.Read(reader, (int)stream.Length);
                var objSec = srcRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                    ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
                if (objSec is not null)
                {
                    for (int i = 0; i < objSec.GetItemsAmount(); i++)
                    {
                        var obj = (PS2AnyObject)objSec.GetItem(i);
                        if (seenIds.Add(obj.GetID())) result.Add((obj.GetID(), obj.Name, obj.Type));
                    }
                }
            }

            using var globalStream = pkg.OpenByPath(@"Startup\Default.rm2");
            if (globalStream is not null)
            {
                var globalRm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default();
                using (var reader = new BinaryReader(globalStream)) globalRm2.Read(reader, (int)globalStream.Length);
                var gObjSec = globalRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                    ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
                if (gObjSec is not null)
                {
                    for (int i = 0; i < gObjSec.GetItemsAmount(); i++)
                    {
                        var obj = (PS2AnyObject)gObjSec.GetItem(i);
                        if (seenIds.Add(obj.GetID())) result.Add((obj.GetID(), obj.Name, obj.Type));
                    }
                }
            }
        }
        catch (Exception ex) { _browser.Log($"Add Object: failed to list objects in {levelPath}: {ex.Message}"); }
        return result;
    }

    private void DrawObjectTransplantObjectPickerWindow()
    {
        if (!_showObjectTransplantObjectPicker) return;
        ImGui.SetNextWindowSize(new Vector2(560f, 460f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin($"Add Object: pick an object from {_objectTransplantSelectedLevel}##objtransplantobj", ref _showObjectTransplantObjectPicker))
        {
            ImGui.End();
            return;
        }
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##objTransplantObjFilter", "Search by name or hex id...", ref _objectTransplantObjectFilter, 128);
        ImGui.BeginChild("##objTransplantObjList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        foreach (var (id, name, type) in _objectTransplantObjectList ?? Enumerable.Empty<(uint, string, ITwinObject.ObjectType)>())
        {
            if (!string.IsNullOrWhiteSpace(_objectTransplantObjectFilter) &&
                !name.Contains(_objectTransplantObjectFilter, StringComparison.OrdinalIgnoreCase) &&
                !$"{id:X4}".Contains(_objectTransplantObjectFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (ImGui.Selectable($"0x{id:X4}  {name}  ({type})"))
            {
                AddFullObjectTransplant(_objectTransplantSelectedLevel, id, name);
                _showObjectTransplantObjectPicker = false;
            }
        }
        ImGui.EndChild();
        ImGui.End();
    }

    private void AddFullObjectTransplant(string sourceLevelPath, uint objectId, string objectName)
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkRoot is null || chunkSource?.Rm2 is null) { _browser.Log("Add Object: no level currently loaded."); return; }

        var basePath = sourceLevelPath.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase)
            ? sourceLevelPath[..^4] : sourceLevelPath;

        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            using var stream = pkg.OpenByPath($"{basePath}.rm2")
                ?? throw new FileNotFoundException($"'{basePath}.rm2' not found in the package.");
            var sourceRm2 = new PS2AnyTwinsanityRM2();
            using (var reader = new BinaryReader(stream))
                sourceRm2.Read(reader, (int)stream.Length);

            PS2AnyTwinsanityRM2 effectiveSourceRm2 = sourceRm2;
            var localObjSecCheck = sourceRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
            if (localObjSecCheck?.GetItem<PS2AnyObject>(objectId) is null)
            {
                using var globalStream = pkg.OpenByPath(@"Startup\Default.rm2");
                if (globalStream is not null)
                {
                    var globalRm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default();
                    using (var reader = new BinaryReader(globalStream)) globalRm2.Read(reader, (int)globalStream.Length);
                    var gObjSecCheck = globalRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                        ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
                    if (gObjSecCheck?.GetItem<PS2AnyObject>(objectId) is not null)
                        effectiveSourceRm2 = globalRm2;
                }
            }

            var rec = MeshDecoder.TransplantObjectFull(Engine.Instance.GL, chunkSource.Rm2, chunkSource.MeshTables,
                chunkSource.TexCache, effectiveSourceRm2, objectId, out string log);
            if (rec is null) { _browser.Log($"Add Object: {log}"); return; }
            chunkSource.FullTransplants[rec.RootObjectId] = rec;

            var section = AllEntities(chunkRoot).Select(e => e.Get<InstanceData>()?.Section).FirstOrDefault(s => s is not null);
            if (section is null)
            {
                var layout = chunkSource.Rm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_LAYOUT_1_SECTION);
                section = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
                if (section is null) { _browser.Log("Add Object: this level has no instances section to add to."); return; }
            }

            PS2AnyInstance? srcInst = null;
            for (int lid = 0; lid <= 7 && srcInst is null; lid++)
            {
                var srcLayout = sourceRm2.GetItem<BaseTwinSection>((uint)lid);
                var srcInstSec = srcLayout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
                if (srcInstSec is null) continue;
                for (int i = 0; i < srcInstSec.GetItemsAmount(); i++)
                {
                    var cand = (PS2AnyInstance)srcInstSec.GetItem(i);
                    if (cand.ObjectId == (ushort)objectId) { srcInst = cand; break; }
                }
            }

            var srcPositions = srcInst?.Positions ?? new List<ushort>();
            var srcPaths     = srcInst?.Paths ?? new List<ushort>();
            var copiedPositions = MeshDecoder.CopyInstanceLayoutRefs<PS2AnyPosition>(
                sourceRm2, chunkSource.Rm2, section, srcPositions,
                (int)TwinConstants.LAYOUT_POSITIONS_SECTION, out int posNotFound);
            var copiedPaths = MeshDecoder.CopyInstanceLayoutRefs<PS2AnyPath>(
                sourceRm2, chunkSource.Rm2, section, srcPaths,
                (int)TwinConstants.LAYOUT_PATHS_SECTION, out int pathNotFound);
            EnsurePositionMarkersExist(chunkRoot, chunkSource.Rm2, srcPositions);

            var spawnPos = _camera?.Transform.Position ?? Vector3.Zero;
            var inst = new PS2AnyInstance
            {
                Position              = new TwinVec4(spawnPos.X, spawnPos.Y, spawnPos.Z, 1f),
                RotationX             = new TwinIntegerRotation(),
                RotationY             = new TwinIntegerRotation(),
                RotationZ             = new TwinIntegerRotation(),
                ObjectId              = (ushort)rec.RootObjectId,
                RefListIndex          = srcInst?.RefListIndex ?? -1,
                OnSpawnHeaderScriptID = srcInst?.OnSpawnHeaderScriptID ?? 0xFFFF,
                StateFlags            = srcInst?.StateFlags ?? 0,
                InstancesRelated      = 0, Instances = new List<ushort>(),
                PositionsRelated      = srcInst?.PositionsRelated ?? 0, Positions = copiedPositions,
                PathsRelated          = srcInst?.PathsRelated ?? 0,     Paths     = copiedPaths,
                ParamList1            = srcInst is not null ? new List<uint>(srcInst.ParamList1) : new List<uint>(),
                ParamList2            = srcInst is not null ? new List<float>(srcInst.ParamList2) : new List<float>(),
                ParamList3            = srcInst is not null ? new List<uint>(srcInst.ParamList3) : new List<uint>(),
            };
            if (srcInst is not null && srcInst.Instances.Count > 0)
                _browser.Log($"Add Object: source instance also references {srcInst.Instances.Count} " +
                              "other placed Instance id(s) — these are NOT copied/remapped (left empty); " +
                              "each would itself need its own full object transplant, out of scope here.");
            if (posNotFound > 0)
                _browser.Log($"Add Object: {posNotFound} of {srcPositions.Count} referenced Position(s) not found in the source level — skipped.");
            if (pathNotFound > 0)
                _browser.Log($"Add Object: {pathNotFound} of {srcPaths.Count} referenced Path(s) not found in the source level — skipped.");
            if (copiedPositions.Count > 0 || copiedPaths.Count > 0)
                _browser.Log($"Add Object: copied {copiedPositions.Count} Position(s) and {copiedPaths.Count} Path(s) referenced by the source instance.");
            var instRoot = chunkRoot.Children.FirstOrDefault(c => c.Name == "Instances") ?? chunkRoot;
            var newEntity = AddOrReuseInstance(section, inst, instRoot, chunkSource);

            _lastAddedInstanceId = (ushort?)newEntity.Get<InstanceData>()?.Source?.GetID();

            SelectClicked(newEntity, false);
            _browser.Log($"Add Object: {log} '{objectName}' placed at the camera position. Not on the Undo " +
                          "stack yet, but Delete Selected now fully removes it (object graph + OGIs/" +
                          "Animations/Behaviours/Sounds/graphics) once its last instance is gone. " +
                          "Save Chunk to keep it, then Build ISO + test.");
        }
        catch (Exception ex) { _browser.Log($"Add Object failed: {ex.Message}"); }
    }

    private bool                       _showTriggerTransplantLevelPicker;
    private string                     _triggerTransplantLevelFilter = "";
    private bool                       _showTriggerTransplantPicker;
    private string                     _triggerTransplantSelectedLevel = "";
    private string                     _triggerTransplantFilter = "";
    private List<(uint Id, string Summary)>? _triggerTransplantList;
    private ushort?                    _lastAddedInstanceId;

    private void DrawTriggerTransplantLevelPickerWindow()
    {
        if (!_showTriggerTransplantLevelPicker) return;
        ImGui.SetNextWindowSize(new Vector2(480f, 420f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Add Trigger: pick a source level##trigtransplantlevel", ref _showTriggerTransplantLevelPicker))
        {
            ImGui.End();
            return;
        }
        ImGui.TextDisabled("Pick the level to copy a trigger from.");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##trigTransplantLevelFilter", "Search levels...", ref _triggerTransplantLevelFilter, 128);
        ImGui.BeginChild("##trigTransplantLevelList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        foreach (var lvl in GetSwapLevelList())
        {
            if (!string.IsNullOrWhiteSpace(_triggerTransplantLevelFilter) &&
                !lvl.Contains(_triggerTransplantLevelFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (ImGui.Selectable(lvl))
            {
                _triggerTransplantSelectedLevel = lvl;
                _triggerTransplantList = LoadTriggerListForLevel(lvl);
                _showTriggerTransplantLevelPicker = false;
                _showTriggerTransplantPicker = true;
            }
        }
        ImGui.EndChild();
        ImGui.End();
    }

    private List<(uint Id, string Summary)> LoadTriggerListForLevel(string levelPath)
    {
        var result = new List<(uint, string)>();
        try
        {
            var basePath = levelPath.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? levelPath[..^4] : levelPath;
            using var pkg = PackageReader.Open(_extractedRoot);
            using var stream = pkg.OpenByPath($"{basePath}.rm2");
            if (stream is null) return result;
            var srcRm2 = new PS2AnyTwinsanityRM2();
            using (var reader = new BinaryReader(stream)) srcRm2.Read(reader, (int)stream.Length);
            for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION; lid++)
            {
                var layout = srcRm2.GetItem<BaseTwinSection>((uint)lid);
                var trigSec = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_TRIGGERS_SECTION);
                if (trigSec is null) continue;
                for (int i = 0; i < trigSec.GetItemsAmount(); i++)
                {
                    if (trigSec.GetItem(i) is not PS2AnyTrigger t) continue;
                    var msgs = string.Join(",", t.TriggerMessages.Where(m => m != 0));
                    var instRefs = t.Trigger.Instances.Count > 0 ? $" watches:{t.Trigger.Instances.Count} inst" : "";
                    result.Add((t.GetID(), $"msgs:[{(msgs.Length > 0 ? msgs : "none")}]{instRefs}"));
                }
            }
        }
        catch (Exception ex) { _browser.Log($"Add Trigger: failed to list triggers in {levelPath}: {ex.Message}"); }
        return result;
    }

    private void DrawTriggerTransplantPickerWindow()
    {
        if (!_showTriggerTransplantPicker) return;
        ImGui.SetNextWindowSize(new Vector2(560f, 460f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin($"Add Trigger: pick a trigger from {_triggerTransplantSelectedLevel}##trigtransplantpick", ref _showTriggerTransplantPicker))
        {
            ImGui.End();
            return;
        }
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##trigTransplantFilter", "Search by hex id or message id...", ref _triggerTransplantFilter, 128);
        ImGui.BeginChild("##trigTransplantList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        foreach (var (id, summary) in _triggerTransplantList ?? Enumerable.Empty<(uint, string)>())
        {
            if (!string.IsNullOrWhiteSpace(_triggerTransplantFilter) &&
                !summary.Contains(_triggerTransplantFilter, StringComparison.OrdinalIgnoreCase) &&
                !$"{id:X4}".Contains(_triggerTransplantFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (ImGui.Selectable($"Trigger 0x{id:X4}  {summary}"))
            {
                AddTriggerTransplant(_triggerTransplantSelectedLevel, id);
                _showTriggerTransplantPicker = false;
            }
        }
        ImGui.EndChild();
        ImGui.End();
    }

    private void AddTriggerTransplant(string sourceLevelPath, uint triggerId)
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkRoot is null || chunkSource?.Rm2 is null) { _browser.Log("Add Trigger: no level currently loaded."); return; }

        var basePath = sourceLevelPath.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase)
            ? sourceLevelPath[..^4] : sourceLevelPath;

        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            using var stream = pkg.OpenByPath($"{basePath}.rm2")
                ?? throw new FileNotFoundException($"'{basePath}.rm2' not found in the package.");
            var sourceRm2 = new PS2AnyTwinsanityRM2();
            using (var reader = new BinaryReader(stream))
                sourceRm2.Read(reader, (int)stream.Length);

            PS2AnyTrigger? srcTrig = null;
            for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION && srcTrig is null; lid++)
            {
                var layout = sourceRm2.GetItem<BaseTwinSection>((uint)lid);
                var trigSec = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_TRIGGERS_SECTION);
                if (trigSec is null) continue;
                for (int i = 0; i < trigSec.GetItemsAmount(); i++)
                    if (trigSec.GetItem(i) is PS2AnyTrigger cand && cand.GetID() == triggerId) { srcTrig = cand; break; }
            }
            if (srcTrig is null) { _browser.Log($"Add Trigger: trigger 0x{triggerId:X4} not found in {sourceLevelPath}."); return; }

            BaseTwinSection? destTrigSection = null;
            BaseTwinSection? fallbackLayout = null;
            for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION; lid++)
            {
                var layout = chunkSource.Rm2.GetItem<BaseTwinSection>((uint)lid);
                if (layout is null) continue;
                if (layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION) is not null)
                    fallbackLayout = layout;
                var trigSec = layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_TRIGGERS_SECTION);
                if (trigSec is not null) { destTrigSection = trigSec; break; }
            }
            if (destTrigSection is null)
            {
                if (fallbackLayout is null) { _browser.Log("Add Trigger: this level has no layout section to add to."); return; }
                destTrigSection = new BaseTwinSection();
                destTrigSection.SetID((uint)TwinConstants.LAYOUT_TRIGGERS_SECTION);
                fallbackLayout.AddItem(destTrigSection);
                _browser.Log("Add Trigger: this level had no Triggers section at all — created one.");
            }

            var spawnPos = _camera?.Transform.Position ?? Vector3.Zero;
            var newTrig = new PS2AnyTrigger();
            newTrig.Trigger.Header              = srcTrig.Trigger.Header;
            newTrig.Trigger.ObjectActivatorMask = srcTrig.Trigger.ObjectActivatorMask;
            newTrig.Trigger.UnkFloat            = srcTrig.Trigger.UnkFloat;
            newTrig.Trigger.Rotation            = new TwinVec4(srcTrig.Trigger.Rotation.X, srcTrig.Trigger.Rotation.Y, srcTrig.Trigger.Rotation.Z, srcTrig.Trigger.Rotation.W);
            newTrig.Trigger.Scale               = new TwinVec4(srcTrig.Trigger.Scale.X, srcTrig.Trigger.Scale.Y, srcTrig.Trigger.Scale.Z, srcTrig.Trigger.Scale.W);
            newTrig.Trigger.Position            = new TwinVec4(spawnPos.X, spawnPos.Y, spawnPos.Z, 1f);
            newTrig.Trigger.InstanceExtensionValue = srcTrig.Trigger.InstanceExtensionValue;
            for (int i = 0; i < 4; i++) newTrig.TriggerMessages[i] = srcTrig.TriggerMessages[i];

            bool remapped = _lastAddedInstanceId.HasValue;
            if (remapped)
                newTrig.Trigger.Instances.Add(_lastAddedInstanceId!.Value);
            else
                foreach (var iid in srcTrig.Trigger.Instances) newTrig.Trigger.Instances.Add(iid);

            newTrig.SetID(GenerateUniqueInstanceId(destTrigSection));
            destTrigSection.AddItem(newTrig);

            if (_triggersRoot is not null)
            {
                var e = new Entity($"Trigger_{newTrig.GetID():X4}");
                e.Transform.Position = new Vector3(newTrig.Trigger.Position.X, newTrig.Trigger.Position.Y, newTrig.Trigger.Position.Z);
                e.Transform.Scale    = new Vector3(MathF.Max(newTrig.Trigger.Scale.X, 0.05f), MathF.Max(newTrig.Trigger.Scale.Y, 0.05f), MathF.Max(newTrig.Trigger.Scale.Z, 0.05f));
                var rdr = e.Add(new DirectCubeRenderer { Mesh = _cubeMesh, Color = new Vector4(0.55f, 0.8f, 1f, 0.35f) });
                rdr.Mat.AlphaBlend = true;
                e.Add(new CrashEngine.Importer.TriggerMarker { Source = newTrig });
                _triggersRoot.AddChild(e);
                _triggersRoot.Active = _showTriggers;
                SelectClicked(e, false);
            }

            var msgList = string.Join(",", newTrig.TriggerMessages.Where(m => m != 0));
            _browser.Log($"Add Trigger: copied trigger 0x{triggerId:X4} from {sourceLevelPath} → new id 0x{newTrig.GetID():X4}, " +
                         $"placed at the camera position, messages:[{(msgList.Length > 0 ? msgList : "none")}]. " +
                         (remapped
                             ? $"Instances list points at the object Add Object placed most recently (id 0x{_lastAddedInstanceId:X4})."
                             : "WARNING: no object was added via \"Add Object\" this session (or the link was lost) — " +
                               "the Instances list was copied AS-IS from the source level and almost certainly points " +
                               "at the wrong (or nonexistent) instance here. Use Add Object first, then Add Trigger.") +
                         " Save Chunk to keep it, then Build ISO + test.");
        }
        catch (Exception ex) { _browser.Log($"Add Trigger failed: {ex.Message}"); }
    }

    private void BakeExternalModel(string modelPath, bool flipX = true)
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkRoot is null || chunkSource?.Rm2 is null) { _browser.Log("Add Model: no level currently loaded."); return; }

        var raw = CrashEngine.Assets.ModelImporter.ExtractRawSubmeshes(modelPath);
        if (raw is null || raw.Count == 0) { _browser.Log($"Add Model: couldn't read any meshes from {Path.GetFileName(modelPath)}."); return; }

        if (flipX)
            foreach (var sm in raw)
            {
                for (int i = 0; i < sm.Positions.Length; i++)
                    sm.Positions[i] = new System.Numerics.Vector3(-sm.Positions[i].X, sm.Positions[i].Y, sm.Positions[i].Z);
                for (int i = 0; i < sm.Normals.Length; i++)
                    sm.Normals[i] = new System.Numerics.Vector3(-sm.Normals[i].X, sm.Normals[i].Y, sm.Normals[i].Z);
                for (int i = 0; i + 2 < sm.Indices.Length; i += 3)
                    (sm.Indices[i + 1], sm.Indices[i + 2]) = (sm.Indices[i + 2], sm.Indices[i + 1]);
            }

        try
        {
            var rec = MeshDecoder.BakeExternalModelAsObject(Engine.Instance.GL, chunkSource.Rm2, chunkSource.MeshTables!,
                chunkSource.TexCache, chunkSource.GlobalRm2, raw, Path.GetFileNameWithoutExtension(modelPath), out string log);
            if (rec is null) { _browser.Log($"Add Model: {log}"); return; }

            // Amedo 2026-09-20
            chunkSource.Transplants[rec.ObjectId] = MeshDecoder.ToTransplantRecord(rec);

            var section = AllEntities(chunkRoot).Select(e => e.Get<InstanceData>()?.Section).FirstOrDefault(s => s is not null);
            if (section is null)
            {
                var layout = chunkSource.Rm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_LAYOUT_1_SECTION);
                section = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
                if (section is null) { _browser.Log("Add Model: this level has no instances section to add to."); return; }
            }

            uint templateStateFlags = (uint)(
                Twinsanity.TwinsanityInterchange.Enumerations.Enums.InstanceState.Visible |
                Twinsanity.TwinsanityInterchange.Enumerations.Enums.InstanceState.ShadowActive);

            var spawnPos = _camera?.Transform.Position ?? Vector3.Zero;
            var inst = new PS2AnyInstance
            {
                Position              = new TwinVec4(spawnPos.X, spawnPos.Y, spawnPos.Z, 1f),
                RotationX             = new TwinIntegerRotation(),
                RotationY             = new TwinIntegerRotation(),
                RotationZ             = new TwinIntegerRotation(),
                ObjectId              = (ushort)rec.ObjectId,
                RefListIndex          = -1,
                OnSpawnHeaderScriptID = 0xFFFF,
                StateFlags            = templateStateFlags,
                InstancesRelated      = 0, Instances = new List<ushort>(),
                PositionsRelated      = 0, Positions = new List<ushort>(),
                PathsRelated          = 0, Paths     = new List<ushort>(),
                ParamList1            = new List<uint>(),
                ParamList2            = new List<float>(),
                ParamList3            = new List<uint>(),
            };

            var instRoot = chunkRoot.Children.FirstOrDefault(c => c.Name == "Instances") ?? chunkRoot;
            var newEntity = AddOrReuseInstance(section, inst, instRoot, chunkSource);

            SelectClicked(newEntity, false);
            _browser.Log($"Add Model: {log} Placed at the camera position " +
                          $"(StateFlags 0x{templateStateFlags:X}, fixed Visible+ShadowActive — independent of this level's own content). " +
                          "Not on the Undo stack yet, but Delete Selected removes it normally. Save Chunk to keep it, then Build ISO + test.");
        }
        catch (Exception ex) { _browser.Log($"Add Model failed: {ex.Message}"); }
    }

    private void BakeExternalModelAsScenery(string modelPath, bool flipX = true)
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        var sm2         = chunkSource?.Sm2;
        var sceneryRoot = chunkRoot is null ? null : AllEntities(chunkRoot).FirstOrDefault(x => x.Name == "Scenery");
        if (chunkRoot is null || sm2 is null || sceneryRoot is null)
        { _browser.Log("Add Scenery: this chunk has no Scenery (SM2) to add to."); return; }

        var scenery = sm2.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery>(
            (uint)TwinConstants.SCENERY_SECENERY_ITEM);
        if (scenery is null) { _browser.Log("Add Scenery: this chunk's SM2 has no Scenery item."); return; }

        var raw = CrashEngine.Assets.ModelImporter.ExtractRawSubmeshes(modelPath);
        if (raw is null || raw.Count == 0) { _browser.Log($"Add Scenery: couldn't read any meshes from {Path.GetFileName(modelPath)}."); return; }

        if (flipX)
            foreach (var sm in raw)
            {
                for (int i = 0; i < sm.Positions.Length; i++)
                    sm.Positions[i] = new Vector3(-sm.Positions[i].X, sm.Positions[i].Y, sm.Positions[i].Z);
                for (int i = 0; i < sm.Normals.Length; i++)
                    sm.Normals[i] = new Vector3(-sm.Normals[i].X, sm.Normals[i].Y, sm.Normals[i].Z);
                for (int i = 0; i + 2 < sm.Indices.Length; i += 3)
                    (sm.Indices[i + 1], sm.Indices[i + 2]) = (sm.Indices[i + 2], sm.Indices[i + 1]);
            }

        try
        {
            var rec = MeshDecoder.BakeExternalModelAsScenery(Engine.Instance.GL, sm2, chunkSource!.SceneryTexCache,
                chunkSource.GlobalRm2, raw, Path.GetFileNameWithoutExtension(modelPath), out string log);
            if (rec is null) { _browser.Log($"Add Scenery: {log}"); return; }

            var leaf = new Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryLeaf();
            for (int i = 0; i < rec.MeshIds.Count; i++)
            {
                leaf.MeshIDs.Add(rec.MeshIds[i]);
                leaf.MeshModelMatrices.Add(new TwinMat4
                {
                    Column1 = new TwinVec4(1f, 0f, 0f, 0f),
                    Column2 = new TwinVec4(0f, 1f, 0f, 0f),
                    Column3 = new TwinVec4(0f, 0f, 1f, 0f),
                    Column4 = new TwinVec4(0f, 0f, 0f, 1f),
                });
                leaf.BoundingBoxes.Add(rec.LocalBoxes[i]);
            }
            var lightTemplate = scenery.Sceneries.FirstOrDefault();
            if (lightTemplate is not null)
                Array.Copy(lightTemplate.LightsEnabler, leaf.LightsEnabler, leaf.LightsEnabler.Length);
            else
                for (int i = 0; i < leaf.LightsEnabler.Length; i++) leaf.LightsEnabler[i] = true;

            MeshDecoder.GraftIndependentSceneryLeaf(scenery, leaf, rec.CombinedMin, rec.CombinedMax);

            // Amedo 2026-09-20
            foreach (var mid in rec.MeshIds) chunkSource.SceneryBakes[mid] = rec;

            var preMoves = CaptureLiveSceneryTransforms(sceneryRoot);
            foreach (var child in sceneryRoot.Children.ToList())
                sceneryRoot.RemoveChild(child);
            chunkSource.SceneryTables = MeshDecoder.BuildSceneryMeshes(Engine.Instance.GL, sm2, sceneryRoot, chunkSource.SceneryTexCache, applyGlobalEffects: false);
            ReapplyLiveSceneryTransforms(sceneryRoot, preMoves);

            var newIds = new HashSet<uint>(rec.MeshIds);
            var justAdded = sceneryRoot.Children.FirstOrDefault(c =>
                c.Get<SceneryTile>() is { } t && newIds.Contains(t.SourceId));
            if (justAdded is not null) SelectClicked(justAdded, false);

            _browser.Log($"Add Scenery: {log} Placed at the world origin (0,0,0) as an independent scenery leaf. " +
                          "Not on the Undo stack, but Delete Selected removes it normally. Save Chunk to keep it, then Build ISO + test.");
        }
        catch (Exception ex) { _browser.Log($"Add Scenery failed: {ex.Message}"); }
    }

    private void AddRealCube()
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkRoot is null || chunkSource?.Rm2 is null) { _browser.Log("Add Cube: no level currently loaded."); return; }

        (Vector3 Normal, Vector3 A, Vector3 B, Vector3 C, Vector3 D)[] faces =
        {
            (new Vector3( 1, 0, 0), new Vector3(0.5f,-0.5f,-0.5f), new Vector3(0.5f, 0.5f,-0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.5f,-0.5f, 0.5f)),
            (new Vector3(-1, 0, 0), new Vector3(-0.5f,-0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f,-0.5f), new Vector3(-0.5f,-0.5f,-0.5f)),
            (new Vector3( 0, 1, 0), new Vector3(-0.5f, 0.5f,-0.5f), new Vector3(-0.5f, 0.5f, 0.5f), new Vector3( 0.5f, 0.5f, 0.5f), new Vector3( 0.5f, 0.5f,-0.5f)),
            (new Vector3( 0,-1, 0), new Vector3(-0.5f,-0.5f, 0.5f), new Vector3(-0.5f,-0.5f,-0.5f), new Vector3( 0.5f,-0.5f,-0.5f), new Vector3( 0.5f,-0.5f, 0.5f)),
            (new Vector3( 0, 0, 1), new Vector3(-0.5f,-0.5f, 0.5f), new Vector3( 0.5f,-0.5f, 0.5f), new Vector3( 0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)),
            (new Vector3( 0, 0,-1), new Vector3( 0.5f,-0.5f,-0.5f), new Vector3(-0.5f,-0.5f,-0.5f), new Vector3(-0.5f, 0.5f,-0.5f), new Vector3( 0.5f, 0.5f,-0.5f)),
        };

        var positions = new List<Vector3>();
        var normals   = new List<Vector3>();
        var uvs       = new List<Vector2>();
        var indices   = new List<uint>();
        foreach (var (n, a, b, c, d) in faces)
        {
            uint baseIdx = (uint)positions.Count;
            positions.Add(a); positions.Add(b); positions.Add(c); positions.Add(d);
            normals.Add(n); normals.Add(n); normals.Add(n); normals.Add(n);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
            indices.Add(baseIdx); indices.Add(baseIdx + 1); indices.Add(baseIdx + 2);
            indices.Add(baseIdx); indices.Add(baseIdx + 2); indices.Add(baseIdx + 3);
        }

        const int texSize = 32;
        var pixels = new byte[texSize * texSize * 4];
        for (int p = 0; p < texSize * texSize; p++)
        {
            pixels[p * 4 + 0] = 127; pixels[p * 4 + 1] = 127; pixels[p * 4 + 2] = 127; pixels[p * 4 + 3] = 255;
        }

        var raw = new List<CrashEngine.Assets.RawSubmesh>
        {
            new CrashEngine.Assets.RawSubmesh
            {
                Positions = positions.ToArray(), Normals = normals.ToArray(), UVs = uvs.ToArray(),
                Indices   = indices.ToArray(),
                DiffusePixelsRGBA = pixels, TexWidth = texSize, TexHeight = texSize,
                Name = "Cube",
                CeAlphaBlend = false,
                CeUnlit = false,
            },
        };

        try
        {
            var rec = MeshDecoder.BakeExternalModelAsObject(Engine.Instance.GL, chunkSource.Rm2, chunkSource.MeshTables!,
                chunkSource.TexCache, chunkSource.GlobalRm2, raw, "Cube", out string log);
            if (rec is null) { _browser.Log($"Add Cube: {log}"); return; }

            // Amedo 2026-09-20
            chunkSource.Transplants[rec.ObjectId] = MeshDecoder.ToTransplantRecord(rec);

            var section = AllEntities(chunkRoot).Select(e => e.Get<InstanceData>()?.Section).FirstOrDefault(s => s is not null);
            if (section is null)
            {
                var layout = chunkSource.Rm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_LAYOUT_1_SECTION);
                section = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
                if (section is null) { _browser.Log("Add Cube: this level has no instances section to add to."); return; }
            }

            uint templateStateFlags = (uint)(
                Twinsanity.TwinsanityInterchange.Enumerations.Enums.InstanceState.Visible |
                Twinsanity.TwinsanityInterchange.Enumerations.Enums.InstanceState.ShadowActive);

            var spawnPos = _camera?.Transform.Position ?? Vector3.Zero;
            var inst = new PS2AnyInstance
            {
                Position              = new TwinVec4(spawnPos.X, spawnPos.Y, spawnPos.Z, 1f),
                RotationX             = new TwinIntegerRotation(),
                RotationY             = new TwinIntegerRotation(),
                RotationZ             = new TwinIntegerRotation(),
                ObjectId              = (ushort)rec.ObjectId,
                RefListIndex          = -1,
                OnSpawnHeaderScriptID = 0xFFFF,
                StateFlags            = templateStateFlags,
                InstancesRelated      = 0, Instances = new List<ushort>(),
                PositionsRelated      = 0, Positions = new List<ushort>(),
                PathsRelated          = 0, Paths     = new List<ushort>(),
                ParamList1            = new List<uint>(),
                ParamList2            = new List<float>(),
                ParamList3            = new List<uint>(),
            };

            var instRoot = chunkRoot.Children.FirstOrDefault(c => c.Name == "Instances") ?? chunkRoot;
            var newEntity = AddOrReuseInstance(section, inst, instRoot, chunkSource);

            SelectClicked(newEntity, false);
            _browser.Log($"Add Cube: {log} Placed at the camera position " +
                          $"(StateFlags 0x{templateStateFlags:X}, fixed Visible+ShadowActive). " +
                          "Not on the Undo stack yet, but Delete Selected removes it normally. Save Chunk to keep it, then Build ISO + test.");
        }
        catch (Exception ex) { _browser.Log($"Add Cube failed: {ex.Message}"); }
    }

    private void SetAllMaterialsUnlit(bool unlit)
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        if (chunkRoot is null) { _browser.Log("All Unlit/Lit: no level currently loaded."); return; }

        int count = 0;
        foreach (var e in AllEntities(chunkRoot))
        {
            var mr = e.Get<CrashEngine.Importer.MeshRenderer>();
            if (mr?.Material is null) continue;
            mr.Material.Unlit = unlit;
            mr.Material.UnlitToggledByUser = true;
            count++;
        }
        _browser.Log($"Set Unlit={unlit} on {count} mesh(es) in the scene (only real StandardLit/" +
                      "StandardUnlit materials will actually persist — others are left alone). " +
                      "Save Chunk to keep it, then Build ISO + test.");
    }

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
                        lighting.SceneryBaseColors[sub] = sub.Colors.Select(c => new TwinVec4(c.X, c.Y, c.Z, c.W)).ToList();
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

        _browser.Log($"Scenery brightness set to {lighting.SceneryBrightness:F2}x ({lighting.SceneryBaseColors.Count} real submodel(s), " +
                      "every LOD detail level included). 3D preview updates immediately now, no reload needed. " +
                      "Save Chunk to keep it, then Build ISO + test. Note: reloading the level resets this SLIDER's " +
                      "number back to 1.0x (there's no way to recover 'what multiplier got you here' from the saved " +
                      "colors alone) -- but the level itself stays exactly as dark/bright as you left it.");
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

        var outDir = Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "SavedChunks");
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


    private void ApplyManualMeshIdChange(Entity tileEntity, SceneryTile tile, uint desiredNewMeshId)
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        var gfx = chunkSource?.Sm2?.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION);
        if (chunkSource?.Sm2 is null || gfx is null) { _browser.Log("Edit Mesh ID: no scenery graphics data loaded."); return; }

        uint oldId;
        if (!tile.IsLod)
        {
            oldId = tile.SourceId;
        }
        else
        {
            var lodSec = gfx.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_LODS_SECTION);
            if (lodSec?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyLOD>(tile.SourceId) is not { } lod
                || lod.Meshes.Count == 0)
            { _browser.Log("Edit Mesh ID: couldn't resolve this LOD tile's own Meshes[0]."); return; }
            oldId = lod.Meshes[0];
        }

        if (!MeshDecoder.SetSceneryMeshIdManually(chunkSource.Sm2, oldId, desiredNewMeshId, out var error))
        { _browser.Log($"Edit Mesh ID: {error}"); return; }

        if (chunkSource.SceneryTables is not null)
            MeshDecoder.AliasSceneryMeshTableEntry(chunkSource.SceneryTables, oldId, desiredNewMeshId);

        if (!tile.IsLod)
        {
            tile.SourceId = desiredNewMeshId;
        }
        else
        {
            var lodSec = gfx.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_LODS_SECTION);
            if (lodSec?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyLOD>(tile.SourceId) is { } lod)
                lod.Meshes[0] = desiredNewMeshId;
        }

        int cut = tileEntity.Name.LastIndexOf('_');
        if (cut >= 0)
            tileEntity.Name = tileEntity.Name[..(cut + 1)] + "mesh" + desiredNewMeshId.ToString("X8");

        _browser.Log($"Edit Mesh ID: 0x{oldId:X8} -> 0x{desiredNewMeshId:X8} (this tile only). " +
                      "Save Chunk to keep it, then Build ISO + test.");
    }

    private void SyncTriggerMarkers(Entity chunkRoot)
    {
        foreach (var e in AllEntities(chunkRoot).Where(e => e.Has<CrashEngine.Importer.TriggerMarker>()))
        {
            var marker = e.Get<CrashEngine.Importer.TriggerMarker>()!;
            if (marker.Source is null) continue;
            var pos = e.Transform.Position;
            var scl = e.Transform.Scale;
            marker.Source.Trigger.Position = new TwinVec4(pos.X, pos.Y, pos.Z, 1f);
            marker.Source.Trigger.Scale    = new TwinVec4(scl.X, scl.Y, scl.Z, 1f);
            var rot = System.Numerics.Quaternion.Normalize(e.Transform.Rotation);
            marker.Source.Trigger.Rotation = new TwinVec4(rot.X, rot.Y, rot.Z, rot.W);
        }
    }

    private void SyncCameraMarkers(Entity chunkRoot)
    {
        foreach (var e in AllEntities(chunkRoot).Where(e => e.Has<CrashEngine.Importer.CameraMarker>()))
        {
            var marker = e.Get<CrashEngine.Importer.CameraMarker>()!;
            if (marker.Source is null) continue;
            var pos = e.Transform.Position;
            var scl = e.Transform.Scale;
            marker.Source.CamTrigger.Position = new TwinVec4(pos.X, pos.Y, pos.Z, 1f);
            marker.Source.CamTrigger.Scale    = new TwinVec4(scl.X, scl.Y, scl.Z, 1f);
            var rot = System.Numerics.Quaternion.Normalize(e.Transform.Rotation);
            marker.Source.CamTrigger.Rotation = new TwinVec4(rot.X, rot.Y, rot.Z, rot.W);
        }
    }

    private void SyncPositionMarkers(Entity chunkRoot)
    {
        foreach (var e in AllEntities(chunkRoot).Where(e => e.Has<CrashEngine.Importer.PositionMarker>()))
        {
            var marker = e.Get<CrashEngine.Importer.PositionMarker>()!;
            if (marker.Source is null) continue;
            var pos = e.Transform.Position;
            var w = marker.Source.Position.W;
            marker.Source.Position = new Twinsanity.TwinsanityInterchange.Common.Vector4(pos.X, pos.Y, pos.Z, w);
        }
    }

    private void SyncAiPositionMarkers(Entity chunkRoot)
    {
        foreach (var e in AllEntities(chunkRoot).Where(e => e.Has<CrashEngine.Importer.AiPositionMarker>()))
        {
            var marker = e.Get<CrashEngine.Importer.AiPositionMarker>()!;
            if (marker.Source is null) continue;
            var pos = e.Transform.Position;
            var w = marker.Source.Position.W;
            marker.Source.Position = new Twinsanity.TwinsanityInterchange.Common.Vector4(pos.X, pos.Y, pos.Z, w);
        }
    }

    // Amedo 2026-09-19
    private void SyncSceneryLightMarkers(Entity chunkRoot)
    {
        foreach (var e in AllEntities(chunkRoot).Where(e => e.Has<CrashEngine.Importer.SceneryLightMarker>()))
        {
            var marker = e.Get<CrashEngine.Importer.SceneryLightMarker>()!;
            if (marker.Source is null) continue;
            var pos = e.Transform.Position;
            var w = marker.Source.Position.W;
            marker.Source.Position = new Twinsanity.TwinsanityInterchange.Common.Vector4(pos.X, pos.Y, pos.Z, w);
        }
    }

}
