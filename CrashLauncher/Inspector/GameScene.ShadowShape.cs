using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using ShadowCommand = Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab.ITwinBehaviourCommand;
using ShadowPack = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourCommandPack;

namespace CrashLauncher;

// Amedo
public sealed partial class GameScene
{
    private static readonly string[] ShadowVolumeNames =
    [
        "0 Cylinder", "1 Cube", "2 Rounded cube", "3 Octagon",
        "4 Cone (shrinks with height)", "5 Pyramid (shrinks)", "6 Rounded cone", "7 Octagon cone",
    ];

    private PS2AnyObject? _shadowEditObject;
    private GameShadowShape?[]? _shadowEditSlots;
    private int _shadowEditActive, _shadowEditSlot;
    private bool _shadowShowShape = true;
    private (int Kind, int Index) _shadowHot = (-1, -1);
    private readonly Dictionary<PS2AnyObject, List<ShadowCommand>> _shadowOriginals = new();

    private void DrawShadowShape(Entity e)
    {
        if (e.Get<InstanceData>() is not { } data || e.Get<SceneryTile>() is not null) return;
        if (ChunkSourceFor(e) is not { } src) return;
        if (!ImGui.CollapsingHeader("Shadow##shadowshape")) return;

        var local = GameShadowDefs.Object(src.Rm2, data.ObjectId);
        var global = GameShadowDefs.Object(src.GlobalRm2, data.ObjectId);
        var owner = GameShadowDefs.HasShadowCommands(local) ? local : GameShadowDefs.HasShadowCommands(global) ? global : null;
        var ogi = ShadowOgi(e, src, data.ObjectId);

        if (owner is null)
        {
            var def = GameShadowDefs.Default(local ?? global, ogi);
            ImGui.TextWrapped(def is null
                ? "This object has no shadow."
                : $"Default shadow for its type: one {ShadowVolumeNames[def.Rect?.Type ?? 0]} {def.Rect?.SizeX:0.##} x {def.Rect?.SizeZ:0.##}.");
            var target = local ?? global;
            if (target is not null && ImGui.Button("Customize Shadow##shadowcustom", new Vector2(-1f, 0f)))
            {
                var slots = new GameShadowShape?[4];
                slots[0] = def is null ? NewShadowShape(ogi) : GameShadowDefs.Clone(def);
                SaveShadow(src, target, slots, 0);
            }
            if (ImGui.IsItemHovered())
                MaybeTooltip("Writes the shadow as real shadow commands (187-191) into this object's definition,\nso the editor, Play, the PC build and the PS2 (Build ISO) all use it.");
            return;
        }

        if (!ReferenceEquals(_shadowEditObject, owner) || _shadowEditSlots is null)
        {
            _shadowEditObject = owner;
            _shadowEditSlots = GameShadowDefs.Slots(owner, null, ogi, out _shadowEditActive, out _);
            _shadowEditSlot = _shadowEditActive;
            _shadowHot = (-1, -1);
        }
        var slotsNow = _shadowEditSlots;
        bool isGlobal = ReferenceEquals(owner, global) && !ReferenceEquals(owner, local);
        ImGui.TextDisabled(isGlobal ? "Defined in Default.rm2: every level changes." : "Defined in this level's object.");
        ImGui.TextDisabled("Every instance of this object changes (Unique Object to keep it to one).");
        if (isGlobal && local is not null)
        {
            if (ImGui.SmallButton("Copy to this level##shadowcopy"))
            {
                SaveShadow(src, local, CloneSlots(slotsNow), _shadowEditActive);
                return;
            }
            ImGui.SameLine();
        }
        if (_shadowOriginals.TryGetValue(owner, out var original) && owner.BehaviourPack is ShadowPack revertPack &&
            !revertPack.Commands.SequenceEqual(original) && ImGui.SmallButton("Revert to Original##shadowrevert"))
        {
            revertPack.Commands = new(original);
            MarkShadowDirty(src, owner);
            return;
        }
        ImGui.Checkbox("Show Shape In Scene##shadowshow", ref _shadowShowShape);

        bool changed = false;
        if (ImGui.BeginCombo("Edit Slot##shadowslot", ShadowSlotLabel(slotsNow, _shadowEditSlot)))
        {
            for (int s = 0; s < slotsNow.Length; s++)
                if (ImGui.Selectable(ShadowSlotLabel(slotsNow, s), s == _shadowEditSlot)) { _shadowEditSlot = s; _shadowHot = (-1, -1); }
            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("An object can have up to 4 shadow shapes; scripts switch between them (ShadowToggle 191).\nThe start slot is the one used when the object appears.");
        int startSlot = _shadowEditActive;
        if (ImGui.SliderInt("Start Slot##shadowstart", ref startSlot, 0, 3) && slotsNow[startSlot] is not null) { _shadowEditActive = startSlot; changed = true; }

        var shape = slotsNow[_shadowEditSlot];
        if (shape is null)
        {
            if (ImGui.Button("Create Shape In This Slot##shadownew", new Vector2(-1f, 0f)))
            {
                slotsNow[_shadowEditSlot] = NewShadowShape(ogi);
                changed = true;
            }
        }
        else
        {
            float maxDist = shape.MaxDistance;
            if (ImGui.DragFloat("Max Camera Distance##shadowmax", ref maxDist, 0.5f, 1f, 1023f, "%.0f")) { shape.MaxDistance = MathF.Round(maxDist); changed = true; }
            if (ImGui.IsItemHovered()) MaybeTooltip("Farther than this from the camera = no shadow.");
            changed |= ImGui.DragFloat("Length Near##shadownear", ref shape.LengthNear, 0.1f, 0f, 200f, "%.2f");
            if (ImGui.IsItemHovered()) MaybeTooltip("How far down the shadow reaches when the camera is close.\nShort = the shadow disappears when the object is high above the ground.");
            changed |= ImGui.DragFloat("Length Far##shadowfar", ref shape.LengthFar, 0.1f, 0f, 200f, "%.2f");
            if (ImGui.IsItemHovered()) MaybeTooltip("Reach at Max Camera Distance (blends from Length Near).");

            var reacts = ShadowJointChoices(ogi);
            changed |= DrawShadowElements(shape.Circles, 0, "Circle", reacts, false);
            changed |= DrawShadowElements(shape.Capsules, 1, "Capsule", reacts, true);

            ImGui.SeparatorText("Rectangle (on the object's root)");
            if (shape.Rect is { } rect)
            {
                ImGui.PushID("shadowrect");
                bool hot = _shadowHot == (2, 0);
                if (ImGui.Selectable(hot ? "> Rectangle" : "Rectangle", hot)) _shadowHot = (2, 0);
                bool rc = false;
                int type = rect.Type;
                if (ImGui.Combo("Volume", ref type, ShadowVolumeNames, ShadowVolumeNames.Length)) { rect.Type = (byte)type; rc = true; }
                var size = new Vector2(rect.SizeX, rect.SizeZ);
                if (ImGui.DragFloat2("Half Size X/Z", ref size, 0.01f, 0f, 50f, "%.3f")) { rect.SizeX = size.X; rect.SizeZ = size.Y; rc = true; }
                rc |= ImGui.DragFloat3("Offset", ref rect.Offset, 0.01f, -50f, 50f, "%.3f");
                if (rc) { shape.Rect = rect; changed = true; }
                if (ImGui.SmallButton("Remove")) { shape.Rect = null; changed = true; }
                ImGui.PopID();
            }
            else if (ImGui.SmallButton("Add Rectangle##shadowaddrect"))
            {
                shape.Rect = new GameShadowShape.Element { Type = 4, SizeX = 0.5f, SizeZ = 0.5f, Offset = new Vector3(0f, 0.1f, 0f) };
                changed = true;
            }

            ImGui.Separator();
            if (ImGui.SmallButton("Simple Round Shadow##shadowsimple"))
            {
                var simple = NewShadowShape(ogi);
                shape.Circles.Clear();
                shape.Capsules.Clear();
                shape.Rect = simple.Rect;
                changed = true;
            }
            if (ImGui.IsItemHovered()) MaybeTooltip("Replaces the parts with one soft round cone under the object (like the fruit).");
            ImGui.SameLine();
            if (slotsNow.Count(x => x is not null) > 1 && ImGui.SmallButton("Delete This Slot##shadowdelslot"))
            {
                slotsNow[_shadowEditSlot] = null;
                if (_shadowEditActive == _shadowEditSlot) _shadowEditActive = Array.FindIndex(slotsNow, x => x is not null);
                changed = true;
            }
        }

        if (changed) SaveShadow(src, owner, slotsNow, _shadowEditActive);
        if (_shadowShowShape && slotsNow[_shadowEditSlot] is { } shown) DrawShadowShapeOverlay(e, ogi, shown);
    }

    private bool DrawShadowElements(List<GameShadowShape.Element> list, int kind, string label, List<(int React, string Name)> reacts, bool capsule)
    {
        bool changed = false;
        ImGui.SeparatorText(capsule ? "Capsules (between two joints)" : "Circles (on a joint)");
        for (int i = 0; i < list.Count; i++)
        {
            ImGui.PushID($"shadow{kind}_{i}");
            var el = list[i];
            bool hot = _shadowHot == (kind, i);
            string title = capsule ? $"{label} {i}: joints {el.JointA}-{el.JointB}" : $"{label} {i}: joint {el.JointA}";
            if (ImGui.TreeNodeEx(title, hot ? ImGuiTreeNodeFlags.Selected : ImGuiTreeNodeFlags.None))
            {
                if (ImGui.IsItemClicked()) _shadowHot = (kind, i);
                bool ec = false;
                int type = el.Type;
                if (ImGui.Combo("Volume", ref type, ShadowVolumeNames, ShadowVolumeNames.Length)) { el.Type = (byte)type; ec = true; }
                ec |= ShadowJointCombo("Joint A", ref el.JointA, reacts);
                if (capsule) ec |= ShadowJointCombo("Joint B", ref el.JointB, reacts);
                if (capsule) ec |= ImGui.DragFloat("Radius", ref el.SizeX, 0.005f, 0f, 10f, "%.3f");
                else
                {
                    var size = new Vector2(el.SizeX, el.SizeZ);
                    if (ImGui.DragFloat2("Radius X/Z", ref size, 0.005f, 0f, 10f, "%.3f")) { el.SizeX = size.X; el.SizeZ = size.Y; ec = true; }
                }
                ec |= ImGui.DragFloat3("Offset", ref el.Offset, 0.005f, -10f, 10f, "%.3f");
                if (ec) { list[i] = el; changed = true; _shadowHot = (kind, i); }
                bool remove = ImGui.SmallButton("Remove");
                ImGui.TreePop();
                if (remove) { list.RemoveAt(i); changed = true; _shadowHot = (-1, -1); ImGui.PopID(); break; }
            }
            else if (ImGui.IsItemClicked()) _shadowHot = (kind, i);
            ImGui.PopID();
        }
        if (ImGui.SmallButton($"Add {label}##shadowadd{kind}"))
        {
            var last = list.Count > 0 ? list[^1] : new GameShadowShape.Element { Type = (byte)(capsule ? 2 : 0), SizeX = 0.15f, SizeZ = 0.15f, JointA = 0, JointB = 1 };
            list.Add(last);
            _shadowHot = (kind, list.Count - 1);
            changed = true;
        }
        return changed;
    }

    private static bool ShadowJointCombo(string label, ref byte joint, List<(int React, string Name)> reacts)
    {
        int current = joint;
        string shown = reacts.FirstOrDefault(r => r.React == current).Name ?? $"{current}";
        bool changed = false;
        if (ImGui.BeginCombo(label, shown))
        {
            foreach (var (react, name) in reacts)
                if (ImGui.Selectable(name, react == current)) { joint = (byte)react; changed = true; }
            ImGui.EndCombo();
        }
        return changed;
    }

    private static List<(int React, string Name)> ShadowJointChoices(PS2AnyOGI? ogi)
    {
        var list = new List<(int, string)>();
        if (ogi is not null)
            foreach (var j in ogi.Joints.Where(j => j.ReactId is >= 0 and < 255).GroupBy(j => j.ReactId).Select(g => g.First()).OrderBy(j => j.ReactId))
                list.Add((j.ReactId, ReactNames.TryGetValue(j.ReactId, out var n) ? $"{j.ReactId} ({n})" : $"{j.ReactId}"));
        if (list.Count == 0) list.Add((0, "0"));
        return list;
    }

    private static string ShadowSlotLabel(GameShadowShape?[] slots, int s) =>
        slots[s] is { } sh ? $"Slot {s}: {sh.Circles.Count} circles, {sh.Capsules.Count} capsules{(sh.Rect is not null ? ", rectangle" : "")}" : $"Slot {s}: empty";

    private static GameShadowShape NewShadowShape(PS2AnyOGI? ogi)
    {
        float hx = 0.4f, hz = 0.4f;
        if (ogi is not null && ogi.BoundingBox.Length >= 2)
        {
            hx = MathF.Max(0.05f, (ogi.BoundingBox[1].X - ogi.BoundingBox[0].X) * 0.5f);
            hz = MathF.Max(0.05f, (ogi.BoundingBox[1].Z - ogi.BoundingBox[0].Z) * 0.5f);
        }
        return new GameShadowShape
        {
            MaxDistance = 30f, LengthNear = 16f, LengthFar = 0f,
            Rect = new GameShadowShape.Element { Type = 4, SizeX = hx, SizeZ = hz, Offset = new Vector3(0f, 0.1f, 0f) },
        };
    }

    private static GameShadowShape?[] CloneSlots(GameShadowShape?[] slots) =>
        slots.Select(s => s is null ? null : GameShadowDefs.Clone(s)).ToArray();

    private void SaveShadow(ChunkSource src, PS2AnyObject target, GameShadowShape?[] slots, int active)
    {
        if (target.BehaviourPack is ShadowPack pack) _shadowOriginals.TryAdd(target, new(pack.Commands));
        GameShadowDefs.WriteToPack(target, slots, active);
        MarkShadowDirty(src, target);
    }

    private void MarkShadowDirty(ChunkSource src, PS2AnyObject target)
    {
        if (ReferenceEquals(target, GameShadowDefs.Object(src.GlobalRm2, (ushort)target.GetID())) &&
            !ReferenceEquals(target, GameShadowDefs.Object(src.Rm2, (ushort)target.GetID())))
            src.GlobalRm2Dirty = true;
        _shadowEditObject = null;
        _shadowEditSlots = null;
        foreach (var root in Roots)
            foreach (var x in AllEntities(root))
                x.Get<GameShadowCasters>()?.Refresh();
    }

    private static PS2AnyOGI? ShadowOgi(Entity e, ChunkSource src, ushort objectId)
    {
        var anim = e.Get<AnimatedObject>();
        if ((anim?.CurrentGroup?.Ogi ?? anim?.DefaultGroup?.Ogi) is { } o) return o;
        return src.MeshTables is { } tables && MeshDecoder.ResolveDefaultOgiId(tables, objectId) is { } id ? MeshDecoder.GetRawOgi(tables, id) : null;
    }

    // Amedo
    private void DrawShadowShapeOverlay(Entity e, PS2AnyOGI? ogi, GameShadowShape shape)
    {
        var world = e.Transform.World;
        Dictionary<int, Matrix4x4>? pose = null;
        var react = new Dictionary<int, int>();
        if (ogi is not null)
        {
            var anim = e.Get<AnimatedObject>();
            pose = anim?.CurrentGroup is not null && anim.ShownJointWorlds is { } live ? live : MeshDecoder.ComputeJointWorlds(ogi);
            foreach (var j in ogi.Joints)
                if (j.ReactId is >= 0 and < 255) react.TryAdd(j.ReactId, j.Index);
        }
        Vector3 Joint(byte id) => pose is not null && react.TryGetValue(id, out int ji) && pose.TryGetValue(ji, out var m) ? (m * world).Translation : world.Translation;

        var dl = ImGui.GetBackgroundDrawList();
        uint col = ImGui.GetColorU32(new Vector4(0.35f, 0.75f, 1f, 0.9f));
        uint hotCol = ImGui.GetColorU32(new Vector4(1f, 0.6f, 0.15f, 1f));
        int sw = ViewW, sh = ViewH;
        void Ring(Vector3 c, float rx, float rz, uint color)
        {
            Span<Vector2> pts = stackalloc Vector2[16];
            for (int k = 0; k < 16; k++)
            {
                float a = k * MathF.Tau / 16f;
                if (!WorldToScreen(c + new Vector3(MathF.Cos(a) * rx, 0f, MathF.Sin(a) * rz), sw, sh, out pts[k])) return;
            }
            for (int k = 0; k < 16; k++) dl.AddLine(pts[k], pts[(k + 1) & 15], color, 1.5f);
        }
        for (int i = 0; i < shape.Circles.Count; i++)
        {
            var c = shape.Circles[i];
            uint color = _shadowHot == (0, i) ? hotCol : col;
            var p = Joint(c.JointA) + c.Offset;
            Ring(p, c.SizeX, c.SizeZ, color);
            if (WorldToScreen(p, sw, sh, out var s)) dl.AddText(s + new Vector2(4f, -14f), color, $"{c.JointA}");
        }
        for (int i = 0; i < shape.Capsules.Count; i++)
        {
            var k = shape.Capsules[i];
            uint color = _shadowHot == (1, i) ? hotCol : col;
            if (WorldToScreen(Joint(k.JointA) + k.Offset, sw, sh, out var a) && WorldToScreen(Joint(k.JointB) + k.Offset, sw, sh, out var b))
            {
                dl.AddLine(a, b, color, MathF.Max(2f, k.SizeX * 40f));
                dl.AddText((a + b) * 0.5f + new Vector2(4f, -14f), color, $"{k.JointA}-{k.JointB}");
            }
        }
        if (shape.Rect is { } r)
        {
            uint color = _shadowHot == (2, 0) ? hotCol : col;
            var o = world.Translation + Vector3.TransformNormal(r.Offset, world);
            var x = Vector3.Normalize(new Vector3(world.M11, 0f, world.M13)) * r.SizeX;
            var z = Vector3.Normalize(new Vector3(world.M31, 0f, world.M33)) * r.SizeZ;
            Span<Vector2> q = stackalloc Vector2[4];
            if (WorldToScreen(o + x + z, sw, sh, out q[0]) && WorldToScreen(o - x + z, sw, sh, out q[1]) &&
                WorldToScreen(o - x - z, sw, sh, out q[2]) && WorldToScreen(o + x - z, sw, sh, out q[3]))
                for (int k = 0; k < 4; k++) dl.AddLine(q[k], q[(k + 1) & 3], color, 1.5f);
        }
    }
}
