using System.Numerics;
using CrashEngine.Runtime.AgentLab;
using CrashEngine.Runtime.Character;
using CrashEngine.Runtime.Physics;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Camera;

// Amedo 2026-09-28
public sealed class RuntimeGameCamera : ICutsceneCamera
{
    private sealed record Zone(uint Address, ConvexHull Hull, Matrix4x4 Matrix, (Vector3 Min, Vector3 Max) Bounds, uint Priority, uint Id = 0);

    private static readonly int[][] AxisSources = [[2], [3], [], [1, 5], [0, 4]];
    private static readonly int[] ButtonSources = [19, 20, 16, 17];
    private const float DeadZone = 0.3f;

    private readonly AgentLabWorld _world;
    private readonly List<Zone> _zones = new();
    private readonly List<(AgentLabAgent Agent, uint Node)> _zoneTargets = new(); // Amedo 2026-09-29
    private readonly uint _inst, _node, _c, _motion;
    private uint _comp, _c9; // Amedo 2026-10-30
    private readonly Dictionary<CharacterController, (uint Comp, uint C9)> _cameras = new(ReferenceEqualityComparer.Instance); // Amedo 2026-10-30
    private readonly uint _vehicle; // Amedo 2026-09-29
    private readonly List<uint> _overlapping = new();
    public IReadOnlyList<uint> OverlappingZones => _overlapping;
    private readonly Dictionary<AgentLabAgent, uint> _proxies = new(); // Amedo 2026-09-29
    private readonly Dictionary<uint, AgentLabAgent> _proxyAgents = new();
    private CharacterController? _bound; // Amedo
    private AgentLabAgent? _by;
    private Matrix4x4 _pose = Matrix4x4.Identity;

    public CamMemory Memory { get; } = new();
    public GameCamera Camera { get; }
    public Matrix4x4 Pose => _pose;
    public bool HasPose => _havePose; // Amedo

    // Amedo
    public (Matrix4x4 View, int Fov, float Near, float Far) CellView_1F4E50(Matrix4x4 rootToChunk) =>
        (_pose * rootToChunk, Memory.I(_c9 + 0x1C), Memory.F(_c9 + 0x24), Memory.F(_c9 + 0x28));

    private static float FrustumAspect => Screen.Oleg.OverlayQueue.Widescreen_30A744 ? 16f / 9f : 4f / 3f; // Amedo
    private static float RenderAspect => Screen.Oleg.OverlayQueue.Widescreen_30A744 ? 1.7066667f : 1.28f; // Amedo

    // Amedo 2026-10-05
    public bool InFrustum_227380(Vector3 p)
    {
        var m = _pose;
        var d = p - m.Translation;
        float x = d.X * m.M11 + d.Y * m.M12 + d.Z * m.M13;
        float y = d.X * m.M21 + d.Y * m.M22 + d.Z * m.M23;
        float z = d.X * m.M31 + d.Y * m.M32 + d.Z * m.M33;
        float sy = 1f / MathF.Tan(Memory.I(_c9 + 0x1C) * (MathF.PI / 65536f));
        float sx = sy / RenderAspect; // Amedo
        float n = Memory.F(_c9 + 0x24), f = Memory.F(_c9 + 0x28);
        float zc = (f + n) / (f - n) * z - 2f * f * n / (f - n);
        x *= sx;
        y *= sy;
        return !(z < x) && !(x < -z) && !(z < y) && !(y < -z) && !(z < zc) && !(zc < -z);
    }
    // Amedo 2026-10-06
    public int BoxVisibility_1F5F90(Vector3 min, Vector3 max) =>
        BoxVisibility_1F5F90(_pose, Memory.I(_c9 + 0x1C), Memory.F(_c9 + 0x24), Memory.F(_c9 + 0x28), min, max); // Amedo

    // Amedo
    public static int BoxVisibility_1F5F90(Matrix4x4 m, int fov, float n, float f, Vector3 min, Vector3 max)
    {
        float sy = 1f / MathF.Tan(fov * (MathF.PI / 65536f));
        float sx = sy / FrustumAspect; // Amedo
        Span<int> outside = stackalloc int[6];
        bool crossing = false;
        for (int i = 0; i < 8; i++)
        {
            var p = new Vector3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z);
            var d = p - m.Translation;
            float x = (d.X * m.M11 + d.Y * m.M12 + d.Z * m.M13) * sx;
            float y = (d.X * m.M21 + d.Y * m.M22 + d.Z * m.M23) * sy;
            float z = d.X * m.M31 + d.Y * m.M32 + d.Z * m.M33;
            float zc = (f + n) / (f - n) * z - 2f * f * n / (f - n);
            Span<float> s = [z - x, z + x, z - y, z + y, z - zc, z + zc];
            for (int k = 0; k < 6; k++)
                if (s[k] < 0f) { outside[k]++; crossing = true; }
        }
        for (int k = 0; k < 6; k++)
            if (outside[k] == 8) return 0;
        return crossing ? 2 : 1;
    }

    public float FovDegrees { get; private set; } = 45f;

    // Amedo 2026-09-30
    private Matrix4x4 _prevPose = Matrix4x4.Identity;
    private float _prevFov = 45f;
    private bool _havePose, _snap;

    // Amedo 2026-09-30
    public void Step(CharacterPad pad, uint dtTicks)
    {
        var pose = _pose;
        float fov = FovDegrees;
        Update(pad, dtTicks);
        if (!_havePose || _snap)
        {
            _prevPose = _pose;
            _prevFov = FovDegrees;
            _havePose = true;
            _snap = false;
        }
        else
        {
            _prevPose = pose;
            _prevFov = fov;
            var f0 = new Vector3(pose.M31, pose.M32, pose.M33);
            var f1 = new Vector3(_pose.M31, _pose.M32, _pose.M33);
            float l = GameMath.Length(f0) * GameMath.Length(f1);
            if (GameMath.DistanceSq(pose.Translation, _pose.Translation) > 9f || (l > 1e-6f && GameMath.Dot(f0, f1) / l < 0.85f))
            {
                _prevPose = _pose;
                _prevFov = FovDegrees;
            }
        }
    }

    // Amedo
    public void Hold()
    {
        _prevPose = _pose;
        _prevFov = FovDegrees;
    }

    // Amedo 2026-09-30
    public Matrix4x4 ShownPose
    {
        get
        {
            float a = SimulationClock.Active ? SimulationClock.Alpha : 1f;
            if (!_havePose || a >= 1f) return _pose;
            var p = _prevPose;
            var c = _pose;
            static Vector3 Row(Matrix4x4 m, int r) => r switch
            {
                1 => new Vector3(m.M11, m.M12, m.M13),
                2 => new Vector3(m.M21, m.M22, m.M23),
                3 => new Vector3(m.M31, m.M32, m.M33),
                _ => new Vector3(m.M41, m.M42, m.M43),
            };
            static Vector3 NLerp(Vector3 x, Vector3 y, float t)
            {
                var v = Vector3.Lerp(x, y, t);
                float len = GameMath.Length(v);
                return len > 1e-6f ? v * (GameMath.Length(x) + (GameMath.Length(y) - GameMath.Length(x)) * t) / len : v;
            }
            var right = NLerp(Row(p, 1), Row(c, 1), a);
            var up = NLerp(Row(p, 2), Row(c, 2), a);
            var fwd = NLerp(Row(p, 3), Row(c, 3), a);
            var eye = Vector3.Lerp(Row(p, 4), Row(c, 4), a);
            return new Matrix4x4(right.X, right.Y, right.Z, c.M14, up.X, up.Y, up.Z, c.M24, fwd.X, fwd.Y, fwd.Z, c.M34, eye.X, eye.Y, eye.Z, c.M44);
        }
    }

    // Amedo 2026-09-30
    public float ShownFov
    {
        get
        {
            float a = SimulationClock.Active ? SimulationClock.Alpha : 1f;
            if (!_havePose || a >= 1f) return FovDegrees;
            return _prevFov + (FovDegrees - _prevFov) * a;
        }
    }
    public float Near { get; private set; } = 0.1f;
    public float Far { get; private set; } = 1500f;
    public (uint Inst, uint Node, uint C, uint Motion, uint Comp, uint C9) Addresses => (_inst, _node, _c, _motion, _comp, _c9);
    public uint CurrentZone => Memory.U(_comp + 0x38);
    public ulong EyeFlags => Memory.Q(_comp + 0x50 + 0x2F0 + 0x40);
    public Vector4 RigTarget => Memory.V(_comp + 0x50 + 0xE0 + 0x20); // Amedo 2026-09-29
    public Vector4 RigEye => Memory.V(_comp + 0x50 + 0x2F0 + 0x20);
    public IReadOnlyDictionary<string, int> Unimplemented => Camera.Unimplemented;
    public float RigFloat(uint offset) => Memory.F(_comp + 0x50 + offset); // Amedo
    public Vector4 ShakeOffset => Memory.V(GameCamera.Shake);
    public Action<float, float>? Rumble { get; set; }

    private RuntimeGameCamera(AgentLabWorld world, CharacterController c, Matrix4x4 crash)
    {
        _world = world;
        Camera = new GameCamera(Memory) { Collision = world.Collision };
        Camera.CharacterVelocity = _ => new Vector4((_bound ?? c).Velocity, 1f); // Amedo
        Camera.CharacterKind = _ => _world.PlayerCharacter?.Kind ?? c.Kind; // Amedo
        Camera.LinkedSecond_15FE98 = () => _world.PlayerCharacter?.Link?.Follower_15FE98() is { } fc && _world.Agents.FirstOrDefault(a => ReferenceEquals(a.OwnCharacter, fc)) is { } fa ? Proxy(fa) : 0u; // Amedo
        Camera.ObjectAt = obj => obj != 0 && _proxyAgents.TryGetValue(obj, out var pa) ? pa : null; // Amedo
        Camera.ObjectBodies = (min, max) => world.QueryBodies(min, max, 0xFFFFFFFF, 1, 0x10, null, requireAll: true);
        _inst   = Camera.Alloc(0x160, "crash instance");
        _node   = Camera.Alloc(0x70, "crash node");
        _c      = Camera.Alloc(0x310, "crash controller");
        _motion = Camera.Alloc(0x40, "crash motion");
        _vehicle = Camera.Alloc(0x1B0, "crash vehicle"); // Amedo 2026-09-29
        Camera.VehicleKind = _ => (_bound ?? c).Vehicle?.Kind ?? 0;
        Camera.VehicleVelocity = _ => new Vector4((_bound ?? c).Vehicle?.Velocity ?? Vector3.Zero, 1f); // Amedo
        Memory.U(_inst + 8, _node);
        Memory.B(_inst + 0x153, 2);
        Memory.U(_c + 0x10, _motion);
        LoadZones(world);
        Sync(c, crash);
        Camera.SetTime((uint)world.NowTicks, 0);
        _comp = Camera.CreatePlayerCamera(_inst, _c);
        _bound = c; // Amedo
        _c9 = Camera.CameraComponent(Camera.Handle(_comp + 0x20));
        _cameras[c] = (_comp, _c9); // Amedo 2026-10-30
        uint comp12 = Camera.Alloc(0x20, "crash comp12"); // Amedo 2026-09-29
        Memory.U(comp12 + 0x18, _c);
        Memory.U(_inst + 0xD8 + 0xC * 4, comp12);
        Memory.U(_inst + 0xD8 + 0x16 * 4, _comp);
        Camera.CreateGlobals(_inst, Camera.Handle(_comp + 0x20));
        Camera.CodeObject = code => _by?.CameraCodeObject(code) is { } o ? Proxy(o) : 0u;
        Camera.CodePosition = code => _by?.CameraCodePosition(code) is { } p ? new Vector4(p, 1f) : null;
        Camera.AgentPath = index => _by?.PathAt(index);
        world.CutsceneCamera = this;
        Camera.Rumble = (word, seconds) =>
        {
            world.Pad1?.RumbleSet_2B36D0(word, seconds); // Amedo
            Rumble?.Invoke(((word >> 7) & 0xFF) / 255f, seconds);
        };
        Camera.ShakeInit_26F508(GameCamera.Shake);
        Camera.ShakeTarget_177A90(Camera.Handle(_comp + 0x20));
        world.CameraImpulse = (strength, falloff, source) => Camera.Impulse_26F5E8(strength, falloff, GameCamera.Shake, new Vector4(source, 1f));
        world.CameraImpulseAxes = (ax, ay, az, falloff, source) => Camera.ImpulseAxes_26F7E8(ax, ay, az, falloff, GameCamera.Shake, new Vector4(source, 1f));
        world.Rumble_27B4F0 = Camera.Rumble_27B4F0; // Amedo
        FovDegrees = Memory.I(_c9 + 0x1C) * 360f / 65536f;
        Near = Memory.F(_c9 + 0x24);
        Far = Memory.F(_c9 + 0x28);
    }

    public static RuntimeGameCamera? Create(AgentLabWorld world)
    {
        if (world.PlayerCharacter is not { } c || world.Player is not { } player) return null;
        return new RuntimeGameCamera(world, c, player.GameMatrix);
    }

    private void LoadZones(AgentLabWorld world)
    {
        for (int lid = Constants.LEVEL_LAYOUT_1_SECTION; lid <= Constants.LEVEL_LAYOUT_8_SECTION; lid++)
        {
            var layout = world.Rm2.GetItem<BaseTwinSection>((uint)lid); // Amedo 2026-09-29
            var section = layout?.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_CAMERAS_SECTION);
            if (section is null) continue;
            var instances = layout!.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_INSTANCES_SECTION);
            for (int i = 0; i < section.GetItemsAmount(); i++)
                if (section.GetItem(i) is PS2AnyCamera cam) AddZone(cam, instances);
        }
    }

    private void AddZone(PS2AnyCamera cam, BaseTwinSection? instances)
    {
        uint zd = Camera.Alloc(0x90, "zone data");
        var m = Memory;
        m.U(zd, cam.CameraHeader);
        m.U(zd + 4, cam.UnkShort); // Amedo 2026-10-06
        m.F(zd + 0x10, cam.UnkFloat1);
        m.V(zd + 0x20, new Vector4(cam.UnkVector1.X, cam.UnkVector1.Y, cam.UnkVector1.Z, cam.UnkVector1.W));
        m.V(zd + 0x30, new Vector4(cam.UnkVector2.X, cam.UnkVector2.Y, cam.UnkVector2.Z, cam.UnkVector2.W));
        m.F(zd + 0x40, cam.UnkFloat2);
        m.F(zd + 0x44, cam.UnkFloat3);
        m.U(zd + 0x48, cam.UnkInt1);
        m.U(zd + 0x4C, cam.UnkInt2);
        m.U(zd + 0x50, cam.UnkInt3);
        m.U(zd + 0x54, cam.UnkInt4);
        m.U(zd + 0x58, cam.UnkInt5);
        m.U(zd + 0x5C, cam.UnkInt6);
        m.F(zd + 0x60, cam.UnkFloat4);
        m.F(zd + 0x64, cam.UnkFloat5);
        m.F(zd + 0x68, cam.UnkFloat6);
        m.F(zd + 0x6C, cam.UnkFloat7);
        m.U(zd + 0x70, cam.UnkInt7);
        m.U(zd + 0x74, cam.UnkInt8);
        m.U(zd + 0x78, cam.UnkInt9);
        m.F(zd + 0x7C, cam.UnkFloat8);
        m.B(zd + 0x80, cam.UnkByte);
        m.U(zd + 8, AddSub(cam.MainCamera1)); // Amedo 2026-09-29
        m.U(zd + 0xC, AddSub(cam.MainCamera2));
        uint obj = Camera.Alloc(0x180, "zone object");
        var t = cam.CamTrigger;
        m.U(obj + 0x18, (uint)LayoutTrigger.BaseFlags_1F5898(t.Header, (uint)t.ObjectActivatorMask) | 0x40000); // Amedo 2026-10-06
        m.U(obj + 0x170, zd);
        m.V(obj + 0x40, new Vector4(t.Position.X, t.Position.Y, t.Position.Z, 1f));
        int count = 0; // Amedo 2026-09-29
        if (instances is not null)
            foreach (var id in t.Instances)
            {
                if (count >= 32 || _world.Find(instances, id) is not { } agent) continue;
                uint inst = Camera.Alloc(0x160, "zone target instance");
                uint node = Camera.Alloc(0x70, "zone target node");
                m.U(inst + 8, node);
                _zoneTargets.Add((agent, node));
                m.U(obj + 0xE4 + (uint)count * 4, inst);
                count++;
            }
        m.B(obj + 0x19, (byte)count);
        var scale = new Vector3(t.Scale.X, t.Scale.Y, t.Scale.Z);
        var hull = ConvexHull.Box(-scale, scale);
        var matrix = Matrix4x4.CreateFromQuaternion(new Quaternion(t.Rotation.X, t.Rotation.Y, t.Rotation.Z, t.Rotation.W))
                   * Matrix4x4.CreateTranslation(t.Position.X, t.Position.Y, t.Position.Z);
        _zones.Add(new Zone(obj, hull, matrix, hull.Bounds(matrix), m.U(obj + 0x18) & 0xFF, cam.GetID())); // Amedo 2026-09-29
    }

    // Amedo 2026-09-29
    private uint AddSub(CameraSubBase? sub)
    {
        if (sub is null) return 0;
        uint a = Camera.Alloc(0x30, "zone sub-camera");
        Memory.U(a, sub.UnkInt);
        Memory.F(a + 4, sub.UnkFloat1);
        Memory.F(a + 8, sub.UnkFloat2);
        Camera.SubCameras[a] = SubCamera.Create(sub);
        return a;
    }

    private void Sync(CharacterController c, Matrix4x4 crash)
    {
        var m = Memory;
        m.M(_node, crash);
        m.V(_node + 0x40, new Vector4(crash.Translation, 1f));
        m.V(_node + 0x50, ToV(Quaternion.CreateFromRotationMatrix(crash)));
        m.Q(_node + 0x60, 0);
        m.V(_c + 0x60, new Vector4(c.Velocity, 1f)); // Amedo
        m.Q(_c + 0x70, c.State70);
        m.U(_c + 0xB0, c.Link is not null ? 1u : 0u); // Amedo
        if (c.Vehicle is { } vehicle) // Amedo 2026-09-29
        {
            m.U(_c + 0xB8, _vehicle);
            m.M(_vehicle + 0x10, vehicle.Frame);
            m.F(_vehicle + 0xEC, vehicle switch { Character.Vehicles.RollerBrawl ball => ball.CloudScale, Character.Vehicles.Humiliskate board => board.Reach, _ => 0f }); // Amedo
        }
        else m.U(_c + 0xB8, 0);
        m.V(_c + 0xD0, new Vector4(c.GroundPoint, 1f));
        m.F(_c + 0x278, c.CenterOffset);
        m.F(_c + 0x300, 0f);
        m.I(_motion + 0x14, c.MotionFlags);
        SyncFollowed(); // Amedo
        foreach (var (agent, node) in _zoneTargets) // Amedo 2026-09-29
        {
            var g = agent.GameMatrix;
            m.M(node, g);
            m.V(node + 0x40, new Vector4(g.Translation, 1f));
            m.V(node + 0x50, ToV(Quaternion.CreateFromRotationMatrix(g)));
            m.Q(node + 0x60, 0);
        }
    }

    private static Vector4 ToV(Quaternion q) => new(q.X, q.Y, q.Z, q.W);

    // Amedo 2026-09-29
    private uint Proxy(AgentLabAgent agent)
    {
        if (!_proxies.TryGetValue(agent, out uint obj))
        {
            obj = Camera.Alloc(0x80, "cutscene target");
            Memory.U(obj + 8, Camera.Alloc(0x70, "cutscene target node"));
            _proxies[agent] = obj;
            _proxyAgents[obj] = agent;
        }
        SyncProxy(obj, agent);
        return obj;
    }

    private void SyncProxy(uint obj, AgentLabAgent agent)
    {
        var m = Memory;
        uint node = m.U(obj + 8);
        var g = agent.GameMatrix;
        m.M(node, g);
        m.V(node + 0x40, new Vector4(g.Translation, 1f));
        m.V(node + 0x50, ToV(Quaternion.CreateFromRotationMatrix(g)));
        m.Q(node + 0x60, 0);
        agent.LocalBox(out var min, out var max);
        m.V(obj + 0x60, min);
        m.V(obj + 0x70, max);
    }

    private void SyncShotTargets()
    {
        for (uint n = 0; n < 2; n++)
            if (_proxyAgents.TryGetValue(Memory.U(Camera.G + 0x254 + n * 4), out var a)) SyncProxy(Memory.U(Camera.G + 0x254 + n * 4), a);
    }

    private string Tag(IReadOnlyList<uint> args, int op) => string.Create(System.Globalization.CultureInfo.InvariantCulture,
        $"[cutcam {_world.Now:0.00}s] {op} 0x{(args.Count > 0 ? args[0] : 0u):X8}");

    public void ToggleCutsceneCamera_111520(IReadOnlyList<uint> args)
    {
        if (_world.PlayerCharacter is { } c && _world.Player is { } player) Sync(c, player.GameMatrix);
        Camera.LoadCommand(args);
        Camera.ToggleCutsceneCamera_111520();
        _world.Log?.Invoke($"{Tag(args, 594)} t {(args.Count > 1 ? BitConverter.UInt32BitsToSingle(args[1]) : 0f):0.##}s -> camera {Camera.CameraIndex}"); // Amedo 2026-09-29
    }

    public void CutsceneTargets_1118C8(AgentLabAgent by, IReadOnlyList<uint> args)
    {
        _by = by;
        Camera.LoadCommand(args);
        Camera.CutsceneTargets_1118C8();
        _by = null;
        uint g = Camera.G;
        string Name(uint o)
        {
            if (o == 0) return "-";
            if (!_proxyAgents.TryGetValue(o, out var a)) return $"0x{o:X}";
            var p = Memory.V(Memory.U(o + 8) + 0x40);
            return string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"{a.Entity.Name} ({p.X:0.##},{p.Y:0.##},{p.Z:0.##}) box y {Memory.F(o + 0x64):0.##}..{Memory.F(o + 0x74):0.##} w {Memory.F(o + 0x6C):0.##}");
        }
        _world.Log?.Invoke($"{Tag(args, 595)} by {by.Entity.Name}: 1 {Name(Memory.U(g + 0x254))} 2 {Name(Memory.U(g + 0x258))} flags 0x{Memory.Q(g + 0x250):X}");
    }

    public void CutsceneShot_110360(List<uint> args)
    {
        SyncShotTargets();
        Camera.LoadCommand(args);
        Camera.CutsceneShot_110360();
        if (args.Count > 11) args[11] = Memory.U(Camera.Command + 0x38);
        uint g = Camera.G;
        var t = Memory.V(g + 0x3C0);
        var e = Memory.V(g + 0x440);
        _world.Log?.Invoke(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{Tag(args, 591)} eye ({e.X:0.##},{e.Y:0.##},{e.Z:0.##}) at ({t.X:0.##},{t.Y:0.##},{t.Z:0.##}) in {Memory.I(g + 0x3E0 + 0x7C) / 9000f:0.##}s"));
    }

    private AgentLabAgent? _followed; // Amedo
    private uint _followedObj;

    // Amedo
    public void SetCamera_10F1D0(uint flags, int angle, float value)
    {
        Camera.SetCamera_10F1D0(flags, angle, value);
        _world.Log?.Invoke($"[cutcam {_world.Now:0.00}s] 559 camera channels: flags 0x{flags & 3:X}, angle {angle}, value {value} (0x10F1D0)");
    }

    // Amedo
    public void Prepare_27DEC0() => Camera.CutDirectorPrepare_27DEC0(Camera.G + 0x210);
    public void PlayerCameraReset_15E538() => Camera.PlayerCameraReset_15E538(_comp + 0x50); // Amedo

    // Amedo
    public void CameraFocusObject_10F2D8(AgentLabAgent? focus)
    {
        uint obj;
        if (focus is null || ReferenceEquals(focus, _world.Player))
        {
            obj = Camera.PlayerObject();
            _followed = null;
        }
        else
        {
            if (!ReferenceEquals(_followed, focus))
            {
                _followedObj = Camera.Alloc(0x180, "camera follow object");
                Memory.U(_followedObj + 8, Camera.Alloc(0x70, "camera follow node"));
                _followed = focus;
            }
            SyncProxy(_followedObj, focus);
            obj = _followedObj;
        }
        if (Camera.Handle(_comp + 0x1A4) == obj) return;
        Memory.U(_comp + 0x1A4, obj == 0 ? 0u : Camera.HandleOf(obj));
        _world.Log?.Invoke($"[cutcam {_world.Now:0.00}s] {(focus is null ? 607 : 606)} the follow camera looks at {(_followed is { } f ? f.Entity.Name : "the player")} (0x10F2D8, node +0x1A4)");
    }

    // Amedo
    public void CameraNodeSetTarget_120110(AgentLabAgent target)
    {
        if ((Memory.Q(_comp + 0x30) & 0x80) != 0) return;
        uint obj;
        if (ReferenceEquals(target, _world.Player))
        {
            obj = Camera.PlayerObject();
            _followed = null;
        }
        else
        {
            if (!ReferenceEquals(_followed, target))
            {
                _followedObj = Camera.Alloc(0x180, "camera follow object");
                Memory.U(_followedObj + 8, Camera.Alloc(0x70, "camera follow node"));
                _followed = target;
            }
            SyncProxy(_followedObj, target);
            obj = _followedObj;
        }
        uint rig = _comp + 0x50;
        Memory.Q(rig + 0x150, ((Memory.Q(rig + 0x150) & ~0x21UL) | 0x20 | 0x2000) & ~0x8000UL);
        Memory.B(rig + 0x141, 0);
        if (Camera.Handle(rig + 0x154) != obj) Memory.U(rig + 0x154, obj == 0 ? 0u : Camera.HandleOf(obj));
        Memory.Q(rig + 0x338, (Memory.Q(rig + 0x338) | 0x40) & ~0x80UL);
        Memory.Q(rig + 0x330, (Memory.Q(rig + 0x330) & ~0x2000UL) | 0x2000);
        Memory.B(rig + 0x320, 0);
        _world.Log?.Invoke($"[cutcam {_world.Now:0.00}s] 623 the follow camera rig targets {target.Entity.Name} (0x1433F8)");
    }

    private void SyncFollowed()
    {
        if (_followed is null) return;
        if (_followed.Destroyed || _followed.Released)
        {
            CameraFocusObject_10F2D8(null);
            return;
        }
        SyncProxy(_followedObj, _followed);
    }

    // Amedo
    public void CameraNode_1201B8(int op, IReadOnlyList<uint> args)
    {
        Camera.LoadCommand(args);
        switch (op)
        {
            case 626: Camera.CameraNodeEnableFlags_1201B8(); break;
            case 627: Camera.CameraNodeClearFlags_111A90(); break;
            case 628: Camera.SetCameraNodeValue_111C68(); break;
            case 560: Camera.RestoreCameraDefaults_11FDC0(); break;
            case 652: Camera.CameraTopdownMode_112060(); break; // Amedo
        }
        _world.Log?.Invoke($"{Tag(args, op)} follow node: positioner flags 0x{Memory.Q(_comp + 0x380):X} target flags 0x{Memory.Q(_comp + 0x1A0):X} 0x630 0x{Memory.Q(_comp + 0x630):X}");
    }

    private static float Axis(CharacterPad pad, int slot)
    {
        float scale = 1f / (1f - DeadZone), result = 0f;
        foreach (int s in AxisSources[slot])
        {
            float v = pad.Axis(s);
            float r = v < 0f ? (v < -DeadZone ? (v + DeadZone) * scale : 0f) : (DeadZone < v ? (v - DeadZone) * scale : 0f);
            if (MathF.Abs(r) <= 5e-5f) continue;
            if (MathF.Abs(result) <= 5e-5f) result = r;
        }
        return result;
    }

    private static float Button(CharacterPad pad)
    {
        foreach (int s in ButtonSources)
        {
            float v = pad.GetButtonPressure_2B19D0(s);
            if (5e-5f < MathF.Abs(v)) return v;
        }
        return 0f;
    }

    public void Update(CharacterPad pad, uint dtTicks)
    {
        if (_world.PlayerCharacter is not { } c || _world.Player is not { } player) return;
        int mode = _world.RespawnMode != 0 ? _world.RespawnMode : _world.GameMode;
        bool play = mode == AgentLabWorld.ModePlay || mode == AgentLabWorld.ModeExitCutscene; // Amedo 2026-09-29
        bool cutscene = mode == AgentLabWorld.ModeCutscene || mode == AgentLabWorld.ModeCutsceneAlt || Camera.CameraIndex != 0;
        if (!play && !cutscene) return;
        Sync(c, player.GameMatrix);
        if (!ReferenceEquals(c, _bound)) // Amedo
        {
            _bound = c;
            SwitchCamera_177A90(c); // Amedo 2026-10-30
        }
        Camera.SetTime((uint)_world.NowTicks, dtTicks);
        if (play) PlayUpdate(pad, c, player);
        FinishUpdate(Memory.U(Camera.Handle(_comp + 0x20) + 8));
    }

    // Amedo 2026-10-30
    private void SwitchCamera_177A90(CharacterController c)
    {
        if (!_cameras.TryGetValue(c, out var cam))
        {
            uint comp = Camera.CreatePlayerCamera(_inst, _c);
            cam = (comp, Camera.CameraComponent(Camera.Handle(comp + 0x20)));
            _cameras[c] = cam;
        }
        (_comp, _c9) = cam;
        Memory.U(_inst + 0xD8 + 0x16 * 4, _comp);
        if (Camera.Handle(_comp + 0x1A4) != _inst) Memory.U(_comp + 0x1A4, Camera.HandleOf(_inst));
        uint obj = Camera.Handle(_comp + 0x20);
        Camera.SetActiveCamera_177A90(obj);
        Camera.ShakeTarget_177A90(obj);
        FovDegrees = Memory.I(_c9 + 0x1C) * 360f / 65536f;
        Near = Memory.F(_c9 + 0x24);
        Far = Memory.F(_c9 + 0x28);
    }

    // Amedo 2026-09-29
    private void PlayUpdate(CharacterPad pad, CharacterController c, AgentLabAgent player)
    {
        uint p = _comp + 0x30;
        Camera.ZoneRequestReset_15E9D8(p);
        var hullMatrix = player.GameMatrix;
        var (min, max) = c.TriggerHull.Bounds(hullMatrix);
        _overlapping.Clear(); // Amedo 2026-09-29
        foreach (var z in _zones)
        {
            if (((Memory.Q(z.Address + 0x18) >> 20) & 1) == 0) continue;
            if (!CollisionWorld.Overlap(min, max, z.Bounds.Min, z.Bounds.Max)) continue;
            if (!ConvexHull.Overlaps(c.TriggerHull, hullMatrix, z.Hull, z.Matrix)) continue;
            _overlapping.Add(z.Address);
            Camera.ZoneRequest_15E9F0(p, z.Address, z.Priority, _c);
        }
        Camera.SetInput(_comp + 0x50, Axis(pad, 0), Axis(pad, 1), Axis(pad, 2), Axis(pad, 3), Axis(pad, 4), Button(pad), 0f, 0f);
        c.LookStickX = Axis(pad, 0); // Amedo 2026-09-29
        c.LookStickY = Axis(pad, 1);
        Camera.PlayerCameraUpdate_172A48(_comp, GameCamera.TimeStruct, _inst, _c);
    }

    // Amedo 2026-09-29
    private void FinishUpdate(uint node)
    {
        Camera.NodeRefresh_18AFA8(node);
        var (pose, fov) = Camera.CameraUpdate_26FD80(_c9, GameCamera.TimeStruct, Memory.M(node));
        Memory.U(_c9 + 0x18, Memory.U(_c9 + 0x18) | 1);
        Memory.I(_c9 + 0x1C, fov);
        Camera.NodeSetMatrix_18AF40(node, pose);
        _pose = pose;
        FovDegrees = fov * 360f / 65536f;
    }

    public void Warp(Matrix4x4 transform)
    {
        Camera.CameraTransform_27B760(_c9, transform);
        _pose *= transform; // Amedo 2026-10-02
        _prevPose = _pose;
        _snap = true; // Amedo 2026-09-30
    }

    // Amedo 2026-09-29
    public string DescribeZone(uint address)
    {
        foreach (var z in _zones)
        {
            if (z.Address != address) continue;
            uint zd = Memory.U(z.Address + 0x170);
            string Sub(uint a) => a == 0 ? "-" : Camera.SubCameras.TryGetValue(a, out var s) ? s.Kind.ToString() : $"0x{Memory.U(a):X}";
            var t = z.Matrix.Translation;
            return string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"zone 0x{address:X} id {z.Id} (#{_zones.IndexOf(z)}) at ({t.X:0.##},{t.Y:0.##},{t.Z:0.##}) box ({z.Bounds.Min.X:0.#},{z.Bounds.Min.Y:0.#},{z.Bounds.Min.Z:0.#})..({z.Bounds.Max.X:0.#},{z.Bounds.Max.Y:0.#},{z.Bounds.Max.Z:0.#}) prio {z.Priority} header 0x{Memory.Q(zd):X} subs {Sub(Memory.U(zd + 8))}/{Sub(Memory.U(zd + 0xC))}");
        }
        return $"zone 0x{address:X} (unknown)";
    }

    // Amedo 2026-10-06
    public int CameraIndex => Camera.CameraIndex;

    // Amedo 2026-10-06
    public bool ZoneShape(uint address, out ConvexHull hull, out Matrix4x4 matrix)
    {
        foreach (var z in _zones)
        {
            if (z.Address != address) continue;
            hull = z.Hull;
            matrix = z.Matrix;
            return true;
        }
        hull = null!;
        matrix = default;
        return false;
    }

    // Amedo 2026-10-06
    public string? ZoneLabel(uint address)
    {
        for (int i = 0; i < _zones.Count; i++)
        {
            var z = _zones[i];
            if (z.Address != address) continue;
            uint zd = Memory.U(z.Address + 0x170);
            string Sub(uint a) => a == 0 ? "-" : Camera.SubCameras.TryGetValue(a, out var s) ? s.Kind.ToString() : "?";
            uint s1 = Memory.U(zd + 8), s2 = Memory.U(zd + 0xC);
            return $"Camera_{z.Id:X4} (zone #{i}), priority {z.Priority}, sub cameras: {(s1 == 0 && s2 == 0 ? "none" : $"{Sub(s1)} / {Sub(s2)}")}";
        }
        return null;
    }

    // Amedo 2026-09-28
    public void ReloadZones()
    {
        _zones.Clear();
        _zoneTargets.Clear(); // Amedo 2026-09-29
        LoadZones(_world);
    }
}
