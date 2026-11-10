using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using System.Numerics;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyTexture = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture;
using PS2AnyGraphicsSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.PS2AnyGraphicsSection;
using PS2AnyTexturesSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics.PS2AnyTexturesSection;
using TwinColor = Twinsanity.TwinsanityInterchange.Common.Color;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using PS2AnyTwinsanitySM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
	private sealed class TexturePickerLevelCache
	{
		public PS2AnyTwinsanityRM2? Rm2;

		public PS2AnyTwinsanitySM2? Sm2;

		public readonly List<(uint Id, Texture2D Thumb, int W, int H, bool IsScenery)> Entries = new List<(uint, Texture2D, int, int, bool)>();
	}

    private bool _showTexturePicker;
    private Material? _texturePickerTargetMat;
    private string? _texturePickerLevel;
    private string _texturePickerLevelFilter = "";
    private string _texturePickerTexFilter = "";
    private readonly Dictionary<string, TexturePickerLevelCache> _texturePickerCache = new Dictionary<string, TexturePickerLevelCache>();

    private void DrawTexturePickerWindow()
    {
        if (!_showTexturePicker) return;
        var mat = _texturePickerTargetMat;
        if (mat is null) { _showTexturePicker = false; return; }

        ImGui.SetNextWindowSize(new Vector2(640f, 480f), ImGuiCond.FirstUseEver);
        if (!_windowChrome.Begin("Import Texture From Level##texpicker", ref _showTexturePicker)) return;

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
        var meshIdsUsingTexture = SceneryMeshIdsUsingTexture(gfx, textureId).ToHashSet();
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

    private static uint? FindSceneryMeshIdUsingTexture(PS2AnyGraphicsSection? gfx, uint textureId)
    {
        foreach (var meshId in SceneryMeshIdsUsingTexture(gfx, textureId)) return meshId;
        return null;
    }

    private static IEnumerable<uint> SceneryMeshIdsUsingTexture(PS2AnyGraphicsSection? gfx, uint textureId)
    {
        var meshSec = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_MESHES_SECTION);
        var matSec  = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_MATERIALS_SECTION);
        if (meshSec is null || matSec is null) yield break;

        for (int i = 0; i < meshSec.GetItemsAmount(); i++)
        {
            if (meshSec.GetItem(i) is not Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyRigidModel rm) continue;
            if (meshSec.GetItem(i) is not BaseTwinItem bi) continue;
            if (rm.Materials.Any(matId => matSec.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyMaterial>(matId) is { } mat &&
                                          mat.Shaders.Any(sh => sh.TextureId == textureId)))
                yield return bi.GetID();
        }
    }
}
