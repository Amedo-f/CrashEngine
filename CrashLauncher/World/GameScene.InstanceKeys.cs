using CrashEngine.Core;
using CrashEngine.Importer;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPosition;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;

namespace CrashLauncher;

// Amedo 2026-11-15
public sealed partial class GameScene
{
    private readonly Dictionary<(object Rm2, ushort ObjectId), MovementInfo> _movementCache = new();

    private MovementInfo MovementOf(InstanceData inst)
    {
        if (Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>() is not { Rm2: { } rm2 } src) return MovementInfo.None;
        var key = ((object)rm2, inst.ObjectId);
        if (_movementCache.TryGetValue(key, out var cached)) return cached;
        var obj = MeshDecoder.CodeSection(rm2, TwinConstants.CODE_GAME_OBJECTS_SECTION)?.GetItem<PS2AnyObject>(inst.ObjectId)
               ?? MeshDecoder.CodeSection(src.GlobalRm2, TwinConstants.CODE_GAME_OBJECTS_SECTION)?.GetItem<PS2AnyObject>(inst.ObjectId);
        var info = obj is null ? MovementInfo.None : MovementModes.Of(rm2, src.GlobalRm2, obj);
        _movementCache[key] = info;
        return info;
    }

    private BaseTwinSection? InstanceLayoutOf(PS2AnyInstance instance)
    {
        if (Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Rm2 is not { } rm2) return null;
        foreach (var layout in Layouts(rm2))
            if (layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION) is { } sec && SectionHolds(sec, instance))
                return layout;
        return null;
    }

    private List<(ushort Id, PS2AnyPosition? Point, Entity? Marker)> PatrolPoints(PS2AnyInstance instance)
    {
        var result = new List<(ushort, PS2AnyPosition?, Entity?)>();
        var posSec = InstanceLayoutOf(instance)?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_POSITIONS_SECTION);
        var markers = new Dictionary<object, Entity>(ReferenceEqualityComparer.Instance);
        if (_positionsRoot is not null)
            foreach (var m in _positionsRoot.Children)
                if (m.Get<PositionMarker>()?.Source is { } s) markers[s] = m;
        foreach (var id in instance.Positions)
        {
            var point = posSec is not null && posSec.ContainsItem(id) ? posSec.GetItem<PS2AnyPosition>(id) : null;
            result.Add((id, point, point is not null && markers.TryGetValue(point, out var ent) ? ent : null));
        }
        return result;
    }

    // Amedo 2026-11-19
    private const double KeyUseRescanSeconds = 3.0;
    private readonly Dictionary<(object Rm2, ushort ObjectId), (double At, List<(KeyUseScan.KeyRange Range, string Script)> Uses)> _keyUseCache = new();

    // Amedo 2026-11-19
    private List<(KeyUseScan.KeyRange Range, string Script)> KeyUsesOf(InstanceData inst)
    {
        if (Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>() is not { Rm2: { } rm2 } src) return new();
        var key = ((object)rm2, inst.ObjectId);
        double now = EngineTime.Total;
        if (_keyUseCache.TryGetValue(key, out var cached) && now - cached.At < KeyUseRescanSeconds) return cached.Uses;
        var uses = new List<(KeyUseScan.KeyRange, string)>();
        if (AnimEventScan.FindObject(inst.ObjectId, rm2, src.GlobalRm2) is { } obj)
            foreach (var graph in AnimEventScan.GraphsOf(obj, rm2, src.GlobalRm2))
                foreach (var range in KeyUseScan.OwnKeys(BehaviourSource.ToCSharp(graph, graph.GetID(), "keys")))
                    uses.Add((range, graph.Name));
        _keyUseCache[key] = (now, uses);
        return uses;
    }

    // Amedo 2026-11-19
    private void AppendKeys(Entity owner, InstanceData inst, IReadOnlyList<Entity> points)
    {
        var posSec = InstanceLayoutOf(inst.Source)?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_POSITIONS_SECTION);
        var rec = new LayoutEditRecorder();
        rec.Instance(inst.Source);
        int added = 0, otherLayout = 0, repeated = 0;
        foreach (var point in points)
        {
            if (point.Get<PositionMarker>() is not { } marker || posSec is null || !SectionHolds(posSec, marker.Source)) { otherLayout++; continue; }
            ushort id = (ushort)marker.Source.GetID();
            if (inst.Source.Positions.Count > 0 && inst.Source.Positions[^1] == id) { repeated++; continue; }
            inst.Source.Positions.Add(id);
            added++;
        }
        if (added > 0) _undoStack.Push(rec.Finish(owner));
        _browser.Log($"{owner.Name}: {added} key(s) added at the end ({inst.Source.Positions.Count} now)" +
                     (otherLayout > 0 ? $", {otherLayout} skipped (another layout)" : "") +
                     (repeated > 0 ? $", {repeated} skipped (same as the last key)" : "") +
                     (added > 0 ? ". Save Chunk to keep." : "."));
    }

    private void RemovePatrolPoint(LayoutEditRecorder rec, InstanceData inst, int index)
    {
        if (index < 0 || index >= inst.Source.Positions.Count) return;
        rec.Instance(inst.Source);
        ushort id = inst.Source.Positions[index];
        inst.Source.Positions.RemoveAt(index);
        if (inst.Source.Positions.Contains(id)) return;
        var layout = InstanceLayoutOf(inst.Source);
        var posSec = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_POSITIONS_SECTION);
        var instSec = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
        if (posSec is null || instSec is null || !posSec.ContainsItem(id)) return;
        for (int i = 0; i < instSec.GetItemsAmount(); i++)
            if (instSec.GetItem(i) is PS2AnyInstance other && other.Positions.Contains(id)) return;
        var point = posSec.GetItem<PS2AnyPosition>(id);
        rec.Section(posSec);
        posSec.RemoveItem(point);
        var marker = _positionsRoot?.Children.FirstOrDefault(m => ReferenceEquals(m.Get<PositionMarker>()?.Source, point));
        if (marker is not null && _positionsRoot is not null)
        {
            rec.Removed(_positionsRoot, marker);
            _positionsRoot.RemoveChild(marker);
        }
    }

    private void MovePatrolPoint(LayoutEditRecorder rec, InstanceData inst, int index, int delta)
    {
        int to = index + delta;
        if (index < 0 || to < 0 || index >= inst.Source.Positions.Count || to >= inst.Source.Positions.Count) return;
        rec.Instance(inst.Source);
        (inst.Source.Positions[index], inst.Source.Positions[to]) = (inst.Source.Positions[to], inst.Source.Positions[index]);
    }

    private void SetIdleMode(InstanceData inst, MovementInfo info, MovementMode? mode)
    {
        uint flags = inst.Source.StateFlags & ~info.ModeMask;
        if (mode is not null) flags |= 1u << mode.Bit;
        inst.Source.StateFlags = flags;
    }
}
