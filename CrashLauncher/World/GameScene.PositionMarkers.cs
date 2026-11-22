using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using System.Numerics;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPosition;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;

namespace CrashLauncher;

// Amedo 2026-11-15
public sealed partial class GameScene : Scene
{
    private static readonly Vector4 PositionMarkerColor = new(1f, 0.9f, 0.15f, 0.7f);
    private static readonly Vector4 PositionRingColor = new(0.95f, 0.78f, 0.1f, 1f);
    private const float PositionMarkerScale = 0.5f;
    private const float PositionPinHeightPerScale = 1f / 0.3f;

    private Entity MakePositionMarker(Entity parent, string name, TwinVec4 pos)
    {
        var gl = Engine.Instance.GL;
        if (EnsurePositionPinModel(gl) is not { } pin) return MakePointMarker(parent, name, pos, PositionMarkerColor);

        var e = new Entity(name);
        e.Transform.Position = new Vector3(pos.X, pos.Y, pos.Z);
        e.Transform.Scale    = new Vector3(PositionMarkerScale);

        var iconEnt = new Entity($"{name}_icon");
        iconEnt.Transform.Scale = new Vector3(PositionPinHeightPerScale / pin.Height);
        var iconRdr = iconEnt.Add(new DirectCubeRenderer { Mesh = pin.Mesh, Color = Vector4.One });
        iconRdr.Albedo          = new Texture2D(gl, pin.Pixels, (uint)pin.TexWidth, (uint)pin.TexHeight);
        iconRdr.Mat.Culling     = Material.CullMode.Both;
        iconRdr.Mat.AlwaysOnTop = false;
        e.AddChild(iconEnt);

        var ringEnt = new Entity($"{name}_ring");
        ringEnt.Transform.Scale = new Vector3(AiRingRadius, 1f, AiRingRadius);
        var ringRdr = ringEnt.Add(new DirectCubeRenderer { Mesh = EnsureAiRingMesh(gl), Color = PositionRingColor });
        ringRdr.Mat.Culling         = Material.CullMode.Both;
        ringRdr.Mat.AlwaysOnTop     = false;
        ringRdr.Mat.IgnoreDepthTest = false;
        e.AddChild(ringEnt);

        parent.AddChild(e);
        return e;
    }

    // Amedo 2026-11-20
    public void AddPositionHere()
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        if (chunkRoot?.Get<ChunkSource>()?.Rm2 is not { } rm2 || _positionsRoot is null) { _browser.Log("Add Position: no level loaded."); return; }
        var owner = SingleSelectedInstance();
        var inst = owner?.Get<InstanceData>();
        var layout = inst is not null ? InstanceLayoutOf(inst.Source)
                   : Layouts(rm2).Where(l => PositionsOf(l) is not null).OrderByDescending(l => PositionsOf(l)!.GetItemsAmount()).FirstOrDefault();
        if (layout is null || PositionsOf(layout) is not { } posSec) { _browser.Log("Add Position: this level has no positions section."); return; }

        var rec = new LayoutEditRecorder();
        rec.Section(posSec);
        CompactPositionIds(rec, layout); // Amedo 2026-11-21
        uint id = (uint)posSec.GetItemsAmount(); // Amedo 2026-11-21
        float w = posSec.GetItemsAmount() > 0 && posSec.GetItem(0) is PS2AnyPosition first ? first.Position.W : 1f;
        var at = inst?.Source.Position is { } p ? new Vector3(p.X, p.Y, p.Z) : Vector3.Zero;
        var point = new PS2AnyPosition { Position = new TwinVec4(at.X, at.Y, at.Z, w) };
        point.SetID(id);
        posSec.AddItem(point);
        var ent = MakePositionMarker(_positionsRoot, $"Position_{id:X4}", point.Position);
        ent.Add(new PositionMarker { Source = point });
        rec.Added(_positionsRoot, ent);
        _undoStack.Push(rec.Finish(ent));
        _positionsRoot.Active = true; _showPositions = true;

        _selectedSet.Clear();
        if (owner is not null) { _selectedSet.Add(owner); SyncSelectionOrder(); }
        _selectedSet.Add(ent);
        SyncSelectionOrder();
        _selected = ent;
        _revealSelectionInTree = true;
        _browser.Log($"Added Position 0x{id:X4} at ({at.X:0.#},{at.Y:0.#},{at.Z:0.#})" +
                     (owner is not null ? $", in the layout of {owner.Name}: drag it, then Add it to its keys in the Inspector." : ". Drag it where you want.") +
                     " Save Chunk to keep.");
    }

    private static BaseTwinSection? PositionsOf(BaseTwinSection layout) =>
        layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_POSITIONS_SECTION);

    // Amedo 2026-11-21
    private int CompactPositionIds(LayoutEditRecorder? rec, BaseTwinSection layout)
    {
        if (PositionsOf(layout) is not { } posSec) return 0;
        var remap = new Dictionary<ushort, ushort>();
        for (int i = 0; i < posSec.GetItemsAmount(); i++)
            if (posSec.GetItem(i).GetID() != (uint)i) remap[(ushort)posSec.GetItem(i).GetID()] = (ushort)i;
        if (remap.Count == 0) return 0;
        var markers = new Dictionary<object, Entity>(ReferenceEqualityComparer.Instance);
        if (_positionsRoot is not null)
            foreach (var m in _positionsRoot.Children)
                if (m.Get<PositionMarker>()?.Source is { } s) markers[s] = m;
        for (int i = 0; i < posSec.GetItemsAmount(); i++)
        {
            var item = posSec.GetItem(i);
            uint oldId = item.GetID(), newId = (uint)i;
            if (oldId == newId) continue;
            var marker = markers.GetValueOrDefault(item);
            string oldName = marker?.Name ?? "", newName = $"Position_{newId:X4}";
            item.SetID(newId);
            if (marker is not null) marker.Name = newName;
            rec?.Changed(() => { item.SetID(oldId); if (marker is not null) marker.Name = oldName; },
                         () => { item.SetID(newId); if (marker is not null) marker.Name = newName; });
        }
        if (layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION) is { } instSec)
            for (int i = 0; i < instSec.GetItemsAmount(); i++)
            {
                if (instSec.GetItem(i) is not PS2AnyInstance inst || !inst.Positions.Any(remap.ContainsKey)) continue;
                rec?.Instance(inst);
                inst.Positions = inst.Positions.Select(k => remap.TryGetValue(k, out var n) ? n : k).ToList();
            }
        return remap.Count;
    }

    private MarkerModel? _positionPin;
    private bool _positionPinTried;
    private MarkerModel? EnsurePositionPinModel(GL gl)
    {
        if (_positionPinTried) return _positionPin;
        _positionPinTried = true;
        return _positionPin = LoadMarkerModel(gl, "PositionPin.glb");
    }

    private void UpdatePositionPins()
    {
        if (_positionsRoot is not { Active: true } root || _positionPin is null) return;
        if (root.Parent?.Get<ChunkSource>() is { } src) UpdateAiRings(root, src);
        if (!TryCameraEye(out var eye)) return;
        foreach (var e in root.Children)
            if (e.Active && IsIconMarker(e)) TurnIconToCamera(e, eye, pitch: false);
    }
}
