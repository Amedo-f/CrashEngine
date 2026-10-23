using System.Numerics;
using CrashEngine.Runtime.Physics;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed partial class AgentLabAgent
{
    private const int WaterSurface = 12;

    private PhysicsController? _physics;
    private Vector3 _sphereCenter;
    private int _groundSurface = -1; // Amedo 2026-09-30
    private int _wetSurface = -1;
    private Vector3 _velocity;
    private Vector3 _prevVelocity;
    private byte _bump;

    public PhysicsController? Physics => _physics;
    public Vector3 SphereCenter => _sphereCenter;
    public float LogicalRadius => _logicalRadius;
    public int GroundSurface => _groundSurface;
    public Vector3 MotionVelocity => _velocity;

    private (Vector3 Min, Vector3 Max) CommittedBox => InChunk_A0 ? (Cell.BoxMin_40, Cell.BoxMax_50) : WorldBox(); // Amedo

    // Amedo
    private void SetPairType_246480(PhysicsController ctrl, int type)
    {
        ctrl.Owner_94 = this;
        if (type == 0)
        {
            if (ctrl.First_DC != 0xFFFF && ChunkA0 is { } c) c.ChunkRigidBodiesOf_1F1CF0().TakeFirst_252488(ctrl);
            ctrl.Flags &= ~PhysicsController.InPairList;
            ctrl.SetPairTypeBits(0);
            if (!(ctrl.TerrainType < 9)) return;
            ctrl.Body = null;
            if (_contactResponse is null) AgentFlags &= ~0x8000u;
            return;
        }
        var lists = ChunkA0?.ChunkRigidBodiesOf_1F1CF0();
        ctrl.SetPairTypeBits(type);
        if (!(ctrl.PairType < 9))
        {
            if (ctrl.Body is null) RigidTypes(ctrl, ctrl.PairType);
            if (ctrl.First_DC != 0xFFFF) lists?.TakeFirst_252488(ctrl);
            ctrl.Flags &= ~PhysicsController.InPairList;
            return;
        }
        if (!ctrl.Has(PhysicsController.InPairList))
        {
            lists?.PutFirst_252430(ctrl);
            ctrl.Flags |= PhysicsController.InPairList;
        }
        if (!(ctrl.TerrainType < 9)) return;
        ctrl.Body = null;
        if (_contactResponse is null) AgentFlags &= ~0x8000u;
    }

    // Amedo
    private void SetTerrainType_2467E0(PhysicsController ctrl, int type)
    {
        ctrl.Owner_94 = this;
        if (type == 0)
        {
            ctrl.Terrain = null;
            if (ctrl.Second_DE != 0xFF && ChunkA0 is { } c) c.ChunkRigidBodiesOf_1F1CF0().TakeSecond_252610(ctrl);
            ctrl.SetTerrainTypeBits(0);
            if (!(ctrl.PairType < 9)) return;
            ctrl.Body = null;
            if (_contactResponse is null) AgentFlags &= ~0x8000u;
            return;
        }
        ctrl.SetTerrainTypeBits(type);
        if (ctrl.Second_DE == 0xFF) ChunkA0?.ChunkRigidBodiesOf_1F1CF0().PutSecond_2525B8(ctrl);
        if (ctrl.Terrain is null && ctrl.TerrainType < 9) ctrl.Terrain = new TriangleCache();
        if (!(ctrl.TerrainType < 9))
        {
            if (ctrl.Body is null) RigidTypes(ctrl, ctrl.TerrainType);
            return;
        }
        if (!(ctrl.PairType < 9)) return;
        ctrl.Body = null;
        if (_contactResponse is null) AgentFlags &= ~0x8000u;
    }

    // Amedo
    internal void ReleaseRigidBody_23DED8()
    {
        if (_physics is not { } ctrl) return;
        if (ctrl.Lists_D8 is { } lists)
        {
            lists.TakeFirst_252488(ctrl);
            lists.TakeSecond_252610(ctrl);
        }
        _physics = null;
    }

    // Amedo
    internal void CanChangeChunk_23D060(WorldChunk target)
    {
        if (_physics is not { } ctrl) return;
        var to = target.ChunkRigidBodiesOf_1F1CF0();
        var from = ctrl.Lists_D8;
        bool first = from?.TakeFirst_252488(ctrl) ?? false;
        bool second = from?.TakeSecond_252610(ctrl) ?? false;
        if (first) to.PutFirst_252430(ctrl);
        if (second) to.PutSecond_2525B8(ctrl);
    }

    internal bool RigidGate_242C58 => SteppedNow || _attachedTo is not null; // Amedo

    private void RigidTypes(PhysicsController ctrl, int type)
    {
        if (type < 9 || ctrl.Body is not null) return;
        bool sphere = type == 9;
        var body = new RigidBody { IsBox = !sphere };
        if (sphere) body.SetBoxInertia(1f, 1.3f, 1.3f, 1.3f); // Amedo
        body.InitFromTransform(LocalMatrix);
        float mass = _contactResponse is { Count: > 24 } response && 0f < BitConverter.UInt32BitsToSingle(response[24])
            ? BitConverter.UInt32BitsToSingle(response[24])
            : RootController().FloatVar(0); // Amedo
        if (sphere) body.SetBoxInertia(mass, 1f, 1f, 1f);
        body.Bounce = 0.3f;
        body.Grip = 0.8f;
        body.Flags2 |= RigidBody.SyncTransform;
        body.CollisionMask = 0x52030;
        body.SurfaceEvent = SurfaceEvent_28B918; // Amedo 2026-09-30
        if (sphere)
        {
            body.IsEllipsoid = false;
            body.Radius = _logicalRadius;
        }
        else
        {
            body.BuildHulls = BuildHulls;
            body.BoxFloat = () => BoxBuoyancy_28F2A8(body, body.WaterLevel); // Amedo
            body.OwnerBox = () => TreeBox; // Amedo
        }
        ctrl.Flags &= ~(PhysicsController.VerticalGravity | PhysicsController.FullGravity);
        ctrl.Body = body;
        AgentFlags |= 0x8000; // Amedo
    }

    private static readonly int[] BoxCorners_1F9B78 = { 0, 1, 5, 4, 2, 3, 7, 6 }; // Amedo

    // Amedo
    private void BoxBuoyancy_28F2A8(RigidBody body, float water)
    {
        if (!_hasBox) return;
        var m = LocalMatrix;
        foreach (int i in BoxCorners_1F9B78) // Amedo
        {
            var corner = Vector3.Transform(new Vector3((i & 1) == 0 ? _boxMin.X : _boxMax.X,
                                                       (i & 2) == 0 ? _boxMin.Y : _boxMax.Y,
                                                       (i & 4) == 0 ? _boxMin.Z : _boxMax.Z), m);
            float depth = water - corner.Y;
            if (!(0.1f < depth)) continue;
            var v = body.PointVelocityAt_289408(corner);
            body.ApplyForceAt(new Vector3(0f, depth * body.Mass * body.Buoyancy - v.Y * 0.3f, 0f), corner);
        }
    }

    private List<ConvexHull>? BuildHulls() // Amedo 2026-09-27
    {
        var ogi = Ogi;
        if (ogi is null || _physics?.Body is not { } body) return null;
        var hulls = new List<ConvexHull>();
        for (int i = 0; i < OgiHulls.Count(ogi); i++) // Amedo
        {
            var m = Matrix4x4.Identity;
            if (OgiHulls.Joint(ogi, i) is var j and not 0xFF) // Amedo
                m = JointsOf(ogi).TryGetValue(j, out var joint) ? joint : Matrix4x4.Identity;
            m.Translation -= Vector3.TransformNormal(body.ComOffset, m);
            var hull = OgiHulls.Fresh(ogi, i); // Amedo
            hull.Transform(m);
            hulls.Add(hull);
        }
        return hulls;
    }

    private Vector3 BoxExtent
    {
        get
        {
            if (!_hasBox) LoadBox();
            return _hasBox ? _boxMax - _boxMin : Vector3.Zero;
        }
    }

    private List<uint>? _contactResponse;

    // Amedo
    private void SetMassAndSize_2462F8(PhysicsController ctrl, RigidBody body, float v)
    {
        float mass = _contactResponse is { Count: > 24 } r && 0f < BitConverter.UInt32BitsToSingle(r[24])
            ? BitConverter.UInt32BitsToSingle(r[24])
            : RootController().FloatVar(0); // Amedo
        int type = ctrl.TerrainType;
        if (type == 9) body.SetBoxInertia(mass, v, v, v);
        else if (type is 10 or 11)
        {
            var size = BoxExtent * v;
            body.SetBoxInertia(mass, size.X, size.Y, size.Z);
        }
    }

    // Amedo
    private PhysicsController RigidBody_23DE78() => _physics ??= new PhysicsController();

    private bool _gravity88; // Amedo

    // Amedo
    private void SetRigidBodyGravity_253988(PhysicsController ctrl, float g)
    {
        ctrl.Gravity = g;
        if (g == 0f)
        {
            ClearRigidBodyGravity_253A08(ctrl);
            return;
        }
        ctrl.Flags |= PhysicsController.Gravitates;
        _gravity88 = true;
    }

    // Amedo
    private void ClearRigidBodyGravity_253A08(PhysicsController ctrl)
    {
        ctrl.Flags &= ~PhysicsController.Gravitates;
        _gravity88 = false;
    }

    private void SetCollisions(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        uint f = Arg(5);
        var (bmin, bmax) = CommittedBox;
        var ext = bmax - bmin;
        float ratio = F(13);
        if ((f & 0x400) != 0) _logicalRadius = F(6);
        if ((f & 0x08000000) != 0)
        {
            float m = ext.X;
            if (m < ext.Y) m = ext.Y;
            if (m < ext.Z) m = ext.Z;
            _logicalRadius = m * 0.5f;
        }
        else if ((f & 0x10000000) != 0)
        {
            float half = ext.Y * 0.5f;
            _logicalRadius = half;
            ratio = MathF.Max(ext.Z * 0.5f, ext.X * 0.5f) * (1f / half);
        }

        var ctrl = _physics;
        if ((f & 0x100) != 0)
        {
            if ((f & 0xF) == 0) { if (ctrl is not null) SetPairType_246480(ctrl, 0); } // Amedo
            else
            {
                ctrl ??= new PhysicsController();
                SetPairType_246480(ctrl, (int)(f & 0xF)); // Amedo
            }
        }
        if ((f & 0x200) != 0)
        {
            if ((f & 0xF0) == 0)
            {
                if (ctrl is null) return;
                SetTerrainType_2467E0(ctrl, 0); // Amedo
            }
            else
            {
                ctrl ??= new PhysicsController();
                SetTerrainType_2467E0(ctrl, (int)((f >> 4) & 0xF)); // Amedo
            }
        }
        if (ctrl is null) return;
        _physics = ctrl;

        if ((f & 0x80000000) != 0) ctrl.State |= 0x800000;
        if ((f & 0x800) != 0) SetRigidBodyGravity_253988(ctrl, RootController().FloatValueOf(Arg(15))); // Amedo
        else ClearRigidBodyGravity_253A08(ctrl); // Amedo
        if ((f & 0x1000) != 0) ctrl.Flags |= PhysicsController.Bit55;
        else ctrl.Flags &= ~PhysicsController.Bit55;
        if ((f & 0x100000) != 0) ctrl.Flags |= PhysicsController.Airborne;
        ctrl.SetState(0x4, (f & 0x2000000) != 0);
        ctrl.SetState(0x8, (f & 0x4000000) != 0);
        ctrl.Ratio = ratio;
        ctrl.ParamBC = F(14);
        if ((f & 0x20000000) != 0) ctrl.State |= 0x200000;
        else if ((f & 0x40000000) != 0) ctrl.State |= 0x400000;
        if ((ctrl.Flags & (0xFFUL << 32)) == 0)
        {
            ReleaseRigidBody_23DED8(); // Amedo
            return;
        }
        if ((f & 0x2000) != 0) { ctrl.SetDamping(F(7)); if (ctrl.Body is { } b1) b1.Damping = F(7); }
        if ((f & 0x1000000) != 0) { ctrl.SetBounce(F(8)); if (ctrl.Body is { } b2) b2.Decel = ctrl.Damping; }
        else if (F(8) < 0f) ctrl.State |= 0x2000000;
        if ((f & 0x4000) != 0) { ctrl.SetGrip(F(9)); if (ctrl.Body is { } b3) b3.Grip = F(9); }
        if ((f & 0x8000) != 0) { ctrl.SetB4(F(10)); if (ctrl.Body is { } b4) b4.Bounce = F(10); }
        else if ((f & 0x400000) != 0) { ctrl.ParamB4 = F(10); ctrl.Flags |= PhysicsController.TiltPowerSet; }
        if ((f & 0x10000) != 0)
        {
            ctrl.SetB8(F(11));
            if (ctrl.Body is { } sized) SetMassAndSize_2462F8(ctrl, sized, F(11)); // Amedo
        }
        else if ((f & 0x200000) != 0) { ctrl.MaxTilt = F(11); ctrl.Flags |= PhysicsController.MaxTiltSet; } // Amedo
        if ((f & 0x800000) != 0 && ctrl.Body is { } b5) { b5.Twist = F(12); b5.Roll = F(12); } // Amedo
        if ((f & 0x80000) != 0) ctrl.Body?.SetCenterOfMass_28B6F0(new Vector3(F(1), F(2), F(3))); // Amedo
        if (ctrl.Body is null) ctrl.Flags = (ctrl.Flags & ~(3UL << 40)) | ((ulong)((f >> 17) & 3) << 40);
        ctrl.Flags &= ~PhysicsController.Bit57;
        ctrl.State |= PhysicsController.StateCounter;
    }

    internal bool SteppedNow => _lastStep == World.NowTicks; // Amedo

    // Amedo
    internal void ClockStopped_22FC58() => _wakeOnce = false;

    // Amedo 2026-09-30
    internal void GatedStep_22F758()
    {
        if (_wobble is { } w && ((WArg(w.Args, 27) >> 15) & 1) != 0) PreWobble();
        if (_physics?.Body is not null) PhysicsPrestep(false); // Amedo
    }

    internal void PhysicsPrestep(bool ran = true) // Amedo
    {
        float dt = StepDt;
        if (_physics is { } ctrl)
        {
            if ((ctrl.Flags & (PhysicsController.Damped | PhysicsController.Gravitates | PhysicsController.Launched)) != 0 && (AgentFlags & 0x40) == 0) // Amedo
                StepRigidBody_24B510(ctrl, ran, dt); // Amedo
            var (bmin, bmax) = CommittedBox;
            _sphereCenter = (bmax - bmin) * 0.5f + bmin;
            bool had = (ctrl.Flags & (PhysicsController.TouchedAgent | PhysicsController.TouchedTerrain)) != 0;
            ctrl.State = (ctrl.State & ~PhysicsController.StateHadContact) | (had ? PhysicsController.StateHadContact : 0UL);
            ctrl.State &= ~(PhysicsController.StateFloor | PhysicsController.StateWall);
            ctrl.Flags &= ~(PhysicsController.TouchedAgent | PhysicsController.TouchedTerrain | PhysicsController.TouchedTerrainNow);
            if ((ctrl.Flags & (1UL << 54)) != 0) ReleaseRigidBodyAtRest_23D790(); // Amedo
        }
        if (OwnCharacter is null && _bump > 0) _bump--; // Amedo
    }

    // Amedo
    private void ReleaseRigidBodyAtRest_23D790()
    {
        if (_physics is not { } ctrl || !ctrl.Has(PhysicsController.TouchedTerrain)) return;
        if (_velocity.X * _velocity.X + _velocity.Y * _velocity.Y + _velocity.Z * _velocity.Z < 0.001f) ReleaseRigidBody_23DED8();
    }

    // Amedo 2026-09-29
    private void ForceVolumeController_24F340(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        var force = new Vector4(FloatValueOf(Arg(0)), FloatValueOf(Arg(1)), FloatValueOf(Arg(2)), 1f);
        var (min, max) = CommittedBox; // Amedo
        if (World.TriggersIn_1ECFC0(min, max, Chunk).FirstOrDefault() is not { } t) return;
        t.Force = force;
        uint flags = Arg(9);
        if ((flags & 1) != 0) t.ForceOn = true;
        else if ((flags & 2) != 0) t.ForceOn = false;
        Trace($"force volume {t.Name} = {force} ({(t.ForceOn ? "on" : "off")})");
    }

    // Amedo
    private void StepRigidBody_24B510(PhysicsController ctrl, bool ran, float dt)
    {
        if (ctrl.Body is { } body && (body.Flags & RigidBody.Asleep) == 0)
        {
            if (!ran) return;
            if (_contactResponse is { } r && (WArg(r, 23) & 0x4000) != 0) ForceVolumes_247930();
            if (!ctrl.Has(PhysicsController.Gravitates)) return;
            body.Force += new Vector3(0f, -ctrl.Gravity * body.Mass, 0f);
            return;
        }
        Integrate(ctrl, dt);
    }

    private void Integrate(PhysicsController ctrl, float dt)
    {
        if ((ctrl.Flags & (PhysicsController.VerticalGravity | PhysicsController.FullGravity | PhysicsController.Launched)) == 0) return;
        if ((ctrl.State & PhysicsController.StateCounter) != PhysicsController.StateCounter)
            ctrl.State = (ctrl.State & ~PhysicsController.StateCounter) | (((((ctrl.State >> 11) & 0xF) + 1) & 0xF) << 11);
        if (ctrl.Has(PhysicsController.Bit57) // Amedo 2026-09-30
            && ((ctrl.Flags & (PhysicsController.TouchedAgent | PhysicsController.TouchedTerrain)) != 0 || _velocity.LengthSquared() < 0.001f)
            && (ctrl.State & PhysicsController.StateCounter) == PhysicsController.StateCounter)
        {
            ctrl.Flags &= ~(PhysicsController.Launched | PhysicsController.Bit57);
            return;
        }

        _prevVelocity = _velocity;
        if (!ctrl.Has(PhysicsController.Gravitates)) return;
        _velocity.Y -= ctrl.Gravity * dt;
        if (ctrl.Has(PhysicsController.Damped)) _velocity *= 1f - ctrl.Damping;
        if (ctrl.Has(PhysicsController.Gripped) && (ctrl.Flags & (PhysicsController.TouchedAgent | PhysicsController.TouchedTerrain)) != 0 && 0f < ctrl.GripNow)
        {
            if (1f < ctrl.GripNow) ctrl.GripNow = 1f;
            _velocity *= 1f - ctrl.GripNow;
        }
        ctrl.GripNow = 0f;

        bool full = (ctrl.Flags & (PhysicsController.VerticalGravity | PhysicsController.FullGravity)) == PhysicsController.FullGravity
                    || ctrl.Has(PhysicsController.Launched);
        var step = full ? _velocity * dt : new Vector3(0f, _velocity.Y * dt, 0f);
        if ((ctrl.Flags & (0xFUL << 36)) != 0) step = HoldRigidBodyMove_2540E8(step); // Amedo
        if (MathF.Abs(step.X) <= 5e-5f && MathF.Abs(step.Y) <= 5e-5f && MathF.Abs(step.Z) <= 5e-5f) return;
        SetLocalPosition(LocalMatrix.Translation + step);
    }

    internal void TerrainPass(CollisionWorld world, float dt)
    {
        if (_physics is not { Terrain: { } cache } ctrl) return;
        var (min, max) = WorldBox();
        cache.Refresh(world, min, max);
        if ((ctrl.Flags & PhysicsController.Inactive) != 0) return;
        int type = ctrl.TerrainType;
        bool hit;
        if (type == 6)
        {
            World.Unimplemented("physics", type, "terrain handler 0x248E88");
            hit = false;
        }
        else if (type == 5) hit = SphereOnTerrain(ctrl, cache, world, dt, new Vector3(_logicalRadius * ctrl.Ratio, _logicalRadius, _logicalRadius * ctrl.Ratio)); // Amedo 2026-10-05
        else hit = SphereOnTerrain(ctrl, cache, world, dt);
        if (hit) ctrl.Flags |= PhysicsController.TouchedTerrainNow | PhysicsController.TouchedTerrain;
    }

    private bool SphereOnTerrain(PhysicsController ctrl, TriangleCache cache, CollisionWorld world, float dt, Vector3? ellipsoid = null)
    {
        var start = _sphereCenter;
        var cur = start;
        float r = _logicalRadius;
        float bestY = -10000f;
        int surface = 0xFFFF;
        bool solid = false, wet = false;
        foreach (var tri in cache.Triangles)
        {
            CollisionWorld.Plane(tri.A, tri.B, tri.C, out var n, out _);
            if (!ctrl.Has(PhysicsController.Airborne) && !(0f <= Vector3.Dot(n, _velocity))) continue;
            Vector3 pushed;
            if (ellipsoid is { } radii ? !CollisionWorld.EllipsoidTriangle_280258(tri, cur, radii, out pushed) : !CollisionWorld.SphereTriangle(tri, cur, r, out pushed)) continue; // Amedo 2026-10-05
            var s = world.SurfaceAt(tri.Surface);
            if (((s.Mask >> 6) & 1) == 0)
            {
                TouchWater(world, tri, pushed);
                wet = true;
                continue;
            }
            solid = true;
            var back = cur - pushed;
            cur = pushed;
            if (bestY < back.Y) { surface = s.Id; bestY = back.Y; }
            if (ctrl.Has(PhysicsController.Gripped))
            {
                float g = ctrl.Grip * s.Grip;
                if (ctrl.GripNow < g) ctrl.GripNow = g;
            }
        }

        if (solid)
        {
            _groundSurface = surface;
            if (surface != 0xFFFF && world.SurfaceAt(surface).Event) ctrl.State |= PhysicsController.StateSurfaceEvent; // Amedo 2026-09-27
            var move = (cur - start) * 0.99f;
            if (!(MathF.Abs(move.X) <= 5e-5f && MathF.Abs(move.Y) <= 5e-5f && MathF.Abs(move.Z) <= 5e-5f))
                SetLocalPosition(LocalMatrix.Translation + move);
            Landing(ctrl, move, dt);
        }
        else if (ctrl.Has(PhysicsController.Airborne)) ctrl.AirTime += dt;
        if (!wet) _wetSurface = -1;
        return solid;
    }

    private Vector3 _waterPoint;
    private double _lastSplash150 = double.NegativeInfinity; // Amedo

    public bool InWater => _wetSurface != -1;

    private void TouchWater(CollisionWorld world, CollisionWorld.Triangle tri, Vector3 pos) // Amedo 2026-09-27
    {
        var s = world.SurfaceAt(tri.Surface);
        if (s.Id != WaterSurface) return;
        CollisionWorld.Plane(tri.A, tri.B, tri.C, out var n, out float w);
        _waterPoint = pos - n * (Vector3.Dot(n, pos) + w);
        if (_wetSurface != WaterSurface) WaterEnter_22EF18(s); // Amedo
        else WaterStay_22F220(s, n);
        _wetSurface = WaterSurface;
        if (_physics is { } ctrl && (ctrl.State & WaterHurtsState) != 0) // Amedo
            SurfaceContact_vt4C(new CollisionWorld.Surface(0u, 0f, WaterSurface, HitSource: _waterPoint, HitKind: 0u, HitAmount: 1), this);
    }

    private const ulong WaterHurtsState = 0x2000000; // Amedo

    // Amedo
    private Vector3 WaterVelocity => ControllerType == 12 && OwnCharacter is { } c ? c.Velocity : _physics?.Body is { } body ? body.Velocity : _velocity;

    // Amedo
    private void WaterEnter_22EF18(CollisionWorld.Surface s)
    {
        var v = WaterVelocity;
        var at = new Vector3(_waterPoint.X + v.X * 0.12f, _waterPoint.Y, v.Z * 0.12f + _waterPoint.Z);
        if (0.5 < World.Now - _lastSplash150)
        {
            Trace($"water enter splash at {at}");
            SurfaceParticles_21A5B0(s, at);
            SurfaceSound_2248C8(s, at);
            _lastSplash150 = World.Now;
        }
        if (_physics?.Body is not { } body || !(v.Y < 0f)) return;
        float spin = MathF.Abs(body.Spin.Y) - 9f;
        if (!(0f < spin)) return;
        body.SetPosition_2914B0(new Vector3(body.Position.X, at.Y, body.Position.Z));
        float across = MathF.Sqrt(v.X * v.X + v.Z * v.Z) / -v.Y;
        float bounce = spin * 0.01f + 0.05f;
        if (across < (body.IsBox ? 3.5f : 2.5f)) bounce = 0f;
        else bounce += across * 3f;
        if (0.87f < bounce) bounce = 0.87f;
        Trace($"rigid body skims the water (spin {body.Spin.Y:0.##}, bounce {bounce:0.###})");
        v.Y *= -bounce;
        body.SetVelocity_2915D0(v);
    }

    // Amedo
    private void WaterStay_22F220(CollisionWorld.Surface s, Vector3 normal)
    {
        var v = WaterVelocity;
        float speed = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
        float chance = speed * 0.001f + 0.01f;
        if (0.5f < chance) chance = 0.5f;
        var at = new Vector3(v.X * 0.14f + _waterPoint.X, _waterPoint.Y, v.Z * 0.14f + _waterPoint.Z);
        if (World.Rng.GetRandFloat_18C0B8() <= chance) ScatterRipple_23C848(at); // Amedo
        if (ControllerType == 12) return;
        if (!(4f < speed) || !(0.5 < World.Now - _lastSplash150)) return;
        SurfaceSound_2248C8(s, at);
        _lastSplash150 = World.Now;
        if (36f < v.X * normal.X + v.Y * normal.Y + v.Z * normal.Z) SurfaceParticles_21A5B0(s, at);
    }

    // Amedo
    private void SurfaceParticles_21A5B0(CollisionWorld.Surface s, Vector3 at)
    {
        ushort pid = s.ParticleAt(0);
        var dir = new Vector3(0.9f, 0f, 0f);
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 3f);
        for (int i = 0; i < 6; i++)
        {
            ScatterRipple_23C848(at);
            if (pid != 0xFFFF) World.Particles?.Spawn(pid, 1, true, RootPoint(new Vector3(at.X + dir.X, at.Y + dir.Y - 0.2f, at.Z + dir.Z)));
            dir = Vector3.Transform(dir, turn);
        }
    }

    // Amedo
    private void SurfaceSound_2248C8(CollisionWorld.Surface s, Vector3 at)
    {
        ushort id = s.SoundAt(0, out float volume);
        if (id != 0xFFFF) World.Sounds?.PlayAt(id, RootPoint(at), volume, -1f, $"{Entity.Name} water (0x2248C8)", SoundAlive);
    }

    // Amedo
    private void ScatterRipple_23C848(Vector3 at)
    {
        var j = Jitter_23C848(Vector3.Zero, 0.4f, 0f, 1f, 1f); // Amedo
        float x = j.X, y = j.Y, z = j.Z;
        Ripple_1AE158(Matrix4x4.CreateTranslation(at + new Vector3(x, y, z)));
    }

    // Amedo 2026-10-05
    private void Impact_23D6A8(float f)
    {
        if (6.4e-11f < f) { if (_bump < 0xFC) _bump += 4; }
        else if (1.6e-11f < f) { if (_bump < 0xFD) _bump += 3; }
        else if (4e-12f < f) { if (_bump < 0xFE) _bump += 2; }
        else if (1e-12f < f && _bump < 0xFF) _bump += 1;
    }

    internal float Impact_229220 => _bump * 0.003921569f; // Amedo 2026-10-05

    private void ClearImpact_2530D0() => _bump = 0; // Amedo

    internal void SetBump_154(byte value) => _bump = value; // Amedo

    private void Landing(PhysicsController ctrl, Vector3 push, float dt)
    {
        bool airborne = ctrl.Has(PhysicsController.Airborne);
        if (!airborne && !(Vector3.Dot(push, _velocity) < 0f)) return;
        push *= InverseLength(push);
        float along = Vector3.Dot(_velocity, push);
        if (along != 0f) _velocity -= push * along;
        float bump = (1f - Vector3.Dot(push, _velocity)) * ((push.X * push.X + push.Z * push.Z) * 0.3f);
        Impact_23D6A8(bump); // Amedo 2026-10-05
        if (ctrl.Has(PhysicsController.Gripped) && ctrl.GripNow == 0f) ctrl.GripNow = 0.8f;
        if (0.7f < push.Y) ctrl.State |= PhysicsController.StateFloor;
        else if (MathF.Abs(push.Y) < 0.3f) ctrl.State |= PhysicsController.StateWall;
        if (ctrl.Has(PhysicsController.MaxTiltSet) && push.Y < ctrl.MaxTilt) _velocity.Y = 0f;

        var n = ctrl.GroundNormal;
        if (airborne)
        {
            float t = dt * (ctrl.Has(PhysicsController.TiltPowerSet) ? ctrl.ParamB4 : 6f);
            if (1f < t) t = 1f;
            n += (push - n) * t;
        }
        else if ((ctrl.State & PhysicsController.StateSmoothNormal) != 0) n += (push - n) * (dt * 5f);
        else n = push;
        ctrl.GroundNormal = n * InverseLength(n);
        if (airborne && !(ctrl.Has(PhysicsController.MaxTiltSet) && ctrl.GroundNormal.Y < ctrl.MaxTilt)) ctrl.AirTime = 0f;
    }

    private void StartContactRigid(List<uint> r, int mode) // Amedo 2026-09-27
    {
        uint Arg(int i) => i < r.Count ? r[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        if (Tracing) Trace($"contact rigid mode {mode} mass {FloatVar(0)} [{string.Join(",", r.Select(x => x.ToString("X")))}]");
        ReleaseRigidBody_23DED8(); // Amedo
        var ctrl = new PhysicsController();
        _physics = ctrl;
        int type = mode == 2 ? 9 : 10;
        SetPairType_246480(ctrl, type); // Amedo
        var body = ctrl.Body!;
        if (mode == 2 || (Arg(23) & 8) == 0) SetTerrainType_2467E0(ctrl, type); // Amedo
        else
        {
            StopRigidBodyCollisions_253CA0(ctrl); // Amedo
            body.Flags2 |= RigidBody.NoTerrain;
        }
        if (mode == 1) { ctrl.SetDamping(F(0)); body.Damping = F(0); } // Amedo
        float mass = RootController().FloatVar(0); // Amedo
        if (mode == 2) body.SetBoxInertia(mass, 1f, 1f, 1f);
        else { var size = BoxExtent; body.SetBoxInertia(mass, size.X, size.Y, size.Z); }
        if (F(0) != 0f) { ctrl.SetDamping(F(0)); body.Damping = F(0); }
        if (F(3) != 0f) SetRigidBodyGravity_253988(ctrl, F(3)); // Amedo
        if (F(2) != 0f)
        {
            ctrl.SetB8(F(2));
            float m = 0f < F(24) ? F(24) : mass;
            if (body.IsBox) { var size = BoxExtent * F(2); body.SetBoxInertia(m, size.X, size.Y, size.Z); }
            else body.SetBoxInertia(m, F(2), F(2), F(2));
        }
        if (F(4) != 0f) { ctrl.SetGrip(F(4)); body.Grip = F(4); }
        if (F(6) != 0f) { ctrl.SetB4(F(6)); body.Bounce = F(6); }
        if (F(5) != 0f) { body.Twist = F(5); body.Roll = F(5); }
        if (F(7) != 0f) { ctrl.SetBounce(F(7)); body.Decel = ctrl.Damping; }
        uint flags = Arg(23);
        if ((Arg(27) & 0x100) != 0) body.SetCenterOfMass_28B6F0(new Vector3(F(12), F(13), F(14))); // Amedo
        body.Substeps = (int)((flags >> 11) & 7);
        float buoyancy = (flags & 0x80000) != 0 ? F(9) : -1f;
        if (0f < buoyancy) body.Buoyancy = buoyancy;
        if ((flags & 0x10000) != 0) body.Flags |= 0x20;
        if ((flags & 0x20000) != 0) body.Flags |= 0x40;
        if ((flags & 0x3C0) != 0) // Amedo 2026-09-29
        {
            body.UpdateDerived();
            var c = body.Constraint ??= new RigidConstraint();
            var v = Vector3.TransformNormal(new Vector3(F(19), F(20), F(21)), GameMatrix);
            switch ((flags >> 6) & 0xF)
            {
                case 1: c.Point_291128(body.Position); break;
                case 2: c.Line_291148(body.Position, v); break;
                case 3: c.Plane_2911E0(v, body.Position); break;
            }
        }
        if ((flags & 7) == 7) body.Flags2 |= RigidBody.PositionOnly;
        else if ((flags & 7) != 0) // Amedo 2026-09-29
        {
            body.UpdateDerived();
            var c = body.Constraint ??= new RigidConstraint();
            if ((flags & 1) != 0) c.Axis_2880F8(Vector3.UnitX, Vector3.TransformNormal(Vector3.UnitX, body.World));
            if ((flags & 2) != 0) c.Axis_2880F8(Vector3.UnitY, Vector3.TransformNormal(Vector3.UnitY, body.World));
            if ((flags & 4) != 0) c.Axis_2880F8(Vector3.UnitZ, Vector3.TransformNormal(Vector3.UnitZ, body.World));
            if ((flags & 0x40000) != 0) c.Limit_2910B8(F(25), body.Orientation);
        }
        if (mode == 2 && (flags & 8) != 0) { StopRigidBodyCollisions_253CA0(ctrl); body.Flags2 |= RigidBody.NoTerrain; } // Amedo
        _bump = 0;
    }

    // Amedo
    private void StopRigidBodyCollisions_253CA0(PhysicsController ctrl)
    {
        ctrl.Terrain = null;
        if (ctrl.Second_DE != 0xFF && ChunkA0 is { } c) c.ChunkRigidBodiesOf_1F1CF0().TakeSecond_252610(ctrl); // Amedo
        ctrl.Flags &= ~(0xFUL << 36);
    }

    private void UpdateContactRigid(WobbleState w) // Amedo 2026-09-27
    {
        if (_physics?.Body is not { } body) return;
        var r = w.Args;
        uint flags = r.Count > 23 ? r[23] : 0u;
        if (((flags >> 15) & 1) == 0 && (body.Flags & RigidBody.Asleep) == 0 && (w.Mode == 1 || body.Touching)) // Amedo
        {
            var here = LocalMatrix.Translation;
            var player = World.Player is { } p && p != this ? p.GamePosition : here;
            var away = here - player; // Amedo
            if (w.SettleDistance2 < away.X * away.X + away.Y * away.Y + away.Z * away.Z && 1.5f < (World.NowTicks - TicksOf(w.Start)) * TickTime)
            {
                var v = body.PrevVelocity;
                var s = body.Spin;
                if (v.X * v.X + v.Y * v.Y + v.Z * v.Z < 0.4f && s.X * s.X + s.Y * s.Y + s.Z * s.Z < 0.4f)
                {
                    if (48 < ++w.Settle)
                    {
                        if (((WArg(r, 26) >> 14) & 1) != 0)
                        {
                            if (_springRest_88 && _storedSpace is { } place) { SetLocalPosition(place.Translation); SetLocalRotation(_storedSpaceQuat); } // Amedo
                            else
                            {
                                if (w.HasRestPose) SetLocalPosition(w.RestPose); // Amedo
                                SetLocalRotation(w.RestQuat);
                            }
                        }
                        w.Settle = 0;
                        _springRest_88 = false; // Amedo
                        body.Flags |= RigidBody.Asleep;
                        body.Momentum = Vector3.Zero;
                        body.AngularMomentum = Vector3.Zero;
                        return;
                    }
                }
                else w.Settle = 0;
            }
        }
        _prevVelocity = _velocity;
        _velocity = body.Velocity;
        if (r.Count > 18 && 0f < BitConverter.UInt32BitsToSingle(r[18])) TurnToFocus_239C00(body, BitConverter.UInt32BitsToSingle(r[18])); // Amedo
        if (w.Mode == 1) SpringCorners_2396DC(w, body); // Amedo
        else if (_launched) Projectile_23B3E0(w); // Amedo 2026-09-30
        int axis = (int)((flags >> 4) & 3);
        if (axis != 0) DampAxis(body, axis, WFloat(r, 1));
    }

    // Amedo
    private void TurnToFocus_239C00(RigidBody body, float k)
    {
        Vector3 target;
        if (_hasFocusObject)
        {
            if (FocusObject is not { } focus) return;
            target = focus.GamePosition;
        }
        else if (_hasFocusPosition) target = _focusPosition;
        else return;
        var d = target - body.World.Translation;
        var m = LocalMatrix;
        float fx = m.M31, fy = m.M32, fz = m.M33;
        body.Torque += new Vector3((d.Y * fz - d.Z * fy) * k, (d.Z * fx - d.X * fz) * k, (d.X * fy - d.Y * fx) * k);
    }

    private void DampAxis(RigidBody body, int axis, float amount) // Amedo 2026-09-27
    {
        float keep = 1f - amount * amount * 60f;
        if (keep < 0f) keep = 0f;
        var m = LocalMatrix;
        var a = axis switch
        {
            2 => new Vector3(m.M21, m.M22, m.M23),
            3 => new Vector3(m.M31, m.M32, m.M33),
            _ => new Vector3(m.M11, m.M12, m.M13),
        };
        var mo = body.Momentum; // Amedo
        float along = mo.X * a.X + mo.Y * a.Y + mo.Z * a.Z;
        if (along != 0f) body.Momentum -= a * (1f - keep) * along;
        body.Flags |= RigidBody.MomentumDirty;
    }

    private RigidBody? SteppedBody => _physics?.Body ?? OwnCharacter?.Body5; // Amedo 2026-09-29

    internal bool BodyAwake => SteppedBody is { } b && (b.Flags & RigidBody.Asleep) == 0 && !_destroyed;
    internal RigidBody? PhysicsBody_290170 => SteppedBody; // Amedo

    internal RigidBody? Component5 => _destroyed ? null : SteppedBody; // Amedo 2026-09-30

    // Amedo
    private void SetBodyMaskFlags_10D2C8(uint fields)
    {
        if (Component5 is not { } body) return;
        static uint Field(uint mask, uint mode, uint bit) => (mode & 3) switch { 1 => mask | bit, 2 => mask & ~bit, _ => mask };
        uint m = body.CollisionMask;
        m = Field(m, fields, 0x2000);
        m = Field(m, fields >> 2, 0x8000);
        m = Field(m, fields >> 4, 0x10000);
        m = Field(m, fields >> 6, 0x1000);
        body.CollisionMask = m | 0x30;
    }

    // Amedo 2026-09-30
    internal void Touched_22FC98(float strength, object sender) // Amedo 2026-10-06
    {
        if (_contactResponse is { } args)
        {
            if (_wobble is null || !ReferenceEquals(_wobble.Args, args))
            {
                if (((WArg(args, 27) >> 14) & 1) != 0) SetWobble(args);
            }
            else if (_physics?.Body is not null) Wake_291308();
            ContactReply_23B5A0(args, sender);
        }
        if (!(0f < strength) || _physics is null) return; // Amedo 2026-10-05
        var from = sender switch { AgentLabAgent a => a.GamePosition, DynamicSceneryInstance scenery => scenery.GameMatrix.Translation, _ => GamePosition }; // Amedo 2026-10-06
        var d = GamePosition - from;
        float s = strength * 4f / (d.X * d.X + d.Y * d.Y + d.Z * d.Z + 4f);
        d *= CrashEngine.Runtime.Camera.CamMath.InvLengthGuarded_11C030(2.4999998e-09f, new Vector4(d, 0f));
        Knock_24BC60(d * s, from);
    }

    // Amedo 2026-10-06
    private void ApplyImpulse_244FC8(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        uint w = Arg(0);
        int sel = (int)(w & 0xFF);
        var target = sel != 0xFF ? (sel >= 0xDF ? CodeObject(sel) : null) : (w & 0x400) != 0 ? Requester : this;
        if (target is null || target._destroyed) return;
        if ((w & 0x1000) != 0)
        {
            var impulse = Vector3.TransformNormal(new Vector3(F(1), F(2), F(3)), LocalMatrix);
            target.ActivateResponse_23D648();
            if (target._physics is not null) target.Knock_24BC60(impulse, target.GamePosition);
            else target.Component5?.ApplyImpulse_289CC0(impulse, Vector3.Zero);
            Trace($"ApplyImpulse {impulse} -> {target.Entity.Name}");
        }
        else if ((w & 0x800) != 0 && target.Component5 is { } body)
        {
            body.AngularMomentum += new Vector3(F(5), F(6), F(7));
            body.Flags |= RigidBody.MomentumDirty;
        }
    }

    // Amedo 2026-10-06
    private void PushStandingOn_123668(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        if (OwnCharacter?.StandingOn is not AgentLabAgent target || target._destroyed || target._physics is null) return;
        target.ActivateResponse_23D648();
        var impulse = Vector3.TransformNormal(new Vector3(F(1), F(2), F(3)), LocalMatrix);
        target.Knock_24BC60(impulse, target._sphereCenter);
        Trace($"push standing-on {target.Entity.Name} {impulse}");
    }

    // Amedo 2026-10-06
    private void ActivateResponse_23D648()
    {
        if (_contactResponse is not { } args || (_wobble is not null && ReferenceEquals(_wobble.Args, args))) return;
        SetWobble(args);
    }

    // Amedo 2026-10-05
    private void Knock_24BC60(Vector3 impulse, Vector3 from)
    {
        if (_physics is not { } ctrl) return;
        switch (ctrl.PairType)
        {
            case >= 1 and <= 5:
            case 8:
                _velocity += impulse;
                break;
            case >= 9 and <= 11:
                if (ctrl.Body is { } body && Matrix4x4.Invert(LocalMatrix, out var inv)) body.ApplyImpulse_289CC0(impulse, Vector3.Transform(from, inv));
                break;
        }
    }

    // Amedo 2026-10-05
    internal void LaunchPhysics_22ECB0(Vector3 v, float gravity)
    {
        float g = 0f <= gravity ? gravity : 30f;
        if (_physics is not { } ctrl)
        {
            ctrl = new PhysicsController();
            _physics = ctrl;
            SetRigidBodyGravity_253988(ctrl, g); // Amedo
            SetPairType_246480(ctrl, 1); // Amedo
            SetTerrainType_2467E0(ctrl, 1); // Amedo
            if (Ogi is { } hullOgi && OgiHulls.Of(hullOgi) is { Count: > 0 } launchHulls && launchHulls[0].Points is { Length: > 0 } pts) // Amedo
            {
                var min = pts[0];
                var max = min;
                foreach (var p in pts)
                {
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
                var ext = max - min;
                float r = ext.X < ext.Y ? ext.Y : ext.X;
                _logicalRadius = r < ext.Z ? ext.Z : r;
            }
        }
        else SetRigidBodyGravity_253988(ctrl, g); // Amedo
        ctrl.Flags = (ctrl.Flags & ~PhysicsController.VerticalGravity) | PhysicsController.FullGravity;
        _prevVelocity = _velocity;
        _velocity = v;
    }

    // Amedo 2026-09-30
    private void ContactReply_23B5A0(List<uint> args, object sender) // Amedo 2026-10-06
    {
        uint flags = WArg(args, 27);
        uint senderFlags = sender switch { AgentLabAgent a => a.AgentFlags, DynamicSceneryInstance d => d.AgentFlags, _ => 0u }; // Amedo 2026-10-06
        if (((flags >> 13) & 1) != 0 && (senderFlags & 0x10) != 0) // Amedo
        {
            if (sender is AgentLabAgent toucher) { if (SticksToMotionBlock_2362B8(args, toucher)) StickToMotionBlock_236120(args, toucher); }
            else World.Unimplemented("physics", 0x236120, "dynamic scenery sticks to a motion block (0x236120)");
        }
        if (((flags >> 16) & 1) != 0 && sender is AgentLabAgent receiver) receiver.Deliver((ushort)(WArg(args, 30) & 0xFFFF), this); // Amedo 2026-10-06
    }

    // Amedo
    private void ActivatePhysics_2461C0()
    {
        if (_physics is not { } ctrl || (ctrl.State & PhysicsController.StateDeactivated) == 0) return;
        var lists = ChunkA0?.ChunkRigidBodiesOf_1F1CF0(); // Amedo
        ctrl.State &= ~PhysicsController.StateDeactivated;
        if (!(ctrl.PairType < 9))
        {
            if (ctrl.Body is null) RigidTypes(ctrl, ctrl.PairType); // Amedo
        }
        else
        {
            lists?.PutFirst_252430(ctrl); // Amedo
            ctrl.Flags |= PhysicsController.InPairList;
        }
        if (ctrl.Second_DE != 0xFF) lists?.PutSecond_2525B8(ctrl); // Amedo
    }

    // Amedo 2026-09-30
    internal void Wake_291308()
    {
        if (SteppedBody is not { } body || (body.Flags & RigidBody.Asleep) == 0) return;
        body.Flags &= ~RigidBody.Asleep;
        if ((AgentFlags & 0x8000) != 0 && _wobble is { } w)
        {
            w.Settle = 0;
            w.Start = World.Now;
            w.RestPose = LocalMatrix.Translation;
            w.RestQuat = LocalRotation;
        }
        body.UpdateDerived();
        body.Force = Vector3.Zero;
        body.Torque = Vector3.Zero;
    }

    internal void BodySubsteps(CollisionWorld world, float dt) // Amedo 2026-09-27
    {
        var body = SteppedBody!;
        if (OwnCharacter is { } rider && ReferenceEquals(body, rider.Body5)) world = rider.CollisionWorld; // Amedo 2026-09-29
        if (body.OnLanded is null) BindBodyContacts_2919E8(body); // Amedo
        body.Flags2 &= ~(RigidBody.HardImpact | 0x30UL);
        body.Flags &= ~(RigidBody.MomentumDirty | RigidBody.TouchA | RigidBody.TouchB);
        body.ImpulseSum = 0f;
        float h = dt / body.Substeps;
        body.Correction = Vector3.Zero;
        body.CorrectionSum = 0f;
        body.InstanceCorrection = Vector3.Zero; // Amedo 2026-09-29
        for (int i = 0; i < body.Substeps; i++)
        {
            body.Substep(h);
            InstanceContacts(body, world); // Amedo 2026-09-29
            if (body.TerrainContacts(world, (tri, pos) => TouchWater(world, tri, pos), out bool wet) && !wet) _wetSurface = -1;
            body.PrevVelocity = body.Velocity;
        }
    }

    internal void BodyFinish(float dt) // Amedo 2026-09-27
    {
        var body = SteppedBody!;
        body.Integrate(dt, !body.IsBox); // Amedo
        body.UpdateDerived();
        body.Force = Vector3.Zero;
        body.Torque = Vector3.Zero;
        if ((body.Flags2 & RigidBody.SyncTransform) != 0)
        {
            var m = LocalMatrix;
            body.WriteBack(ref m);
            GameMatrix = m; // Amedo
        }
        body.IdleFrames = (body.Flags2 & 0x30) != 0 ? (byte)0 : (byte)Math.Min(255, body.IdleFrames + 1);
    }

    private void RaycastFocusPosition(List<uint> a) // Amedo 2026-09-27
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        int code = (int)(Arg(9) & 0xFF);
        var frame = this;
        Vector3 origin;
        if (CodeObject(code) is { } obj) { origin = obj.GamePosition; frame = obj; }
        else if (!CodePosition(code, out origin)) origin = GamePosition;
        var dir = new Vector3(F(1), F(2), F(3));
        if ((int)Arg(7) is 2 or 3) dir = Vector3.TransformNormal(dir, frame.LocalMatrix);
        if (Chunk.Collision is not { } world) return; // Amedo 2026-09-28
        if (MathF.Abs(dir.X) <= 5e-5f && MathF.Abs(dir.Y) <= 5e-5f && MathF.Abs(dir.Z) <= 5e-5f) return;
        if (!world.SegmentCast(origin, origin + dir, 0x40, out var hit)) { Trace($"RaycastFocusPosition from {origin} dir {dir}: no hit"); return; } // Amedo 2026-10-05
        var target = hit.Point;
        Trace($"RaycastFocusPosition from {origin} dir {dir} -> {target}");
        float back = F(5);
        if (back != 0f)
        {
            float len = target.Length();
            target *= (len - back) / len;
        }
        FocusAt(target);
    }

    // Amedo 2026-10-05
    private bool SceneRay_280958(Vector3 start, ref Vector3 ray, uint instanceMask, bool instances)
    {
        static bool Tiny(Vector3 v) => MathF.Abs(v.X) <= 5e-5f && MathF.Abs(v.Y) <= 5e-5f && MathF.Abs(v.Z) <= 5e-5f;
        if (Tiny(ray)) { ray = Vector3.Zero; return false; }
        bool hit = false;
        if (Chunk.Collision is { } world && world.SegmentCast(start, start + ray, 0x40, out var h))
        {
            hit = true;
            ray = h.Point - start;
        }
        if (Tiny(ray)) { ray = Vector3.Zero; return hit; }
        if (!instances) return hit;
        var end = start + ray;
        float t = HullQueries.SegmentBodies_1F1740(World.QueryBodies(Vector3.Min(start, end), Vector3.Max(start, end), instanceMask, 1, 0, null, requireAll: true), start, end);
        if (0f <= t && t <= 1f)
        {
            hit = true;
            ray *= t;
        }
        return hit;
    }

    private bool LineOfSight_280958(Vector3 from, ref Vector3 move, uint sceneMask, uint objectMask) =>
        LineOfSight_280958(from, ref move, sceneMask, objectMask, null, out _); // Amedo

    // Amedo
    private bool LineOfSight_280958(Vector3 from, ref Vector3 move, uint sceneMask, uint objectMask, AgentLabAgent? parent, out AgentLabAgent? other)
    {
        other = null;
        if (Still(move))
        {
            move = Vector3.Zero;
            return false;
        }
        bool hit = false;
        if (Chunk.Collision is { } world && world.SegmentCast(from, from + move, sceneMask, out var h))
        {
            move = h.Point - from;
            hit = true;
        }
        if (Still(move))
        {
            move = Vector3.Zero;
            return hit;
        }
        float best = 1e30f;
        var found = World.SegmentAgents_1E9168(from, from + move, objectMask, this, parent, ref best); // Amedo
        if (found is not null && 0f <= best && best <= 1f)
        {
            move *= best;
            other = found; // Amedo
            hit = true;
        }
        return hit;
    }

    private static bool Still(Vector3 v) => MathF.Abs(v.X) <= 5e-05f && MathF.Abs(v.Y) <= 5e-05f && MathF.Abs(v.Z) <= 5e-05f;

    // Amedo 2026-10-05
    private float LedgeDrop_2405F0(int parameter)
    {
        var m = LocalMatrix;
        var start = m.Translation + new Vector3(m.M31, m.M32, m.M33) * (parameter == 1 ? -5f : 5f) + new Vector3(0f, 8f, 0f);
        var ray = new Vector3(0f, -16f, 0f);
        return SceneRay_280958(start, ref ray, 0x252000, true) ? MathF.Abs(ray.Y + 8f) : 10000f;
    }

    private static float InverseLength(Vector3 v)
    {
        float lenSq = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
        return lenSq > 2.4999998e-09f ? 1f / MathF.Sqrt(lenSq) : 0f;
    }

    private Vector3 ClampToRadius(Vector3 move) =>
        _physics is null || (_physics.Flags & (0xFFUL << 32)) == 0 ? move : HoldRigidBodyMove_2540E8(move); // Amedo

    // Amedo
    private Vector3 HoldRigidBodyMove_2540E8(Vector3 move)
    {
        float len = MathF.Sqrt(move.X * move.X + move.Y * move.Y + move.Z * move.Z);
        if (!(_logicalRadius < len)) return move;
        float s = _logicalRadius / len;
        return new Vector3(move.X * s, move.Y * s, move.Z * s);
    }

    private float TouchingTerrain(TwinBehaviourCondition c)
    {
        if (_physics is not { } ctrl) return 0f;
        if (ctrl.Body is { } body) return body.Touching ? 1f : 0f;
        if (c.Parameter != 0) return 0f;
        return (ctrl.Flags & (PhysicsController.TouchedTerrain | PhysicsController.TouchedAgent)) != 0 ? 1f : 0f;
    }

    // Amedo 2026-09-30
    private float OnGround_228F58()
    {
        if (_physics is not { } ctrl) return 0f;
        if (ctrl.Body is { } body) return (body.Flags & RigidBody.TouchA) != 0 ? 1f : 0f;
        return (ctrl.Flags & PhysicsController.TouchedTerrain) != 0 ? 1f : 0f;
    }
}
