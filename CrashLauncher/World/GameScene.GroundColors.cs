using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyGraphicsSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.PS2AnyGraphicsSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;

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
                if (CopySubModelColors(chunkSource, meshId, destModel, srcModel, subIndex) is not { } broadcast) continue;

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
