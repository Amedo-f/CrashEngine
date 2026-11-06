using CrashEngine.Core;
using CrashEngine.Importer;
using System.Numerics;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using PS2AnyAIPath = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPath;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{

    public Entity? AddAiPositionAt(Vector3 pos)
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var rm2 = chunkRoot?.Get<ChunkSource>()?.Rm2;
        if (chunkRoot is null || rm2 is null || _aiPosRoot is null) { _browser.Log("Add AI Position: no level loaded."); return null; }

        BaseTwinSection? aiSec = null;
        for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION && aiSec is null; lid++)
            aiSec = rm2.GetItem<BaseTwinSection>((uint)lid)?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_POSITIONS_SECTION);
        if (aiSec is null) { _browser.Log("Add AI Position: this level has no AI-positions section."); return null; }

        uint id = 0; var used = new HashSet<uint>();
        for (int i = 0; i < aiSec.GetItemsAmount(); i++) if (aiSec.GetItem(i) is PS2AnyAIPosition p) used.Add(p.GetID());
        while (used.Contains(id)) id++;

        var ap = new PS2AnyAIPosition { Position = new TwinVec4(pos.X, pos.Y, pos.Z, 2.44f), UnkShort = 0 };
        ap.SetID(id);
        aiSec.AddItem(ap);

        var ent = MakeAiPositionMarker(_aiPosRoot, $"AiPosition_{id:X4}", ap.Position); // Amedo
        ent.Add(new CrashEngine.Importer.AiPositionMarker { Source = ap });
        _aiPosRoot.Active = true; _showAiPositions = true;

        _selectedSet.Clear(); _selectedSet.Add(ent); _selected = ent; _revealSelectionInTree = true;
        _browser.Log($"Added AI Position 0x{id:X4} at ({pos.X:0.#},{pos.Y:0.#},{pos.Z:0.#}). Ctrl+D to copy, drag to move, Delete to remove. Save Chunk to keep.");
        return ent;
    }

    public void AddAiPathBetweenSelected()
    {
        var nodes = _selectedSet
            .Select(x => (Ent: x, Marker: x.Get<CrashEngine.Importer.AiPositionMarker>()))
            .Where(t => t.Marker is not null).ToList();
        if (nodes.Count < 2)
        { _browser.Log($"Add AI Path: select at least TWO AI positions (Ctrl+click). You have {nodes.Count} selected."); return; }

        var rm2 = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Rm2;
        if (rm2 is null) { _browser.Log("Add AI Path: no level loaded."); return; }

        BaseTwinSection? pathSec = null;
        for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION && pathSec is null; lid++)
            pathSec = rm2.GetItem<BaseTwinSection>((uint)lid)?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_PATHS_SECTION);
        if (pathSec is null) { _browser.Log("Add AI Path: this level has no AI-paths section."); return; }

        Vector3 Pos(Entity e) => e.Transform.World.Translation;
        var tour = new List<(Entity Ent, CrashEngine.Importer.AiPositionMarker? Marker)> { nodes[0] };
        var remaining = nodes.Skip(1).ToList();
        while (remaining.Count > 0)
        {
            var last = Pos(tour[^1].Ent);
            int bi = 0; float bd = float.MaxValue;
            for (int i = 0; i < remaining.Count; i++)
            { var d = Vector3.DistanceSquared(last, Pos(remaining[i].Ent)); if (d < bd) { bd = d; bi = i; } }
            tour.Add(remaining[bi]); remaining.RemoveAt(bi);
        }

        var existing = new HashSet<(ushort, ushort)>();
        uint nextId = 0;
        for (int i = 0; i < pathSec.GetItemsAmount(); i++)
            if (pathSec.GetItem(i) is PS2AnyAIPath p)
            { existing.Add((p.Args[0], p.Args[1])); existing.Add((p.Args[1], p.Args[0])); nextId = Math.Max(nextId, p.GetID() + 1); }

        int edges = tour.Count == 2 ? 1 : tour.Count;
        int made = 0;
        for (int i = 0; i < edges; i++)
        {
            ushort fromId = (ushort)tour[i].Marker!.Source.GetID();
            ushort toId   = (ushort)tour[(i + 1) % tour.Count].Marker!.Source.GetID();
            if (fromId == toId || existing.Contains((fromId, toId))) continue;
            var path = new PS2AnyAIPath { Args = new ushort[] { fromId, toId, 0, 0, 0 } };
            path.SetID(nextId++);
            pathSec.AddItem(path);
            existing.Add((fromId, toId)); existing.Add((toId, fromId));
            made++;
        }
        _browser.Log($"Added {made} AI path(s) linking {tour.Count} selected AI position(s) into a connected {(tour.Count > 2 ? "loop" : "pair")}. Save Chunk to keep.");
    }
}
