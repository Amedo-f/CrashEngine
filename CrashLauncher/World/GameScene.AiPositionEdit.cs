using CrashEngine.Core;
using CrashEngine.Importer;
using System.Numerics;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using PS2AnyAIPath = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPath;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using ITwinItem = Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{

    public Entity? AddAiPositionAt(Vector3 pos) => AddAiPositionAt(pos, null);

    // Amedo 2026-11-15
    private Entity? AddAiPositionAt(Vector3 pos, BaseTwinSection? layout)
    {
        var rec = new LayoutEditRecorder();
        var ent = AddAiPosition(rec, pos, layout);
        if (ent is not null) _undoStack.Push(rec.Finish(ent));
        return ent;
    }

    // Amedo 2026-11-15
    private Entity? AddAiPosition(LayoutEditRecorder rec, Vector3 pos, BaseTwinSection? layout)
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var rm2 = chunkRoot?.Get<ChunkSource>()?.Rm2;
        if (chunkRoot is null || rm2 is null || _aiPosRoot is null) { _browser.Log("Add AI Position: no level loaded."); return null; }

        var aiSec = (layout ?? AiNetworkLayout(rm2))?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_POSITIONS_SECTION); // Amedo 2026-11-15
        if (aiSec is null) { _browser.Log("Add AI Position: this level has no AI-positions section."); return null; }
        rec.Section(aiSec);

        uint id = 0; var used = new HashSet<uint>();
        for (int i = 0; i < aiSec.GetItemsAmount(); i++) if (aiSec.GetItem(i) is PS2AnyAIPosition p) used.Add(p.GetID());
        while (used.Contains(id)) id++;

        var ap = new PS2AnyAIPosition { Position = new TwinVec4(pos.X, pos.Y, pos.Z, 2.44f), UnkShort = 0 };
        ap.SetID(id);
        aiSec.AddItem(ap);

        var ent = MakeAiPositionMarker(_aiPosRoot, $"AiPosition_{id:X4}", ap.Position); // Amedo
        ent.Add(new CrashEngine.Importer.AiPositionMarker { Source = ap });
        rec.Added(_aiPosRoot, ent);
        _aiPosRoot.Active = true; _showAiPositions = true;

        _selectedSet.Clear(); _selectedSet.Add(ent); _selected = ent; _revealSelectionInTree = true;
        _browser.Log($"Added AI Position 0x{id:X4} at ({pos.X:0.#},{pos.Y:0.#},{pos.Z:0.#}). Ctrl+D to copy, drag to move, Delete to remove. Save Chunk to keep.");
        return ent;
    }

    // Amedo 2026-11-15
    private static BaseTwinSection? AiPositionsOf(BaseTwinSection layout) =>
        layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_POSITIONS_SECTION);

    // Amedo 2026-11-15
    private static BaseTwinSection? AiNetworkLayout(PS2AnyTwinsanityRM2 rm2) =>
        Layouts(rm2).Where(l => AiPositionsOf(l) is not null)
                    .OrderByDescending(l => AiPositionsOf(l)!.GetItemsAmount())
                    .FirstOrDefault();

    // Amedo 2026-11-15
    private static List<BaseTwinSection> AiLayoutsInUse(PS2AnyTwinsanityRM2 rm2) =>
        Layouts(rm2).Where(l => AiPositionsOf(l) is { } s && s.GetItemsAmount() > 0).ToList();

    // Amedo 2026-11-15
    private int MergeAiLayouts(LayoutEditRecorder rec)
    {
        if (Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Rm2 is not { } rm2 || AiNetworkLayout(rm2) is not { } target) return 0;
        var targetPositions = AiPositionsOf(target)!;
        var targetPaths = target.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_PATHS_SECTION);
        if (targetPaths is null) return 0;
        rec.Section(targetPositions).Section(targetPaths);
        int moved = 0;
        foreach (var source in AiLayoutsInUse(rm2).Where(l => !ReferenceEquals(l, target)))
        {
            var positions = AiPositionsOf(source)!;
            var paths = source.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_PATHS_SECTION);
            rec.Section(positions).Section(paths);
            var newIds = new Dictionary<uint, uint>();
            foreach (var point in Enumerable.Range(0, positions.GetItemsAmount()).Select(positions.GetItem).ToList())
            {
                uint id = 0, oldId = point.GetID();
                while (targetPositions.ContainsItem(id)) id++;
                newIds[oldId] = id;
                positions.RemoveItem(point);
                point.SetID(id);
                targetPositions.AddItem(point);
                var marker = _aiPosRoot?.Children.FirstOrDefault(e => ReferenceEquals(e.Get<AiPositionMarker>()?.Source, point));
                string oldName = marker?.Name ?? "", newName = $"AiPosition_{id:X4}";
                if (marker is not null) marker.Name = newName;
                rec.Changed(() => { point.SetID(oldId); if (marker is not null) marker.Name = oldName; },
                            () => { point.SetID(id); if (marker is not null) marker.Name = newName; });
                moved++;
            }
            if (paths is null) continue;
            uint nextPath = 0;
            for (int i = 0; i < targetPaths.GetItemsAmount(); i++) nextPath = Math.Max(nextPath, targetPaths.GetItem(i).GetID() + 1);
            foreach (var path in Enumerable.Range(0, paths.GetItemsAmount()).Select(paths.GetItem).OfType<PS2AnyAIPath>().ToList())
            {
                paths.RemoveItem(path);
                if (!newIds.TryGetValue(path.Args[0], out var a) || !newIds.TryGetValue(path.Args[1], out var b)) continue;
                ushort oldA = path.Args[0], oldB = path.Args[1];
                uint oldPathId = path.GetID(), newPathId = nextPath++;
                path.Args[0] = (ushort)a;
                path.Args[1] = (ushort)b;
                path.SetID(newPathId);
                targetPaths.AddItem(path);
                rec.Changed(() => { path.Args[0] = oldA; path.Args[1] = oldB; path.SetID(oldPathId); },
                            () => { path.Args[0] = (ushort)a; path.Args[1] = (ushort)b; path.SetID(newPathId); });
            }
        }
        return moved;
    }

    // Amedo 2026-11-15
    private BaseTwinSection? AiLayoutOf(ITwinItem aiPosition)
    {
        if (Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Rm2 is not { } rm2) return null;
        foreach (var layout in Layouts(rm2))
            if (layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_POSITIONS_SECTION) is { } sec && SectionHolds(sec, aiPosition))
                return layout;
        return null;
    }

    // Amedo 2026-11-15
    private static bool SectionHolds(BaseTwinSection section, ITwinItem item)
    {
        for (int i = 0; i < section.GetItemsAmount(); i++)
            if (ReferenceEquals(section.GetItem(i), item)) return true;
        return false;
    }

    // Amedo 2026-11-15
    private bool LinkAiPositions(LayoutEditRecorder rec, AiPositionMarker from, AiPositionMarker to, out string? error)
    {
        error = null;
        if (ReferenceEquals(from.Source, to.Source)) { error = "the same AI position"; return false; }
        var layout = AiLayoutOf(from.Source);
        if (layout is null || !ReferenceEquals(layout, AiLayoutOf(to.Source))) { error = "the two AI positions are in different layouts"; return false; }
        var pathSec = layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_PATHS_SECTION);
        if (pathSec is null) { error = "this layout has no AI-paths section"; return false; }
        ushort fromId = (ushort)from.Source.GetID(), toId = (ushort)to.Source.GetID();
        uint nextId = 0;
        for (int i = 0; i < pathSec.GetItemsAmount(); i++)
        {
            if (pathSec.GetItem(i) is not PS2AnyAIPath p) continue;
            if ((p.Args[0] == fromId && p.Args[1] == toId) || (p.Args[0] == toId && p.Args[1] == fromId)) { error = "already linked"; return false; }
            nextId = Math.Max(nextId, p.GetID() + 1);
        }
        rec.Section(pathSec);
        var path = new PS2AnyAIPath { Args = new ushort[] { fromId, toId, 0, 0, 0 } };
        path.SetID(nextId);
        pathSec.AddItem(path);
        return true;
    }

    // Amedo 2026-11-15
    private static void DeleteAiPath(LayoutEditRecorder rec, BaseTwinSection pathSection, PS2AnyAIPath path)
    {
        rec.Section(pathSection);
        pathSection.RemoveItem(path);
    }

    public void AddAiPathBetweenSelected() => LinkAiChain(SelectedAiPositions()); // Amedo 2026-11-19

    // Amedo 2026-11-19
    private List<Entity> SelectedAiPositions() =>
        _selectionOrder.Where(x => x.Parent is not null && x.Get<AiPositionMarker>() is not null).ToList();

    // Amedo 2026-11-19
    private void LinkAiChain(IReadOnlyList<Entity> chain)
    {
        if (chain.Count < 2) { _browser.Log($"Link AI positions: select at least TWO AI positions (Shift+click). You have {chain.Count} selected."); return; }
        var rec = new LayoutEditRecorder();
        int made = 0, already = 0;
        string? lastError = null;
        for (int i = 0; i + 1 < chain.Count; i++)
        {
            if (LinkAiPositions(rec, chain[i].Get<AiPositionMarker>()!, chain[i + 1].Get<AiPositionMarker>()!, out var error)) made++;
            else if (error == "already linked") already++;
            else lastError = error;
        }
        if (made > 0) _undoStack.Push(rec.Finish(chain[^1]));
        _browser.Log($"Link AI positions: {made} new link(s) along {chain.Count} point(s)" +
                     (already > 0 ? $", {already} already linked" : "") +
                     (lastError is null ? "." : $", some skipped: {lastError}.") +
                     (made > 0 ? " Save Chunk to keep." : ""));
    }

    // Amedo 2026-11-15
    private IEnumerable<(BaseTwinSection Section, PS2AnyAIPath Path, Entity A, Entity B)> AiLinks()
    {
        if (_aiPosRoot is not { } root || Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Rm2 is not { } rm2) yield break;
        var markers = new Dictionary<object, Entity>(ReferenceEqualityComparer.Instance);
        foreach (var e in root.Children)
            if (e.Get<AiPositionMarker>()?.Source is { } s) markers[s] = e;
        foreach (var layout in Layouts(rm2))
        {
            var posSec = layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_POSITIONS_SECTION);
            var pathSec = layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_PATHS_SECTION);
            if (posSec is null || pathSec is null) continue;
            for (int i = 0; i < pathSec.GetItemsAmount(); i++)
            {
                if (pathSec.GetItem(i) is not PS2AnyAIPath p || p.Args.Length < 2) continue;
                if (!posSec.ContainsItem(p.Args[0]) || !posSec.ContainsItem(p.Args[1])) continue;
                if (!markers.TryGetValue(posSec.GetItem<PS2AnyAIPosition>(p.Args[0]), out var a) || !markers.TryGetValue(posSec.GetItem<PS2AnyAIPosition>(p.Args[1]), out var b)) continue;
                yield return (pathSec, p, a, b);
            }
        }
    }
}
