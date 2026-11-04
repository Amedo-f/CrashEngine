using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using RenderMaterial = CrashEngine.Renderer.Material;

namespace CrashLauncher;

// Amedo
public sealed partial class GameScene
{
    private InspectorSnapshot? _inspectorPending;

    private sealed class InspectorSnapshot
    {
        public required Entity Target;
        public ChunkSource? Src;
        public Entity? ChunkRoot;
        public TransformSnapshot Transform;
        public bool Active, HasTransform = true;
        public int UndoCount;
        public readonly List<(BaseTwinItem Item, byte[] Bytes, bool Global)> Items = new();
        public readonly List<(object Obj, object?[] Values)> Values = new();
    }

    private void InspectorUndoFrame(Entity? selected)
    {
        CommitInspectorEdit();
        if (_inspectorPending is not null || selected is null) return;
        bool mouse = ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right);
        if (!mouse) return;
        bool inInspector = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByPopup);
        bool inPopup = ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel);
        if (!inInspector && !inPopup) return;
        _inspectorPending = TakeInspectorSnapshot(selected);
    }

    private void CommitInspectorEdit(bool force = false)
    {
        if (_inspectorPending is not { } before) return;
        if (!force && (ImGui.IsAnyItemActive() || ImGui.IsMouseDown(ImGuiMouseButton.Left) || ImGui.IsMouseDown(ImGuiMouseButton.Right))) return;
        _inspectorPending = null;
        if (_undoStack.Count != before.UndoCount) return;
        var after = TakeInspectorSnapshot(before.Target, before);
        var action = new InspectorEditAction { Scene = this, Before = before, After = after };
        if (action.Trim()) _undoStack.Push(action);
    }

    private InspectorSnapshot TakeInspectorSnapshot(Entity target, InspectorSnapshot? like = null)
    {
        var snap = new InspectorSnapshot { Target = target, Transform = new TransformSnapshot(target.Transform), Active = target.Active, UndoCount = _undoStack.Count };
        if (like is not null)
        {
            snap.Src = like.Src;
            snap.ChunkRoot = like.ChunkRoot;
            foreach (var (item, _, global) in like.Items) snap.Items.Add((item, InspectorUndoData.ItemBytes(item), global));
            foreach (var (obj, _) in like.Values) snap.Values.Add((obj, InspectorUndoData.ReadMembers(obj)));
            return snap;
        }
        snap.Src = ChunkSourceFor(target);
        for (var p = target; p is not null; p = p.Parent)
            if (p.Has<ChunkSource>()) snap.ChunkRoot = p;
        snap.ChunkRoot ??= Roots.FirstOrDefault(r => r.Has<ChunkSource>());

        var items = new HashSet<BaseTwinItem>();
        void Item(BaseTwinItem? it, bool global) { if (it is not null && items.Add(it)) snap.Items.Add((it, InspectorUndoData.ItemBytes(it), global)); }
        var values = new HashSet<object>(ReferenceEqualityComparer.Instance);
        void Value(object? o) { if (o is not null && values.Add(o)) snap.Values.Add((o, InspectorUndoData.ReadMembers(o))); }

        if (snap.ChunkRoot is { } chunk)
            foreach (var x in AllEntities(chunk))
            {
                if (x.Get<InstanceData>() is { } d) { Item(d.Source, false); Value(d); }
                if (x.Get<TriggerMarker>() is { } t) Item(t.Source, false);
                if (x.Get<CameraMarker>() is { } c) Item(c.Source, false);
                if (x.Get<PositionMarker>() is { } pm) Item(pm.Source, false);
                if (x.Get<AiPositionMarker>() is { } ap) Item(ap.Source, false);
                if (x.Get<WorldLightingSettings>() is { } wl) Value(wl);
                if (x.Get<SkydomeMarker>() is { } sky) Value(sky);
            }
        foreach (var x in AllEntities(target))
            foreach (var comp in x.Components)
                switch (comp)
                {
                    case CrashEngine.Importer.MeshRenderer { Material: { } m }: MaterialValues(m); break;
                    case CrashEngine.Renderer.MeshRenderer r: MaterialValues(r.Material); break;
                    case DirectCubeRenderer cube: MaterialValues(cube.Mat); break;
                    case CollisionShapeMarker or SkydomeMarker or WorldLightingSettings: Value(comp); break;
                    case SceneryLightMarker light: Value(light.Source); break;
                }
        void MaterialValues(RenderMaterial m)
        {
            Value(m);
            Value(m.SourceShader);
            if (m.Layers is { } layers) foreach (var l in layers) MaterialValues(l);
        }
        if (snap.Src is { } src)
        {
            if (target.Get<InstanceData>() is { } data)
            {
                Item(GameShadowDefs.Object(src.Rm2, data.ObjectId), false);
                if (GameShadowDefs.Object(src.GlobalRm2, data.ObjectId) is { } g && !ReferenceEquals(g, GameShadowDefs.Object(src.Rm2, data.ObjectId))) Item(g, true);
            }
            foreach (var (rm, global) in new[] { (src.Rm2, false), (src.GlobalRm2, true) })
                if (CodeSectionOf(rm, TwinConstants.CODE_BEHAVIOURS_SECTION) is { } beh)
                    for (int i = 0; i < beh.GetItemsAmount(); i++)
                        if (beh.GetItem(i) is BaseTwinItem g) Item(g, global);
            if (src.Sm2?.GetItem<PS2AnyScenery>((uint)TwinConstants.SCENERY_SECENERY_ITEM) is { } scenery)
            {
                foreach (var l in scenery.PointLights) Value(l);
                foreach (var l in scenery.NegativeLights) Value(l);
            }
        }
        return snap;
    }

    private sealed class InspectorEditAction : IEditAction
    {
        public required GameScene Scene;
        public required InspectorSnapshot Before, After;

        public bool Trim()
        {
            bool tf = !Before.Transform.Equals(in After.Transform) || Before.Active != After.Active;
            for (int i = Before.Items.Count - 1; i >= 0; i--)
                if (Before.Items[i].Bytes.AsSpan().SequenceEqual(After.Items[i].Bytes)) { Before.Items.RemoveAt(i); After.Items.RemoveAt(i); }
            for (int i = Before.Values.Count - 1; i >= 0; i--)
                if (InspectorUndoData.SameValues(Before.Values[i].Values, After.Values[i].Values)) { Before.Values.RemoveAt(i); After.Values.RemoveAt(i); }
            Before.HasTransform = After.HasTransform = tf;
            return tf || Before.Items.Count > 0 || Before.Values.Count > 0;
        }

        public void Undo() => Scene.ApplyInspectorSnapshot(Before);
        public void Redo() => Scene.ApplyInspectorSnapshot(After);
        public IEnumerable<Entity> AffectedEntities => Array.Empty<Entity>();
    }

    private void ApplyInspectorSnapshot(InspectorSnapshot s)
    {
        if (s.HasTransform)
        {
            s.Transform.ApplyTo(s.Target.Transform);
            s.Target.Active = s.Active;
        }
        bool global = false, objects = false, shaders = false;
        var rebuild = new List<Entity>();
        var objectIds = new HashSet<uint>();
        foreach (var (item, bytes, isGlobal) in s.Items)
        {
            InspectorUndoData.RestoreItem(item, bytes);
            global |= isGlobal;
            if (item is PS2AnyObject) { objects = true; objectIds.Add(item.GetID()); }
        }
        foreach (var (obj, values) in s.Values)
        {
            ushort oldObject = (obj as InstanceData)?.ObjectId ?? 0;
            float oldScenery = (obj as WorldLightingSettings)?.SceneryBrightness ?? 0f;
            float oldSky = (obj as SkydomeMarker)?.Brightness ?? 0f;
            InspectorUndoData.WriteMembers(obj, values);
            shaders |= obj is Twinsanity.TwinsanityInterchange.Common.TwinShader;
            if (obj is InstanceData d && d.ObjectId != oldObject && d.Entity is { } de) rebuild.Add(de);
            if (s.ChunkRoot is { } root)
            {
                if (obj is WorldLightingSettings wl && wl.SceneryBrightness != oldScenery) ApplySceneryBrightness(root, wl);
                if (obj is SkydomeMarker sky && sky.Brightness != oldSky) ApplySkydomeBrightness(root, sky);
            }
            if (obj is CollisionShapeMarker { Entity: { } ce } shape)
                switch (shape)
                {
                    case CollisionBoxMarker b: RebuildCollisionBoxMesh(ce, b); break;
                    case CollisionCylinderMarker c: RebuildCollisionCylinderMesh(ce, c); break;
                    case CollisionPlaneMarker p: RebuildCollisionPlaneMesh(ce, p); break;
                }
        }
        if (objectIds.Count > 0 && s.ChunkRoot is { } objRoot)
            foreach (var x in AllEntities(objRoot))
                if (x.Get<InstanceData>() is { } od && objectIds.Contains(od.ObjectId) && !rebuild.Contains(x)) rebuild.Add(x);
        if (s.Src is { } src)
        {
            if (global || shaders) src.GlobalRm2Dirty = true;
            foreach (var e in rebuild) MeshDecoder.BuildMeshForInstance(Engine.Instance.GL, src.MeshTables, e);
        }
        if (shaders) RefreshShadowReceivers();
        if (objects)
        {
            _shadowEditObject = null;
            _shadowEditSlots = null;
            foreach (var root in Roots)
                foreach (var x in AllEntities(root))
                    x.Get<GameShadowCasters>()?.Refresh();
        }
        _uniqueStatus = null;
    }
}
