using CrashEngine.Core;
using CrashEngine.Importer;
using System.Numerics;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
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

    private Entity? AddPatrolPoint(LayoutEditRecorder rec, InstanceData inst, Vector3 pos)
    {
        var posSec = InstanceLayoutOf(inst.Source)?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_POSITIONS_SECTION);
        if (posSec is null || _positionsRoot is null) { _browser.Log("Add patrol point: this enemy's layout has no Positions section."); return null; }
        rec.Section(posSec).Instance(inst.Source);
        uint id = 0;
        while (posSec.ContainsItem(id)) id++;
        if (id > ushort.MaxValue) { _browser.Log("Add patrol point: no free position id left in this layout."); return null; }
        var point = new PS2AnyPosition { Position = new TwinVec4(pos.X, pos.Y, pos.Z, 1f) };
        point.SetID(id);
        posSec.AddItem(point);
        var marker = MakePositionMarker(_positionsRoot, $"Position_{id:X4}", point.Position);
        marker.Add(new PositionMarker { Source = point });
        rec.Added(_positionsRoot, marker);
        inst.Source.Positions.Add((ushort)id);
        _positionsRoot.Active = true; _showPositions = true;
        UsePatrolMode(inst);
        return marker;
    }

    private bool AppendPatrolPoint(LayoutEditRecorder rec, InstanceData inst, PositionMarker existing, out string? error)
    {
        error = null;
        var posSec = InstanceLayoutOf(inst.Source)?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_POSITIONS_SECTION);
        if (posSec is null || !SectionHolds(posSec, existing.Source)) { error = "that point is in a different layout than this enemy"; return false; }
        ushort id = (ushort)existing.Source.GetID();
        if (inst.Source.Positions.Count > 0 && inst.Source.Positions[^1] == id) { error = "already the last patrol point"; return false; }
        rec.Instance(inst.Source);
        inst.Source.Positions.Add(id);
        UsePatrolMode(inst);
        return true;
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

    private void UsePatrolMode(InstanceData inst)
    {
        var info = MovementOf(inst);
        if (info.IdleModes.FirstOrDefault(m => m.Name == "Patrol") is { } patrol && info.Current(inst.Source.StateFlags) != patrol)
            SetIdleMode(inst, info, patrol);
    }
}
