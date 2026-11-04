using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace CrashEngine.Importer;

// Amedo 2026-09-26
public sealed class GameShadowShape
{
    public struct Element
    {
        public byte Type, JointA, JointB;
        public float SizeX, SizeZ;
        public Vector3 Offset;
    }

    public float MaxDistance, LengthNear, LengthFar;
    public readonly List<Element> Circles = new();
    public readonly List<Element> Capsules = new();
    public Element? Rect;

    public bool UsesJoints => Circles.Count > 0 || Capsules.Count > 0;
}

// Amedo 2026-09-26
public static class GameShadowDefs
{
    public static float Arg(uint v) =>
        ((v >> 1) & 3) == 2 && (v & 1) == 0 ? BitConverter.UInt32BitsToSingle(v & 0xFFFFFFF8u) : 0f;

    public static bool HasShadowCommands(PS2AnyObject? obj) =>
        obj?.BehaviourPack?.Commands.Any(c => c is PS2BehaviourCommand { CommandIndex: >= 187 and <= 191 }) ?? false;

    public static GameShadowShape? FromPack(PS2AnyObject? obj)
    {
        if (obj?.BehaviourPack is not { } pack) return null;
        var slots = new GameShadowShape?[4];
        int active = 0;
        foreach (var c in pack.Commands)
            if (c is PS2BehaviourCommand pc) Apply(slots, ref active, pc.CommandIndex, pc.Arguments, Arg); // Amedo
        return slots[active];
    }

    // Amedo
    public static void Apply(GameShadowShape?[] slots, ref int active, int command, IReadOnlyList<uint> a, Func<uint, float> value)
    {
        if (command < 187 || command > 191 || a.Count == 0) return;
        uint packed = a[0];
        int slot = (int)(packed & 0xFF);
        if (slot >= slots.Length) return;
        byte b1 = (byte)(packed >> 8), b2 = (byte)(packed >> 16), b3 = (byte)(packed >> 24);
        float F(int i) => i < a.Count ? value(a[i]) : 0f;
        var shape = slots[slot];
        switch (command)
        {
            case 187:
                slots[slot] = new GameShadowShape { MaxDistance = F(1), LengthNear = F(2), LengthFar = F(3) };
                break;
            case 188:
                shape?.Circles.Add(new() { Type = b1, JointA = b2, SizeX = F(1), SizeZ = F(2), Offset = new(F(3), F(4), F(5)) });
                break;
            case 189:
                shape?.Capsules.Add(new() { Type = b1, JointA = b2, JointB = b3, SizeX = F(1), Offset = new(F(2), F(3), F(4)) });
                break;
            case 190:
                if (shape is not null) shape.Rect = new() { Type = b1, SizeX = F(1), SizeZ = F(2), Offset = new(F(3), F(4), F(5)) };
                break;
            case 191:
                active = slot;
                break;
        }
    }

    // Amedo
    public static uint EncodeFloat(float f) => (BitConverter.SingleToUInt32Bits(f) & 0xFFFFFFF8u) | 4u;

    // Amedo
    public static GameShadowShape Clone(GameShadowShape s)
    {
        var c = new GameShadowShape { MaxDistance = s.MaxDistance, LengthNear = s.LengthNear, LengthFar = s.LengthFar, Rect = s.Rect };
        c.Circles.AddRange(s.Circles);
        c.Capsules.AddRange(s.Capsules);
        return c;
    }

    // Amedo
    public static List<PS2BehaviourCommand> ToCommands(GameShadowShape?[] slots, int active)
    {
        var list = new List<PS2BehaviourCommand>();
        void Add(ushort index, uint packed, params float[] f)
        {
            var cmd = new PS2BehaviourCommand { CommandIndex = index, Bitfield = index };
            cmd.Arguments.Add(packed);
            foreach (var v in f) cmd.Arguments.Add(EncodeFloat(v));
            int want = (int)((PS2BehaviourCommand.GetCommandSize(index) - 0xC) / 4);
            while (cmd.Arguments.Count < want) cmd.Arguments.Add(0);
            if (cmd.Arguments.Count > want) cmd.Arguments.RemoveRange(want, cmd.Arguments.Count - want);
            list.Add(cmd);
        }
        for (int s = 0; s < slots.Length; s++)
        {
            if (slots[s] is not { } shape) continue;
            uint slot = (uint)s;
            Add(187, slot, (int)shape.MaxDistance, shape.LengthNear, shape.LengthFar);
            foreach (var c in shape.Circles)
                Add(188, slot | (uint)c.Type << 8 | (uint)c.JointA << 16, c.SizeX, c.SizeZ, c.Offset.X, c.Offset.Y, c.Offset.Z);
            foreach (var k in shape.Capsules)
                Add(189, slot | (uint)k.Type << 8 | (uint)k.JointA << 16 | (uint)k.JointB << 24, k.SizeX, k.Offset.X, k.Offset.Y, k.Offset.Z);
            if (shape.Rect is { } r)
                Add(190, slot | (uint)r.Type << 8, r.SizeX, r.SizeZ, r.Offset.X, r.Offset.Y, r.Offset.Z);
        }
        if (active != 0) Add(191, (uint)active);
        return list;
    }

    // Amedo
    public static void WriteToPack(PS2AnyObject obj, GameShadowShape?[] slots, int active)
    {
        if (obj.BehaviourPack is not PS2BehaviourCommandPack pack) return;
        int at = pack.Commands.FindIndex(c => c is PS2BehaviourCommand { CommandIndex: >= 187 and <= 191 });
        pack.Commands.RemoveAll(c => c is PS2BehaviourCommand { CommandIndex: >= 187 and <= 191 });
        if (at < 0 || at > pack.Commands.Count) at = pack.Commands.Count;
        pack.Commands.InsertRange(at, ToCommands(slots, active));
    }

    // Amedo
    public static GameShadowShape?[] Slots(PS2AnyObject? local, PS2AnyObject? global, PS2AnyOGI? ogi, out int active, out bool component)
    {
        var slots = new GameShadowShape?[4];
        active = 0;
        var src = HasShadowCommands(local) ? local : HasShadowCommands(global) ? global : null;
        if (src?.BehaviourPack is { } pack)
        {
            foreach (var c in pack.Commands)
                if (c is PS2BehaviourCommand pc) Apply(slots, ref active, pc.CommandIndex, pc.Arguments, Arg);
            component = true;
            return slots;
        }
        slots[0] = Default(local ?? global, ogi);
        component = slots[0] is not null;
        return slots;
    }

    // Amedo
    public static PS2AnyObject? Object(PS2AnyTwinsanityRM2? rm2, ushort id)
    {
        var objects = rm2?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
                          ?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
        return objects is not null && objects.ContainsItem(id) ? objects.GetItem<BaseTwinItem>(id) as PS2AnyObject : null;
    }

    public static GameShadowShape? Default(PS2AnyObject? obj, PS2AnyOGI? ogi)
    {
        if (obj is null) return null;
        switch (obj.Type)
        {
            case ITwinObject.ObjectType.Character:
            case ITwinObject.ObjectType.Creature:
                return FromBounds(ogi, 4);
            case ITwinObject.ObjectType.GenericObject:
                return FromBounds(ogi, 5);
            case ITwinObject.ObjectType.Crate:
                return new GameShadowShape
                {
                    MaxDistance = 50f, LengthNear = 10f, LengthFar = 0f,
                    Rect = new() { Type = 1, SizeX = 0.5f, SizeZ = 0.5f },
                };
            default:
                return null;
        }
    }

    private static GameShadowShape? FromBounds(PS2AnyOGI? ogi, byte type)
    {
        if (ogi is null || ogi.BoundingBox.Length < 2) return null;
        var mn = ogi.BoundingBox[0];
        var mx = ogi.BoundingBox[1];
        return new GameShadowShape
        {
            MaxDistance = 30f, LengthNear = 16f, LengthFar = 0f,
            Rect = new() { Type = type, SizeX = (mx.X - mn.X) * 0.5f, SizeZ = (mx.Z - mn.Z) * 0.5f, Offset = new(0f, 0.1f, 0f) },
        };
    }

    public static int UploadVolumes(GameShadowPass pass, PS2AnyTwinsanityRM2? defaultRm2)
    {
        var gfx = defaultRm2?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var models = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        var meshes = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MESHES_SECTION);
        if (models is null || meshes is null) return 0;

        int uploaded = 0;
        for (uint type = 0; type < GameShadowPass.ShapeCount; type++)
        {
            if (!meshes.ContainsItem(type) || meshes.GetItem<BaseTwinItem>(type) is not PS2AnyMesh mesh) continue;
            if (!models.ContainsItem(mesh.Model) || models.GetItem<BaseTwinItem>(mesh.Model) is not PS2AnyModel model) continue;

            var points = new List<Vector3>();
            var tris = new List<(Vector3 A, Vector3 B, Vector3 C)>();
            foreach (var sub in model.SubModels)
            {
                if (sub.Vertexes is null || sub.Vertexes.Count == 0) sub.CalculateData();
                if (sub.Vertexes is null) continue;
                Vector3 P(int i) => new(sub.Vertexes[i].X, sub.Vertexes[i].Y, sub.Vertexes[i].Z);
                for (int i = 0; i < sub.Vertexes.Count; i++) points.Add(P(i));
                foreach (var (a, b, c) in MeshDecoder.GetSubModelTriangleIndices(sub))
                    tris.Add((P(a), P(b), P(c)));
            }
            if (points.Count == 0 || tris.Count == 0) continue;

            var centroid = points.Aggregate(Vector3.Zero, (s, p) => s + p) / points.Count;
            var verts = new List<Vector3>(tris.Count * 3);
            foreach (var (a, b, c) in tris)
            {
                var n = Vector3.Cross(b - a, c - a);
                if (GameMath.LengthSquared(n) < 1e-12f) continue;
                bool outward = GameMath.Dot(n, (a + b + c) / 3f - centroid) >= 0f;
                verts.Add(a);
                verts.Add(outward ? b : c);
                verts.Add(outward ? c : b);
            }
            pass.SetShape((int)type, verts.ToArray());
            uploaded++;
        }
        return uploaded;
    }

    // Amedo
    private static IEnumerable<T> FindAll<T>(PS2AnyTwinsanityRM2?[] order, uint section, uint id, bool code) where T : BaseTwinItem
    {
        foreach (var rm2 in order)
        {
            var sec = code
                ? rm2?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)?.GetItem<BaseTwinSection>(section)
                : rm2?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_GRAPHICS_SECTION)?.GetItem<BaseTwinSection>(section);
            if (sec is not null && sec.ContainsItem(id)) yield return sec.GetItem<T>(id);
        }
    }

    // Amedo
    public static bool CratesReceiveShadows { get; set; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Twinsanity.TwinsanityInterchange.Common.TwinShader, object> OriginalReceiver = new();

    // Amedo
    public static int ApplyCrateReceivers(PS2AnyTwinsanityRM2? level, PS2AnyTwinsanityRM2? global, bool on)
    {
        int changed = 0;
        foreach (var sh in CrateShaders(level, global))
        {
            if (!OriginalReceiver.TryGetValue(sh, out _)) OriginalReceiver.Add(sh, sh.AlphaCorrectionValue);
            bool want = on || (OriginalReceiver.TryGetValue(sh, out var orig) && (bool)orig);
            if (sh.AlphaCorrectionValue == want) continue;
            sh.AlphaCorrectionValue = want;
            changed++;
        }
        return changed;
    }

    // Amedo
    public static List<Twinsanity.TwinsanityInterchange.Common.TwinShader> CrateShaders(PS2AnyTwinsanityRM2? level, PS2AnyTwinsanityRM2? global)
    {
        var order = new[] { level, global };
        var shaders = new List<Twinsanity.TwinsanityInterchange.Common.TwinShader>();
        var seen = new HashSet<Twinsanity.TwinsanityInterchange.Common.TwinShader>();
        foreach (var rm2 in order)
        {
            if (rm2?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION) is not { } objs) continue;
            for (int i = 0; i < objs.GetItemsAmount(); i++)
            {
                if (objs.GetItem(i) is not PS2AnyObject { Type: ITwinObject.ObjectType.Crate } o) continue;
                foreach (var ogiId in o.OGISlots.Where(x => x != 0xFFFF).Concat(o.RefOGIs).Distinct())
                    foreach (var ogi in FindAll<PS2AnyOGI>(order, (uint)Constants.CODE_OGIS_SECTION, ogiId, true))
                    {
                        var matIds = new List<uint>();
                        foreach (var rmId in ogi.RigidModelIds)
                            foreach (var rm in FindAll<PS2AnyRigidModel>(order, (uint)Constants.GRAPHICS_RIGID_MODELS_SECTION, rmId, false))
                                matIds.AddRange(rm.Materials);
                        if (ogi.SkinID != 0)
                            foreach (var skin in FindAll<PS2AnySkin>(order, (uint)Constants.GRAPHICS_SKINS_SECTION, ogi.SkinID, false))
                                matIds.AddRange(skin.SubSkins.Select(s => s.Material));
                        if (ogi.BlendSkinID != 0)
                            foreach (var blend in FindAll<PS2AnyBlendSkin>(order, (uint)Constants.GRAPHICS_BLEND_SKINS_SECTION, ogi.BlendSkinID, false))
                                matIds.AddRange(blend.SubBlends.Select(s => s.Material));
                        foreach (var matId in matIds.Distinct())
                            foreach (var mat in FindAll<PS2AnyMaterial>(order, (uint)Constants.GRAPHICS_MATERIALS_SECTION, matId, false))
                                foreach (var sh in mat.Shaders)
                                    if (seen.Add(sh)) shaders.Add(sh);
                    }
            }
        }
        return shaders;
    }
}

// Amedo 2026-09-26
public sealed class GameShadowCasters : Component
{
    private readonly List<Entity> _instances = new();
    private readonly Dictionary<ushort, GameShadowShape?> _shapes = new();
    private readonly Dictionary<ushort, PS2AnyOGI?> _ogis = new();
    private readonly Dictionary<PS2AnyOGI, (Dictionary<int, Matrix4x4> Bind, int[] React)> _skeletons = new();
    private int _refreshIn;
    private bool _uploaded;

    public int LastCasterCount { get; private set; }

    public void Refresh() => _refreshIn = 0; // Amedo

    public override void OnRender(GL gl)
    {
        var pass = GameShadowPass.Instance;
        if (pass is null || !GameShadowPass.ShowShadows || Entity?.Scene is null || pass.Entity?.Scene != Entity.Scene) return;
        var src = Entity.Get<ChunkSource>();
        if (src is null) return;

        if (!_uploaded)
        {
            _uploaded = true;
            int n = GameShadowDefs.UploadVolumes(pass, src.GlobalRm2 ?? src.Rm2);
            Console.WriteLine($"[GameShadows] shadow volume shapes: {n}/{GameShadowPass.ShapeCount}");
        }
        if (--_refreshIn <= 0)
        {
            _refreshIn = 30;
            _instances.Clear();
            _shapes.Clear();
            _ogis.Clear();
            _skeletons.Clear();
            Collect(Entity);
        }

        var cam = RenderPipeline.Instance?.Camera;
        if (cam is null) return;
        var chunkWorld = Entity.Transform.World;
        if (!Matrix4x4.Invert(chunkWorld, out var toGame)) return;
        var camGame = Vector3.Transform(cam.Transform.World.Translation, toGame);
        GameMath.FrustumPlanes(chunkWorld * cam.View * cam.Projection, _planes); // Amedo

        int casters = 0;
        foreach (var e in _instances)
            if (Cast(pass, src, e, toGame, chunkWorld, camGame, cam.View * cam.Projection)) casters++;
        LastCasterCount = casters;
    }

    private readonly Vector4[] _planes = new Vector4[6]; // Amedo

    private void Collect(Entity e)
    {
        if (e.Has<InstanceData>())
        {
            _instances.Add(e);
            return;
        }
        foreach (var c in e.Children) Collect(c);
    }

    private bool Cast(GameShadowPass pass, ChunkSource src, Entity e, Matrix4x4 toGame, Matrix4x4 chunkWorld, Vector3 camGame, Matrix4x4 viewProjection)
    {
        var data = e.Get<InstanceData>();
        if (data is null) return false;
        uint flags = data.Source?.StateFlags ?? data.StateFlags;
        if (data.RuntimeFlags is { } runtimeFlags) // Amedo 2026-10-01
        {
            if ((runtimeFlags & 0x400) == 0) return false;
            if (!(data.ShadowOverride ?? (runtimeFlags & 0x1000) != 0)) return false;
        }
        else if (data.ShadowOverride is { } shadowOn) { if (!shadowOn) return false; } // Amedo 2026-09-28
        else if ((flags & 0xC) != 0xC) return false;
        for (var p = e; p is not null && p != Entity; p = p.Parent)
            if (!p.Active) return false;

        var anim = e.Get<AnimatedObject>();
        var ogi = anim?.CurrentGroup?.Ogi ?? anim?.DefaultGroup?.Ogi ?? ResolveOgi(src, data.ObjectId);
        var shape = data.RuntimeShadowSlots is { } runtimeSlots ? runtimeSlots[data.RuntimeShadowActive] : ResolveShape(src, data.ObjectId, ogi); // Amedo
        if (shape is null) return false;

        if (!InView(e.Transform.World, ogi, viewProjection)) return false;
        var obj = e.Transform.World * toGame;
        var pos = obj.Translation;
        int maxDist = (int)shape.MaxDistance & 0x3FF;
        int d2 = (int)GameMath.DistanceSq(camGame, pos);
        if (d2 >= maxDist * maxDist) return false;
        int dist = (int)MathF.Sqrt(d2);
        float t = (float)dist / maxDist;
        float len = shape.LengthFar * t + shape.LengthNear * (1f - t);
        if (MathF.Abs(len) <= 5e-5f) return false;
        var end = pos - new Vector3(0f, len, 0f);

        Dictionary<int, Matrix4x4>? pose = null;
        int[]? react = null;
        int slots = 0; // Amedo
        if (shape.UsesJoints && ogi is not null)
        {
            var sk = Skeleton(ogi);
            react = sk.React;
            pose = anim?.CurrentGroup is not null && anim.ShownJointWorlds is { } live ? live : sk.Bind; // Amedo 2026-10-01
            int objectSlots = anim?.ReactSlots ?? (src.MeshTables is { } tables && MeshDecoder.GetRawObject(tables, data.ObjectId) is { } o ? o.ReactJointAmount : 0); // Amedo
            slots = Math.Min((int)ogi.HeaderReactJointAmount, objectSlots); // Amedo
        }
        Matrix4x4 Joint(byte id) =>
            react is not null && pose is not null && id < slots && react[id] >= 0 && pose.TryGetValue(react[id], out var jw) ? jw * obj : obj; // Amedo

        foreach (var c in shape.Circles)
        {
            if (ogi is null) break; // Amedo
            var j = Joint(c.JointA);
            var jp = j.Translation;
            Emit(pass, c.Type, new Vector3(j.M31, j.M32, j.M33), jp,
                new Vector3(c.SizeX, Length(end - jp), c.SizeZ), c.Offset, chunkWorld, _planes); // Amedo
        }
        foreach (var k in shape.Capsules)
        {
            if (ogi is null) break; // Amedo
            var a = Joint(k.JointA).Translation;
            var b = Joint(k.JointB).Translation;
            var mid = (a + b) * 0.5f;
            var toEnd = end - mid;
            float reach = Length(toEnd); // Amedo
            toEnd *= 1f / reach;
            var axis = b - a;
            float bone = Length(axis);
            if (MathF.Abs(bone) <= 1e-4f) continue;
            axis *= 1f / bone;
            float cos = axis.X * toEnd.X + axis.Y * toEnd.Y + axis.Z * toEnd.Z;
            if (!(-0.996f <= cos && cos <= 0.996f)) continue;
            Emit(pass, k.Type, axis, mid, new Vector3(k.SizeX, reach, bone * 0.5f), k.Offset, chunkWorld, _planes);
        }
        if (shape.Rect is { } r)
            Emit(pass, r.Type, new Vector3(obj.M31, obj.M32, obj.M33), pos,
                new Vector3(r.SizeX, Length(end - pos), r.SizeZ), r.Offset, chunkWorld, _planes); // Amedo
        return true;
    }

    private static float Length(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z); // Amedo

    private static bool InView(Matrix4x4 world, PS2AnyOGI? ogi, Matrix4x4 viewProjection)
    {
        Span<Vector4> clip = stackalloc Vector4[8];
        int n = 1;
        if (ogi is not null && ogi.BoundingBox.Length >= 2)
        {
            var mn = ogi.BoundingBox[0];
            var mx = ogi.BoundingBox[1];
            n = 8;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) != 0 ? mx.X : mn.X, (i & 2) != 0 ? mx.Y : mn.Y, (i & 4) != 0 ? mx.Z : mn.Z);
                clip[i] = Vector4.Transform(new Vector4(Vector3.Transform(c, world), 1f), viewProjection);
            }
        }
        else clip[0] = Vector4.Transform(new Vector4(world.Translation, 1f), viewProjection);

        for (int plane = 0; plane < 5; plane++)
        {
            bool allOut = true;
            for (int i = 0; i < n && allOut; i++)
            {
                var v = clip[i];
                allOut = plane switch
                {
                    0 => v.X < -v.W,
                    1 => v.X > v.W,
                    2 => v.Y < -v.W,
                    3 => v.Y > v.W,
                    _ => v.W <= 0f,
                };
            }
            if (allOut) return false;
        }
        return true;
    }

    private static void Emit(GameShadowPass pass, byte type, in Vector3 axisZ, in Vector3 origin, in Vector3 sizeIn, in Vector3 offset, Matrix4x4 chunkWorld, Vector4[] planes)
    {
        var size = sizeIn; // Amedo 2026-10-27
        const float Eps = 2.4999998e-09f; // Amedo
        var up = Vector3.UnitY;
        var z = axisZ * GameMath.InvLength_11C030(Eps, axisZ.X, axisZ.Y, axisZ.Z);
        var x = Vector3.Cross(up, z);
        if (0.03f < x.X * x.X + x.Y * x.Y + x.Z * x.Z)
        {
            x *= GameMath.InvLength_11C030(Eps, x.X, x.Y, x.Z);
            z = Vector3.Cross(x, up);
            z *= GameMath.InvLength_11C030(Eps, z.X, z.Y, z.Z);
        }
        else
        {
            x = Vector3.UnitX;
            z = Vector3.UnitZ;
        }
        if (!ClipSegment_1FD800(planes, origin, origin - x * size.X - up * size.Y - z * size.Z, out var corner)) return; // Amedo
        size.Y = Length(origin - corner);
        var frame = new Matrix4x4(
            x.X, x.Y, x.Z, 0f,
            0f, 1f, 0f, 0f,
            z.X, z.Y, z.Z, 0f,
            origin.X, origin.Y, origin.Z, 1f);
        var scale = new Matrix4x4(
            size.X, 0f, 0f, 0f,
            0f, size.Y, 0f, 0f,
            0f, 0f, size.Z, 0f,
            offset.X, offset.Y, offset.Z, 1f);
        pass.Submit(type, scale * frame * chunkWorld);
    }

    // Amedo
    private static bool ClipSegment_1FD800(Vector4[] planes, Vector3 p0, Vector3 p1, out Vector3 q1)
    {
        float len = Length(p0 - p1);
        var dir = p1 - p0;
        if (!(MathF.Abs(len) <= 1e-4f)) dir *= 1f / len;
        var q0 = p0;
        q1 = p1;
        foreach (var n in planes)
        {
            float s0 = n.X * q0.X + n.Y * q0.Y + n.Z * q0.Z + n.W;
            float s1 = n.X * q1.X + n.Y * q1.Y + n.Z * q1.Z + n.W;
            if (s0 < 0f)
            {
                if (len <= -s0) return false;
                len += s0;
                q0 -= dir * s0;
            }
            if (s1 < 0f)
            {
                if (len <= -s1) return false;
                len += s1;
                q1 += dir * s1;
            }
        }
        return true;
    }

    private GameShadowShape? ResolveShape(ChunkSource src, ushort id, PS2AnyOGI? ogi)
    {
        if (_shapes.TryGetValue(id, out var cached)) return cached;
        var local = FindObject(src.Rm2, id);
        var global = FindObject(src.GlobalRm2, id);
        var shape = GameShadowDefs.HasShadowCommands(local) ? GameShadowDefs.FromPack(local)
                  : GameShadowDefs.HasShadowCommands(global) ? GameShadowDefs.FromPack(global)
                  : GameShadowDefs.Default(local ?? global, ogi);
        _shapes[id] = shape;
        return shape;
    }

    private static PS2AnyObject? FindObject(PS2AnyTwinsanityRM2? rm2, ushort id)
    {
        var objects = rm2?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
                          ?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
        return objects is not null && objects.ContainsItem(id) ? objects.GetItem<BaseTwinItem>(id) as PS2AnyObject : null;
    }

    private PS2AnyOGI? ResolveOgi(ChunkSource src, ushort id)
    {
        if (_ogis.TryGetValue(id, out var cached)) return cached;
        PS2AnyOGI? ogi = null;
        if (src.MeshTables is { } tables && MeshDecoder.ResolveDefaultOgiId(tables, id) is { } ogiId)
            ogi = MeshDecoder.GetRawOgi(tables, ogiId);
        _ogis[id] = ogi;
        return ogi;
    }

    private (Dictionary<int, Matrix4x4> Bind, int[] React) Skeleton(PS2AnyOGI ogi)
    {
        if (_skeletons.TryGetValue(ogi, out var s)) return s;
        var react = new int[256];
        Array.Fill(react, -1);
        foreach (var j in ogi.Joints)
            if (j.ReactId is >= 0 and < 255 && react[j.ReactId] < 0) react[j.ReactId] = j.Index; // Amedo
        s = (MeshDecoder.ComputeJointWorlds(ogi), react);
        _skeletons[ogi] = s;
        return s;
    }
}
