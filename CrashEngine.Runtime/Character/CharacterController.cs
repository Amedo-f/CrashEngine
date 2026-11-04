using System.Numerics;
using CrashEngine.Runtime.Physics;
using CrashEngine.Runtime.Character.Vehicles; // Amedo
using CrashEngine.Core;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-09-27
public sealed partial class CharacterController
{
    public const int GravityOn = 0x2;
    public const int Grounded  = 0x4;
    public const int Moving    = 0x10;
    public const int Falling   = 0x20;

    public const uint TurnOverride     = 1u << 16;
    public const uint ForwardOverride  = 1u << 17;
    public const uint SideOverride     = 1u << 18;
    public const uint VerticalOverride = 1u << 19;
    public const uint AccelOverride    = 1u << 20;

    private const float Far = 1e30f;
    private const float AngleToRadians = 9.58738e-5f;

    private CollisionWorld _world; // Amedo 2026-09-28

    // Amedo 2026-09-28
    public CollisionWorld CollisionWorld
    {
        get => _world;
        set => _world = value;
    }
    private readonly Func<Matrix4x4> _getMatrix;
    private readonly Action<Matrix4x4> _setMatrix;
    private readonly Action<int> _trigger;

    public readonly CharacterCollision Collision = new();
    public readonly TriangleCache Triangles2B0 = new(0x10); // Amedo
    private readonly CharacterCollision _probe = new();
    public ConvexHull StandHull { get; private set; } // Amedo 2026-10-03
    public ConvexHull LowHull { get; private set; }

    // Amedo 2026-10-03
    public static (float Height, float Radius, float LowHeight, float LowRadius) BoxSize_13C758(int kind) => kind switch
    {
        2 => (2f, 0.29f, 0.75f, 0.29f),
        4 => (1f, 0.5f, 1f, 0.5f),
        5 => (9f, 1.6f, 5.5f, 1.6f),
        _ => (1.8f, 0.29f, 0.75f, 0.29f),
    };

    public void SetKindBoxes_13C758(int kind)
    {
        var (h, r, lh, lr) = BoxSize_13C758(kind);
        if (CharacterLimbs.ForKind_13C758(kind) is { } parts) Limbs.Parts = parts; // Amedo
        StandHull = Box(h, r);
        LowHull = Box(lh, lr);
        StandHeight_C0 = h; // Amedo
        LowHeight_C0 = lh;
    }

    public float StandHeight_C0 { get; private set; } = 1.8f; // Amedo
    public float LowHeight_C0 { get; private set; } = 0.75f;
    public CharacterLocomotion Locomotion;
    public CharacterJump Jump;
    public CharacterCrouch Crouch;
    public CharacterSpin Spin;
    public CharacterGun? Gun; // Amedo
    public CharacterClaw? Claw; // Amedo
    public Func<bool>? IsActivePlayer; // Amedo
    public Action? GunHolder; // Amedo
    public Func<uint>? Progression; // Amedo
    public readonly CharacterLimbs Limbs = new(); // Amedo 2026-09-27
    public readonly CharacterBones Bones; // Amedo 2026-09-28

    private readonly float[] _floats;
    private readonly IReadOnlyList<uint> _ints;
    public uint JumpRules { get; }

    public float Gravity;
    public float FallSpeed;
    public float WalkSpeed;
    public float RunSpeed;

    public Vector3 Velocity;
    public ulong State70 = 2; // Amedo
    public Vector3 FallStart;
    public float BaseVertical;
    public Vector3 PrevCenter_280; // Amedo
    private bool _groundedAtFrame; // Amedo
    public CollisionWorld.Surface? LastSurface;
    public CollisionWorld.Triangle GroundTriangle;
    public Vector3 GroundNormal;
    public int Mode270;
    public float Timer274;
    public float CenterOffset;
    public float Offset27C;
    public Vector3 GroundPoint; // Amedo 2026-09-28
    public Func<Vector3, Vector3, float>? GroundObjects;

    public int MotionWord = 3;
    public int MotionFlags = GravityOn;
    public uint MotionFlagsHigh;
    public int TurnRate;
    public float Forward, Side, Vertical, AccelScale;
    public float Efficiency = 1f;

    public Vector3 Move;
    public float MoveTurn, MoveZ, MoveX;
    public float Cross, Square, Circle, RightMinusLeft;
    public uint InputDisabled;

    public Func<Vector3, Vector3, uint, IReadOnlyList<CharacterBody>>? Bodies; // Amedo 2026-09-27
    public Func<object?>? SolveSkip_139964;
    public Func<object, object, bool>? LinkedTo; // Amedo 2026-10-05
    public float HullRadius = 0.29f;
    public Action<object, int>? Touch;
    public Func<object, uint>? StateFlagsOf;
    public Func<object, int>? MotionStateOf; // Amedo
    public Func<Vector3, Vector3, IReadOnlyList<CharacterBody>>? LimbBodies;
    public Func<Vector3?>? StrafeTarget_124; // Amedo
    public object? StandingOn;
    public int StandingPart = -1;
    // Amedo 2026-09-29
    public object? SupportModel;
    public Vector3 SupportLocal;
    public Matrix4x4 SupportInverse = Matrix4x4.Identity;
    public float PlatformTurn;
    public Func<object, int, Matrix4x4>? PartMatrix;
    public Func<object, object?>? ModelOf;
    public Func<object, int, int>? HullSurfaceOf; // Amedo
    public Func<object, uint>? InstanceFlagsOf;

    public int Frames { get; private set; }
    public int LastHit;
    public Vector3 LastMove;
    public Vector3 LastPlatform; // Amedo 2026-10-01
    public bool Enabled { get; set; } = true;

    public CharacterController(CollisionWorld world, Func<Matrix4x4> getMatrix, Action<Matrix4x4> setMatrix, Action<int> trigger,
                               float[] floats, IReadOnlyList<uint> ints, uint jumpRules = 0x44)
    {
        _world     = world;
        _getMatrix = getMatrix;
        _setMatrix = setMatrix;
        _trigger   = trigger;
        _floats    = floats;
        _ints      = ints;
        JumpRules  = jumpRules;
        float F(int i, float fallback) => i < floats.Length ? floats[i] : fallback;
        Gravity   = F(1, 50f);
        FallSpeed = F(3, 15f);
        WalkSpeed = F(6, 2.5f);
        RunSpeed  = F(7, 9f);
        StandHull = Box(1.8f, 0.29f);
        LowHull   = Box(0.75f, 0.29f);
        NewComponents(); // Amedo 2026-09-29
        Bones  = new CharacterBones(this); // Amedo 2026-09-28
    }

    // Amedo 2026-09-29
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(Locomotion), nameof(Crouch), nameof(Jump), nameof(Spin))]
    private void NewComponents()
    {
        Locomotion = new CharacterLocomotion(this)
        {
            TurnRates = [_ints.Count > 0 ? (int)_ints[0] : 0x10000, _ints.Count > 1 ? (int)_ints[1] : 0x20000, _ints.Count > 2 ? (int)_ints[2] : 0x20000],
        };
        Crouch = new CharacterCrouch(this);
        Jump   = new CharacterJump(this);
        Spin   = new CharacterSpin(this);
    }

    // Amedo 2026-09-29
    public Action? SetVideoModel_29E988; // Amedo

    private void CreateComponents_134F18()
    {
        LinkTicks = 0; // Amedo 2026-10-05
        Link = null;
        State70 &= ~0xC000UL;
        SetVideoModel_29E988?.Invoke(); // Amedo
        NewComponents();
        KindSetUp_134F18(); // Amedo
    }

    // Amedo
    public int Kind => Bones.Kind < 0 ? 0 : Bones.Kind;
    public bool HasCrouch => Kind is 0 or 1 or 2;
    public bool HasJump => Kind is >= 0 and <= 5 and not 4;
    public bool HasSpin => Kind is 0 or 2 or 3 or 5;
    public bool HasAir => Kind is >= 0 and <= 5 and not 4;

    // Amedo
    internal void KindSetUp_134F18()
    {
        Gun = Kind is 1 or 5 ? new CharacterGun(this) : null;
        Claw = Kind == 3 ? new CharacterClaw(this) : null;
    }

    public float Param(int i) => i < _floats.Length ? _floats[i] : 0f;
    public int IntParam(int i) => i < _ints.Count ? (int)_ints[i] : 0;

    // Amedo
    internal bool Controlled_13FD18() => IntParam(0) == 4 || AutomaticInput;
    public void SetParam(int i, float value) { if (i < _floats.Length) _floats[i] = value; }

    public bool Carrying => (MotionFlagsHigh & ((1u << 21) | (1u << 22))) != 0;

    public Func<int>? Clock; // Amedo
    private int _ownTicks; // Amedo
    public int Ticks => Clock?.Invoke() ?? _ownTicks; // Amedo

    public Vector3 ForwardAxis
    {
        get
        {
            var m = _getMatrix();
            return new Vector3(m.M31, m.M32, m.M33);
        }
    }

    public static readonly ConvexHull SpinHull = BoxRange(0.99f, 0.05f, 0.9f); // Amedo 2026-09-27
    public static readonly ConvexHull CarrySpinHull = BoxRange(1.5f, 0.5f, 1.4f);

    public Func<ConvexHull, Matrix4x4, uint, IReadOnlyList<object>>? OverlapQuery; // Amedo 2026-09-27

    // Amedo 2026-09-27
    public static ConvexHull BoxRange(float r, float y0, float y1) => new()
    {
        Points =
        [
            new(-r, y0, -r), new(r, y0, -r), new(r, y0, r), new(-r, y0, r),
            new(-r, y1, -r), new(r, y1, -r), new(r, y1, r), new(-r, y1, r),
        ],
        Faces =
        [
            new(0f, -1f, 0f, y0), new(0f, 0f, -1f, -r), new(1f, 0f, 0f, -r),
            new(0f, 0f, 1f, -r), new(-1f, 0f, 0f, -r), new(0f, 1f, 0f, -y1),
        ],
        EdgeDirs = [new(-1f, 0f, 0f), new(0f, 0f, -1f), new(0f, -1f, 0f)],
        Normals  = [new(0f, -1f, 0f), new(0f, 0f, -1f), new(1f, 0f, 0f)],
    };

    // Amedo 2026-10-06
    private static readonly ConvexHull SlamHull = ConvexHull.Box(new Vector3(-0.4f, -0.65f, -0.4f), new Vector3(0.4f, 0f, 0.8f));
    private readonly List<object> _slamHits = new();

    // Amedo 2026-10-06
    private void SlamAttack_130B80()
    {
        if ((MotionFlagsHigh & (1u << 22)) == 0 || Link is not { } link || !link.SlamWindow_15FC00() || OverlapQuery is null || Touch is null) return;
        if (Limbs.ExitMatrix(0, _getMatrix()) is not { } m) return;
        _slamHits.Clear();
        _slamHits.AddRange(OverlapQuery(SlamHull, m, 0x5F000));
        foreach (var owner in _slamHits) SendTouch(owner, 0f);
    }

    // Amedo 2026-09-27
    private void SpinAttack()
    {
        if (MotionState is not (6 or 10 or 13) || OverlapQuery is null || Touch is null) return;
        var m = _getMatrix();
        var hits = new List<object>();
        if ((MotionFlagsHigh & (1u << 21)) == 0)
        {
            if ((MotionFlagsHigh & (1u << 22)) != 0) return;
            hits.AddRange(OverlapQuery(SpinHull, m, 0x15F000));
            hits.AddRange(OverlapQuery(StandHull, m, 0x15F000));
        }
        else hits.AddRange(OverlapQuery(CarrySpinHull, m, 0x15F000));
        foreach (var owner in hits) SendTouch(owner, 0f);
    }

    public Func<Vector3, float, uint, IReadOnlyList<object>>? SphereQuery; // Amedo 2026-09-27
    public Func<(Vector3 Min, Vector3 Max)>? InstanceBox;
    public Action<object>? Collect;
    public bool ActivePlayer { get; set; } = true;

    // Amedo 2026-09-27
    private void Pickups()
    {
        if (SphereQuery is null || InstanceBox is null) return;
        var (min, max) = InstanceBox();
        var center = (min + max) * 0.5f;
        if (MotionState is not (6 or 10) && Touch is not null)
            foreach (var owner in SphereQuery(center, 1f, 0x4000)) SendTouch(owner, 0f);
        if (ActivePlayer && Collect is not null)
            foreach (var owner in SphereQuery(center, 1.5f, 0x4000)) Collect(owner);
    }

    public static ConvexHull Box(float height, float radius) => new()
    {
        Points =
        [
            new(-radius, 0f, -radius), new(radius, 0f, -radius), new(radius, 0f, radius), new(-radius, 0f, radius),
            new(-radius, height, -radius), new(radius, height, -radius), new(radius, height, radius), new(-radius, height, radius),
        ],
        Faces =
        [
            new(0f, -1f, 0f, 0f), new(0f, 0f, -1f, -radius), new(1f, 0f, 0f, -radius),
            new(0f, 0f, 1f, -radius), new(-1f, 0f, 0f, -radius), new(0f, 1f, 0f, -height),
        ],
        EdgeDirs = [new(-1f, 0f, 0f), new(0f, 0f, -1f), new(0f, -1f, 0f)],
        Normals  = [new(0f, -1f, 0f), new(0f, 0f, -1f), new(1f, 0f, 0f)],
    };

    public int MotionState
    {
        get => MotionWord & 0xFF;
        set => MotionWord = (MotionWord & ~0xFF) | (value & 0xFF);
    }

    public bool IsGrounded => (MotionFlags & Grounded) != 0;
    public bool InAirState => MotionState is 7 or 11;
    public bool OnEdge => (MotionFlagsHigh & (1u << 23)) != 0; // Amedo 2026-09-27

    // Amedo 2026-09-27
    public int HitPoints
    {
        get => (MotionFlags >> 6) & 0xFF;
        set => MotionFlags = (MotionFlags & ~0x3FC0) | ((value & 0xFF) << 6);
    }

    // Amedo
    public AgentLab.WorldChunk? HomeChunk_90 { get; set; }

    public int Phase => Locomotion.Phase;

    private Vector3 Position => _getMatrix().Translation;
    public Matrix4x4 GameMatrix => _getMatrix(); // Amedo 2026-09-28

    private void SetPosition(Vector3 p)
    {
        var m = _getMatrix();
        m.Translation = p;
        _setMatrix(m);
    }

    public void Step(float dt)
    {
        if (!Enabled || dt <= 0f) return;
        Frames++;
        int frameTicks = (int)MathF.Round(dt * 9000f);
        _ownTicks += frameTicks; // Amedo
        dt = frameTicks * (1f / 9000f);
        ApplyInput(dt); // Amedo 2026-09-29
        if ((State70 & 0xF) == 2 && Kind != 4) LiftOntoGround_136890(2f, 20f); // Amedo
        if (0 < CrushCounter_CC) CrushCounter_CC--; // Amedo
        RefreshCache_136A78(); // Amedo
        _groundedAtFrame = IsGrounded; // Amedo
        if (Kind == 5) HullHits?.Invoke(); // Amedo
        if (Frames == 1) PrevCenter_280 = Position + new Vector3(0f, CenterOffset, 0f); // Amedo
        if (Dead) // Amedo
        {
            DeadFrame_137B40(dt, frameTicks);
            return;
        }
        if ((State70 & 0x8000UL) != 0) // Amedo
        {
            MakeBoxOfExitPoints_1395A0(); // Amedo
            CheckTriggers_131320?.Invoke();
            InvincibilityTimer();
            return;
        }
        if (MotionPaused?.Invoke() == true) // Amedo
        {
            Bones.Joints_14B6D0(dt);
            InvincibilityTimer();
            CheckTriggers_131320?.Invoke();
            return;
        }
        MotionFlagsHigh &= ~TurnOverride;
        ClawFrame_137CC0(); // Amedo
        GunFrame_137D40();
        if (Vehicle is null && HasAir) // Amedo 2026-09-29
        {
            Locomotion.Update(Ticks, frameTicks);
            MotionFlagsHigh = (MotionFlagsHigh & ~((1u << 10) | (1u << 11) | (1u << 12)))
                            | (Locomotion.Phase == 4 ? 1u << 10 : 0u) | (Locomotion.Phase == 5 ? 1u << 11 : 0u)
                            | (Locomotion.RightMinusLeftHeld ? 1u << 12 : 0u);
        }
        else MotionFlagsHigh &= ~((1u << 10) | (1u << 11) | (1u << 12));
        if (Vehicle is { } vehicle) // Amedo 2026-09-29
        {
            Gravity = Param(1);
            MotionFlagsHigh &= ~((1u << 0) | (1u << 1) | (1u << 2) | (1u << 6) | (1u << 7) | (1u << 8) | (1u << 9));
            vehicle.Step(dt);
        }
        else
        {
            JumpIntent(frameTicks);
            CrouchIntent(frameTicks);
            SpinIntent();
            Integrate(dt);
        }
        Bones.Joints_14B6D0(dt); // Amedo
        Bones.LookFrame_130958(dt); // Amedo
        SlamAttack_130B80(); // Amedo 2026-10-06
        SpinAttack(); // Amedo 2026-09-27
        Pickups();
        Splash_1310D0(); // Amedo
        InvincibilityTimer(); // Amedo 2026-09-28
        if (Vehicle is null) // Amedo 2026-09-29
        {
            FallAndLand();
            LinkStep_12EA78(frameTicks); // Amedo 2026-10-05
        }
        GroundPoint_134DB8(); // Amedo 2026-09-28
        MoveTurn = MoveZ = MoveX = 0f;
        Limbs.Accumulated = Vector3.Zero; // Amedo 2026-09-27
        MotionFlagsHigh &= ~(ForwardOverride | SideOverride | VerticalOverride | AccelOverride);
        var v = Velocity; // Amedo
        if (3600f < v.X * v.X + v.Y * v.Y + v.Z * v.Z) Velocity = v * GameMath.InverseLength(v) * 60f;
        CheckTriggers_131320?.Invoke(); // Amedo
    }

    public Action? CheckTriggers_131320; // Amedo

    // Amedo
    private void DeadFrame_137B40(float dt, int frameTicks)
    {
        if (Vehicle is { } riding) riding.Step(dt);
        else if ((LastDamage.Kind & 0x2000000) == 0) Integrate(dt);
        Bones.Joints_14B6D0(dt);
        Bones.LookRelax_147B18(dt);
        InvincibilityTimer();
        if (Vehicle is null)
        {
            FallAndLand();
            LinkStep_12EA78(frameTicks);
        }
        MoveTurn = MoveZ = MoveX = 0f;
        Limbs.Accumulated = Vector3.Zero;
        MotionFlagsHigh &= ~(ForwardOverride | SideOverride | VerticalOverride | AccelOverride | TurnOverride);
        CheckTriggers_131320?.Invoke();
    }

    // Amedo
    private void RefreshCache_136A78()
    {
        if (_world is null) return;
        var p = GameMatrix.Translation;
        float side = 2f, up = 3f, down = 1f;
        if (Kind == 5)
        {
            side = 12f;
            up = 13f;
            down = 3f;
        }
        Triangles2B0.Refresh(_world, new Vector3(p.X - side, p.Y - down, p.Z - side), new Vector3(p.X + side, p.Y + up, p.Z + side));
    }

    public Func<Vector3, float, float, bool>? ParticleHit; // Amedo

    // Amedo
    internal void SplashPoint_13F4C0(out Vector3 position, out float radius)
    {
        position = Position + new Vector3(0f, CenterOffset + 1f, 0f);
        radius = 0.9f;
    }

    // Amedo
    private void Splash_1310D0()
    {
        Vector3 position;
        float radius;
        if (Vehicle is { } vehicle) vehicle.SplashSphere_160278(out position, out radius);
        else SplashPoint_13F4C0(out position, out radius);
        if (ParticleHit?.Invoke(position, radius, 1f) != true) return;
        Damage(Position, 8, 1, Self);
    }

    // Amedo 2026-09-28
    private void GroundPoint_134DB8()
    {
        var p = Position + new Vector3(0f, CenterOffset, 0f);
        p.Y += 0.25f;
        var ray = new Vector3(0f, -8f, 0f);
        bool hit = _world.SegmentCast(p, p + ray, 0x10, out var h);
        if (hit) ray = h.Point - p;
        bool zero = MathF.Abs(ray.X) <= 5e-5f && MathF.Abs(ray.Y) <= 5e-5f && MathF.Abs(ray.Z) <= 5e-5f;
        if (zero) ray = Vector3.Zero;
        else if (GroundObjects is { } objects)
        {
            float t = objects(p, p + ray);
            if (0f <= t && t <= 1f) { ray *= t; hit = true; }
        }
        GroundPoint = new Vector3(p.X + ray.X, p.Y + ray.Y + 0.02f, p.Z + ray.Z);
        State70 = (State70 & ~0x1000UL) | (hit ? 0x1000UL : 0UL);
    }

    public bool SetOverride(uint bit, ref float field, float value)
    {
        if ((MotionFlagsHigh & bit) != 0) return false;
        MotionFlagsHigh |= bit;
        field = value;
        return true;
    }

    public bool SetTurn(int value)
    {
        if ((MotionFlagsHigh & TurnOverride) != 0) return false;
        MotionFlagsHigh |= TurnOverride;
        TurnRate = value;
        return true;
    }

    private void SetHigh(int bit, bool on) => MotionFlagsHigh = (MotionFlagsHigh & ~(1u << bit)) | (on ? 1u << bit : 0u);

    // Amedo
    private void ClawFrame_137CC0()
    {
        if (Claw is not { } claw)
        {
            SetHigh(4, false);
            return;
        }
        claw.Frame_146860(Circle, Cross);
        SetHigh(4, claw.State is not (0 or 3 or 13));
    }

    // Amedo
    private void GunFrame_137D40()
    {
        if (Gun is not { } gun)
        {
            SetHigh(5, false);
            return;
        }
        bool blocked = HasJump && Jump.Phase is >= 12 and <= 14;
        float trigger = blocked ? 0f : Square;
        bool strafe = HasAir && Locomotion.RightMinusLeftHeld;
        gun.Frame_14A190(trigger, blocked || strafe ? 0f : 1f);
        SetHigh(5, gun.State != 0);
    }

    private void JumpIntent(int frameTicks)
    {
        if (!HasJump) // Amedo
        {
            Gravity = Param(1);
            SetHigh(1, false);
            SetHigh(2, false);
            SetHigh(8, false);
            SetHigh(7, false);
            return;
        }
        int state = MotionState;
        Jump.Update(Cross, Circle, Ticks, frameTicks, !HasCrouch || Crouch.JumpAllowed); // Amedo 2026-10-30
        int p = Jump.Phase;
        int s = p is 6 or 7 ? (state == 3 ? 7 : state) : (state == 7 ? 3 : state);
        if ((Jump.Flags & 0x1000000) != 0) { if (s == 7) s = 11; }
        else if (s == 11) s = 7;
        SetHigh(1, p != 0);
        SetHigh(2, p is 3 or 6 or 7 or 0x11);
        SetHigh(3, p is 4 or 0x12);
        SetHigh(8, p is 10 or 11);
        MotionState = s;
        SetHigh(7, p is 8 or 9);
    }

    private void CrouchIntent(int frameTicks)
    {
        if (!HasCrouch || Carrying) // Amedo
        {
            SetHigh(0, false);
            SetHigh(9, false);
            return;
        }
        int state = MotionState;
        float len = MathF.Sqrt(MoveZ * MoveZ + MoveX * MoveX);
        Crouch.Update(Circle, len, MoveTurn, Ticks, frameTicks);
        int phase = Crouch.Phase;
        int s = phase is 5 or 6 ? (state == 3 ? 8 : state) : (state == 8 ? 3 : state);
        if (Crouch.Sloped) { if (s == 8) s = 12; }
        else if (s == 12) s = 8;
        SetHigh(0, phase != 0);
        MotionState = s;
        SetHigh(9, phase == 3);
    }

    private void SpinIntent()
    {
        if (!HasSpin) // Amedo
        {
            SetHigh(6, false);
            return;
        }
        int state = MotionState;
        Spin.Update(Gun is not null ? Circle : Square, MathF.Sqrt(MoveZ * MoveZ + MoveX * MoveX), Ticks); // Amedo
        int p = Spin.Phase;
        MotionState = p is 1 or 2 ? (state == 3 ? 6 : state) : (state == 6 ? 3 : state);
    }

    public bool Fits(int shape, bool push = false, bool touchUp = false) => FitsAt_138180(shape, Position, push, touchUp); // Amedo 2026-10-06

    private readonly List<CharacterBody> _fitBodies = new(); // Amedo 2026-10-05
    private readonly List<object> _fitTouched = new(); // Amedo 2026-10-06

    // Amedo 2026-10-05
    public bool FitsAt_138180(int shape, Vector3 point, bool push = false, bool touchUp = false)
    {
        var hull = shape is 3 or 4 or 7 ? LowHull : StandHull;
        var p = point + new Vector3(0f, CenterOffset, 0f);
        var mv = new Vector3(5e-5f, 5e-5f, 5e-5f);
        _probe.Begin();
        _probe.Gather(_world, p, ref mv, hull);
        if (Bodies is { } bodies)
        {
            var (qmin, qmax) = CharacterCollision.LocalBounds(hull);
            qmin += p - new Vector3(0.1f);
            qmax += p + new Vector3(0.1f);
            var partner = Link is { } link ? (IsLinkFollower ? link.Leader_15FE50() : link.Follower_15FE98())?.Self : null;
            _fitBodies.Clear();
            foreach (var b in bodies(qmin, qmax, 0x15B010u))
                if (!ReferenceEquals(b.Owner, partner) && !ReferenceEquals(b.Owner, StandingOn) && !(partner is not null && LinkedTo?.Invoke(b.Owner, partner) == true)) _fitBodies.Add(b);
            _probe.GatherBodies(_fitBodies, qmin, qmax, hull);
        }
        bool touch = touchUp && shape == 0; // Amedo 2026-10-06
        _fitTouched.Clear();
        bool hit = _probe.PointTest_280C60(p, out var dir, touch ? _fitTouched : null);
        if (push && hit) Limbs.Accumulated += dir;
        if (touch && _fitTouched.Count > 0)
            foreach (var owner in _fitTouched.ToArray()) SendTouch(owner, 1f);
        return !hit;
    }

    public void SetAirMode(int mode)
    {
        if (Mode270 == 0)
        {
            if (mode == 1)
            {
                ShiftCenter(-Offset27C);
                Offset27C = 0f;
                Timer274 = 0f;
            }
        }
        else if (Mode270 == 1 && mode == 0) Offset27C = CenterOffset;
        Mode270 = mode;
    }

    private void ShiftCenter(float delta)
    {
        float shifted = CenterOffset + delta;
        float d = CenterOffset - shifted;
        if (5e-5f < MathF.Abs(d)) SetPosition(Position + new Vector3(0f, d, 0f));
        CenterOffset = shifted;
    }

    private void Integrate(float dt)
    {
        var m = _getMatrix();
        var right = new Vector3(m.M11, m.M12, m.M13);
        var forward = new Vector3(m.M31, m.M32, m.M33);
        bool groundedIn = IsGrounded;
        int turn = (MotionFlagsHigh & TurnOverride) != 0 ? (int)(TurnRate * dt) : 0;
        bool slow = true;
        var fwd = Vector3.Zero;
        var side = Vector3.Zero;
        if ((MotionFlagsHigh & ForwardOverride) != 0)
        {
            fwd = forward * Forward;
            slow &= MathF.Abs(Forward) <= 1f;
        }
        if ((MotionFlagsHigh & SideOverride) != 0)
        {
            side = right * Side;
            slow &= MathF.Abs(Side) <= 1f;
        }
        Accelerate(fwd + side, dt);
        if ((MotionFlagsHigh & VerticalOverride) != 0) MeasureVelocity_12E3B8(_groundedAtFrame, Vertical, dt); // Amedo
        PrevCenter_280 = Position + new Vector3(0f, CenterOffset, 0f); // Amedo
        ApplyGravity(dt);
        var disp = Velocity * dt;
        int shape = ShapeClass();
        if (IsLinkFollower && Link is { } link) FollowerPlace_1302F8(link, dt); // Amedo 2026-10-05
        else
        {
            if ((MotionFlagsHigh & LeaderBit) != 0) turn = (int)(turn * 0.65f); // Amedo 2026-10-30
            float turnOut = MoveBody(disp, turn * AngleToRadians, shape, dt, groundedIn);
            if ((MotionFlagsHigh & LeaderBit) == 0 && (MotionFlagsHigh & (1u << 4)) == 0) Rotate(turnOut); // Amedo 2026-10-05
        }
        MotionFlags = (MotionFlags & ~Moving) | (slow ? 0 : Moving);
    }

    // Amedo
    private void MeasureVelocity_12E3B8(bool groundedIn, float vertical, float dt)
    {
        if (groundedIn)
        {
            var d = Position + new Vector3(0f, CenterOffset, 0f) - PrevCenter_280;
            float inv = 1f / dt;
            Velocity.X = d.X * inv;
            Velocity.Z = d.Z * inv;
            BaseVertical = 0f;
        }
        Velocity.Y = BaseVertical + vertical;
    }

    private int ShapeClass() => MotionState switch
    {
        7 or 11 => 10,
        8 or 12 => 11,
        6 or 10 => 12,
        9 => 13,
        13 or 14 => 14,
        _ => 0,
    };

    private void Rotate(float radians)
    {
        int angle = (int)(radians * 10430.378f);
        if (angle == 0) return;
        var m = _getMatrix();
        var pos = m.Translation;
        m.Translation = Vector3.Zero;
        var q = Quaternion.CreateFromRotationMatrix(m);
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle * AngleToRadians);
        m = Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(turn * q));
        m.Translation = pos;
        _setMatrix(m);
    }

    private CollisionWorld.Surface? CurrentSurface()
    {
        switch (State70 & 0xF00)
        {
            case 0x100: return _world.SurfaceAt(GroundTriangle.Surface);
            case 0x200:
            case 0x300: // Amedo
                if (StandingOn is not { } support)
                {
                    State70 &= ~0xF00UL;
                    return null;
                }
                return _world.SurfaceAt(ReferenceEquals(ModelOf?.Invoke(support), SupportModel) ? HullSurfaceOf?.Invoke(support, StandingPart) ?? 0 : 0);
        }
        return IsGrounded ? _world.SurfaceAt(0) : null;
    }

    private void Accelerate(Vector3 target, float dt)
    {
        if (CurrentSurface() is { } surface)
        {
            SetSurface_13F658(surface); // Amedo
            GroundVelocity(dt, surface, target);
            Velocity.Y = -0.01f;
            return;
        }
        float a = LastSurface is { } last ? last.Grip * 40f + 10f : 10f;
        float speed = GameMath.Length(Velocity);
        if (30f < speed)
        {
            float cap = 1500f / speed;
            if (cap < a) a = cap;
        }
        if (!HasAir) a = 0f; // Amedo
        else if (Locomotion.Phase == 9) a = Far;
        else if ((MotionFlagsHigh & AccelOverride) != 0) a *= AccelScale;
        float maxDelta = dt * a;
        var d = new Vector3(target.X - Velocity.X, 0f, target.Z - Velocity.Z);
        if (maxDelta * maxDelta < d.X * d.X + d.Z * d.Z) d = d * GameMath.InverseLength(d) * maxDelta;
        Velocity.X += d.X;
        Velocity.Z += d.Z;
    }

    private void GroundVelocity(float dt, in CollisionWorld.Surface surface, Vector3 target)
    {
        var slide = Vector3.Zero;
        if (0f < surface.Slide && GroundNormal.Y < surface.SlideLimit)
        {
            var h = new Vector3(GroundNormal.X, 0f, GroundNormal.Z);
            float hLen = MathF.Sqrt(GroundNormal.X * GroundNormal.X + GroundNormal.Z * GroundNormal.Z);
            h *= GameMath.InverseLength(h);
            float k = hLen + hLen;
            if (1f < k) k = 1f;
            slide = h * surface.Slide * k;
        }
        Velocity.X += slide.X * dt;
        Velocity.Z += slide.Z * dt;
        var d = target - Velocity + surface.Conveyor;
        float maxDelta = surface.Accel * dt;
        var dh = new Vector3(d.X, 0f, d.Z);
        float len = MathF.Sqrt(dh.X * dh.X + dh.Z * dh.Z);
        if (len != 0f)
        {
            dh *= GameMath.InverseLength(dh);
            dh *= maxDelta < len ? maxDelta : len;
        }
        Velocity.X += dh.X;
        Velocity.Z += dh.Z;
        Velocity.Y = target.Y;
    }

    private void ApplyGravity(float dt)
    {
        if ((MotionFlags & Grounded) != 0 || (MotionFlags & GravityOn) == 0) return;
        Velocity.Y += -1f * (Gravity * dt);
    }

    private float MoveBody(Vector3 disp, float turn, int shape, float dt, bool groundedIn)
    {
        bool slope = MotionState is not (13 or 14) && Mode270 == 1 && 0.1f < Timer274;
        MakeBoxOfExitPoints_1395A0(); // Amedo
        var before = Position;
        var d = disp;
        if (1f < GameMath.Length(d)) d *= GameMath.InverseLength(d);
        float turnOut = Carrying // Amedo 2026-10-05
            ? CollideLinked_13A500(d, turn, dt, groundedIn)
            : Collide(d, turn, Mode270 == 1 ? LowestPoint() : 0f, dt, groundedIn); // Amedo 2026-09-27
        var delta = Position - before;
        if (slope && 0.01f < Velocity.Y && 1e-5f < d.Y && delta.Y / d.Y < 0.9f) Velocity.Y -= dt * 150f;
        if (IsGrounded && !groundedIn)
        {
            if (Mode270 == 1) Offset27C = CenterOffset;
            Mode270 = 0;
        }
        if (Mode270 == 0 && Offset27C != 0f) // Amedo 2026-09-27
        {
            float h = LowestPoint() + CenterOffset;
            float over = Offset27C - h;
            if (0f < over)
            {
                ShiftCenter(-over);
                Offset27C = h;
            }
            if (CenterOffset < 0f)
            {
                ShiftCenter(-CenterOffset);
                Offset27C = 0f;
            }
        }
        Timer274 += dt;
        float dLen = MathF.Sqrt(d.X * d.X + d.Z * d.Z);
        float mLen = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
        if (dLen < 0.01f || delta.Y < -0.05f) Efficiency = 1f;
        else
        {
            float r = mLen / dLen;
            Efficiency = r < 0.705f ? r * 1.41443f : 1f;
        }
        if (Crouch.Phase is 5 or 6 && 0f < Circle)
        {
            float t = LastSurface is { } surface ? 3.5f / surface.Accel : 0f;
            if (0.001f < mLen)
            {
                float down = delta.Y / mLen;
                if (down < 0f) t += down * -4f;
            }
            if (1f < t) t = 1f;
            Crouch.PhaseTime += GameMath.Trunc(t * dt * GameMath.SecToTick);
        }
        return turnOut;
    }

    private float Collide(Vector3 disp, float turn, float stepAdjust, float dt, bool groundedIn)
    {
        var node = Position;
        var p = node + new Vector3(0f, CenterOffset, 0f);
        var mv = Limbs.Push(_world, Collision, StandHull, _getMatrix(), p, disp, dt, this); // Amedo 2026-09-27
        float turnOut = turn;
        var plat = Vector3.Zero; // Amedo 2026-09-29
        if (Platform_1357C0(out var carried, out float platTurn))
        {
            plat = carried;
            mv += plat;
            turnOut = turn + platTurn;
        }
        LastPlatform = plat; // Amedo 2026-10-01
        var hull = (Crouch.LowShape && (MotionFlagsHigh & 1u) != 0) || InAirState ? LowHull : StandHull;
        Collision.Begin();
        Collision.Gather(_world, p, ref mv, hull);
        GatherAgents_2846E8(p, mv, hull, MotionState is 13 or 14 ? 0x5A010u : 0x5B010u, SolveSkip_139964?.Invoke()); // Amedo
        if (StateFlagsOf is { } flagsOf) Collision.ClassFilter(ShapeClass(), flagsOf, MotionStateOf); // Amedo 2026-09-27
        if (StandingOn is { } support && (State70 & 0xF00) == 0x300 && 1e-4f < GameMath.Length(plat)) // Amedo 2026-09-29
            Collision.MarkPart_287E90(support, StandingPart, 8);
        if (0.1f < stepAdjust) stepAdjust = 0.1f;
        if (stepAdjust < -0.1f) stepAdjust = -0.1f;
        if (!groundedIn && !Collision.PointTest(p + new Vector3(0f, stepAdjust, 0f), 0x800, 0f))
        {
            p.Y += stepAdjust;
            CenterOffset += stepAdjust;
        }
        var start = p; // Amedo 2026-09-27
        var crushFrom = p; // Amedo
        var push = Collision.PushOut_2849C8(p + plat, true, false, true, BodyOf, out float depth) * 1.001f; // Amedo
        float pushSq = push.X * push.X + push.Y * push.Y + push.Z * push.Z; // Amedo
        if (1f < depth && 0.25f < depth - pushSq && CrushedAgents_136478()) Crushed_1367D0(); // Amedo
        float pushLen = MathF.Sqrt(pushSq);
        if (HullRadius < pushLen)
        {
            push *= HullRadius / pushLen;
            pushSq = HullRadius * HullRadius; // Amedo
        }
        if (push != Vector3.Zero)
        {
            var pn = push * GameMath.InverseLength(push);
            float into = GameMath.Dot(mv, pn);
            mv -= pn * MathF.Min(0f, into);
            Collision.Slide(p, push, out start, out _, false);
        }
        var (_, box) = CharacterCollision.LocalBounds((MotionFlagsHigh & 1u) != 0 ? LowHull : StandHull); // Amedo 2026-09-30
        mv += Collision.SpherePush_284DF0(start + plat, disp, box.Y, box.X);
        int hit = Collision.Slide(start, mv, out var newPos, out var normal, groundedIn);
        LastHit = hit;
        LastMove = mv;
        SetPosition(newPos - new Vector3(0f, CenterOffset, 0f));
        AfterSlide(hit, disp, normal, newPos); // Amedo 2026-09-29
        var moved = newPos - p;
        if (0.1f < mv.Y && moved.Y < mv.Y * 0.5f) Velocity.Y *= 0.5f;
        if (Collision.StepUp(newPos, mv, ref moved, groundedIn) &&
            !(MathF.Abs(moved.X) <= 5e-5f && MathF.Abs(moved.Y) <= 5e-5f && MathF.Abs(moved.Z) <= 5e-5f))
            SetPosition(Position + moved);
        DispatchTouches(); // Amedo 2026-09-27
        if (!CutsceneMode) // Amedo 2026-09-28
        {
            SurfaceHazards();
            NonSolidContacts();
        }
        Push_134810(dt); // Amedo 2026-09-30
        SetHigh(23, IsGrounded && (MotionFlagsHigh & 1u) == 0 && MotionState is not (6 or 7 or 10 or 11) && !GroundAhead()); // Amedo 2026-09-27
        if (0.2f < pushSq) // Amedo
        {
            var d = Position + new Vector3(0f, CenterOffset, 0f) - crushFrom;
            if (d.X * d.X + d.Y * d.Y + d.Z * d.Z < pushSq * 0.2f && CrushedAgents_136478()) Crushed_1367D0();
        }
        if (Kind == 3) ClingToWall_135E20(); // Amedo
        return turnOut;
    }

    // Amedo
    private void LiftOntoGround_136890(float up, float down)
    {
        var node = _getMatrix().Translation;
        var from = node + new Vector3(0f, CenterOffset + up, 0f);
        if (ShapeCastDown_280EA0(from, up + down, StandHull, 0x15B010u, out var hit))
        {
            CenterOffset = 0f;
            if (hit != node) SetPosition(hit);
            MotionFlags |= Grounded;
        }
        else MotionFlags &= ~Grounded;
    }

    // Amedo
    private bool ShapeCastDown_280EA0(Vector3 from, float length, ConvexHull hull, uint mask, out Vector3 hit)
    {
        var (min, max) = CharacterCollision.LocalBounds(hull);
        float step = (max.Y - min.Y) * 0.5f;
        var mv = new Vector3(5e-5f, 5e-5f, 5e-5f);
        var cur = from;
        while (true)
        {
            var probe = cur;
            probe.Y -= step;
            Collision.Begin();
            Collision.Gather(_world, probe, ref mv, hull);
            GatherAgents_2846E8(probe, mv, hull, mask);
            if (Collision.PointTest(probe, 0x800, 0f))
            {
                step *= 0.5f;
                if (step < 0.002f)
                {
                    hit = cur;
                    return true;
                }
                continue;
            }
            cur = probe;
            if (length < from.Y - cur.Y)
            {
                hit = cur;
                return false;
            }
        }
    }

    // Amedo
    public int CrushCounter_CC;
    private readonly List<CharacterCollision.Record> _crushSolids = new();

    // Amedo
    private bool CrushedAgents_136478()
    {
        bool crushed = true;
        _crushSolids.Clear();
        _crushSolids.AddRange(Collision.Solids);
        foreach (var r in _crushSolids)
        {
            if ((r.Flags & CharacterCollision.Dynamic) == 0 || (r.Flags & 0x104) == 0 || r.Owner is not { } owner) continue;
            if (((ComponentMaskOf?.Invoke(owner) ?? 0u) & 0x1FF000u) == 0) continue;
            if (((StateFlagsOf?.Invoke(owner) ?? 0u) & 0x800) != 0) continue;
            crushed = false;
            Touch?.Invoke(owner, 8);
        }
        return crushed;
    }

    // Amedo
    private void Crushed_1367D0()
    {
        CrushCounter_CC += (MotionFlagsHigh & ((1u << 21) | (1u << 22))) != 0 ? 2 : 4;
        if (CrushCounter_CC < 8 || Invincible) return;
        Damage(_getMatrix().Translation, 0xC00, 100, Self);
    }

    // Amedo
    private void CheckLinkedCrush_136678(float push, Vector3 from)
    {
        if (!(0.2f < push)) return;
        var d = _getMatrix().Translation + new Vector3(0f, CenterOffset, 0f) - from;
        if (!(d.X * d.X + d.Y * d.Y + d.Z * d.Z < push * 0.2f) && !(0.4f < push)) return;
        if (CrushedAgents_136478() && Link?.Follower_15FE98() is { } follower) follower.Crushed_1367D0();
    }

    // Amedo
    internal void GatherAgents_2846E8(Vector3 p, Vector3 mv, ConvexHull hull, uint mask, object? skip = null)
    {
        if (Bodies is not { } bodies) return;
        var (qmin, qmax) = CharacterCollision.LocalBounds(hull);
        float m = MathF.Abs(mv.X);
        if (m < MathF.Abs(mv.Y)) m = MathF.Abs(mv.Y);
        if (m < MathF.Abs(mv.Z)) m = MathF.Abs(mv.Z);
        qmin -= new Vector3(m);
        qmax += new Vector3(m);
        qmin -= new Vector3(0.1f);
        qmax += new Vector3(0.1f);
        qmin += p;
        qmax += p;
        var found = bodies(qmin, qmax, mask);
        if (skip is not null)
        {
            _skipBodies.Clear();
            foreach (var b in found)
                if (!ReferenceEquals(b.Owner, skip)) _skipBodies.Add(b);
            found = _skipBodies;
        }
        Collision.GatherBodies(found, qmin, qmax, hull);
    }

    private readonly List<CharacterBody> _skipBodies = new();
    private readonly List<CharacterCollision.Record> _clingSolids = new();

    // Amedo
    private void ClingToWall_135E20()
    {
        if ((MotionFlags & Grounded) != 0) return;
        var m = _getMatrix();
        var fwd = new Vector3(m.M31, m.M32, m.M33);
        var probe = m.Translation + fwd * 0.4f;
        _clingSolids.Clear();
        _clingSolids.AddRange(Collision.Solids);
        foreach (var r in _clingSolids)
        {
            if ((r.Flags & CharacterCollision.Static) == 0) continue;
            if (((_world.SurfaceMask(r.Triangle.Surface) >> 11) & 1) == 0) continue;
            if (!CharacterCollision.Contains(r, probe)) continue;
            var t = r.Triangle;
            var n = Vector3.Cross(t.B - t.A, t.C - t.A);
            n *= 1f / MathF.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);
            m = _getMatrix();
            if (!(0.707f < n.X * m.M31 + n.Y * m.M32 + n.Z * m.M33)) continue;
            n *= GameMath.InverseLength(n);
            if (!(MathF.Abs(n.Y) < 0.01f)) continue;
            var at = _getMatrix().Translation;
            if (_world.SegmentCast(at + new Vector3(0f, 0.1f, 0f), at - new Vector3(0f, 2f, 0f), 0x10, out _)) continue;
            Gravity = Param(1);
            MotionFlagsHigh &= ~((1u << 1) | (1u << 2) | (1u << 7) | (1u << 8));
            Velocity = n * -15f;
            EnterVehicle_13B938(Vehicle.Kind7, null, 0);
            if (Vehicle is WallCling cling) cling.WallNormal = n;
        }
    }

    // Amedo 2026-09-27
    private void DispatchTouches()
    {
        if (Touch is null) return;
        foreach (var r in Collision.Solids)
        {
            if ((r.Flags & CharacterCollision.Dynamic) == 0 || r.Owner is not { } owner) continue;
            bool vertical = false;
            if ((r.Flags & 0x20) != 0)
            {
                SendTouch(owner, 1f);
                vertical = true;
            }
            if ((r.Flags & 0x40) != 0)
            {
                SendTouch(owner, -1f);
                vertical = true;
            }
            if (vertical) continue;
            if ((r.Flags & 0x10) != 0) SendTouch(owner, 0f);
            if ((r.Flags & 0x100) != 0) SendTouch(owner, 0f);
            if ((r.Flags & 4) != 0) SendTouch(owner, 0f);
        }
    }

    // Amedo 2026-09-27
    private void SendTouch(object owner, float dirY)
    {
        uint touchClass = TouchClass_131ED0(owner); // Amedo 2026-10-05
        if (touchClass == 0) return;
        int type = TouchType(dirY);
        if (type == 0xF)
        {
            Touch!(owner, 4);
            Touch(owner, 6);
        }
        else if (type == 0x10)
        {
            Touch!(owner, 5);
            Touch(owner, 6);
        }
        else Touch!(owner, type);
        if (touchClass == 0x1000) TouchBack?.Invoke(owner); // Amedo 2026-10-05
        if (InstanceFlagsOf is { } flagsOf && (flagsOf(owner) & 0x8000) != 0) TouchHook?.Invoke(owner); // Amedo 2026-09-30
    }

    // Amedo
    private void SendAttack_1327D8(object owner, float dirY)
    {
        uint cls = 0;
        if (!ReferenceEquals(owner, Self) && ComponentMaskOf is { } maskOf)
        {
            uint m = maskOf(owner);
            if ((m & 0x1000) != 0) cls = 0x1000;
            else if ((m & 0x2000) != 0) cls = 0x2000;
            else if ((m & 0x4000) != 0) cls = 0x4000;
            else if ((m & 0x100000) != 0) cls = 0x100000;
            else if ((m & 0x8000) != 0) cls = 0x8000;
            else if ((m & 0x10000) != 0) cls = 0x10000;
            else if ((m & 0x20000) != 0) cls = 0x20000;
            else if ((m & 0x40000) != 0) cls = 0x40000;
        }
        if (cls != 0) Touch!(owner, dirY == -1f ? 4 : dirY == 1f ? 5 : 3);
        if (InstanceFlagsOf is { } flagsOf && (flagsOf(owner) & 0x8000) != 0) TouchHook?.Invoke(owner);
    }

    public Action? HullHits; // Amedo
    public Action<object>? TouchHook; // Amedo 2026-09-30
    public Action<object>? TouchBack; // Amedo 2026-10-05
    public Func<object, uint>? ComponentMaskOf;

    // Amedo 2026-10-05
    private uint TouchClass_131ED0(object owner)
    {
        if (ReferenceEquals(owner, Self)) return 0;
        if (ComponentMaskOf is not { } maskOf) return 0x10;
        uint m = maskOf(owner);
        if ((m & 0x1000) != 0)
            return Link?.Follower_15FE98() is { } follower && ReferenceEquals(follower.Self, owner) ? 0u : 0x1000u;
        if ((m & 0x2000) != 0) return 0x2000;
        if ((m & 0x4000) != 0) return 0x4000;
        if ((m & 0x100000) != 0) return 0x100000;
        if ((m & 0x8000) != 0) return 0x8000;
        if ((m & 0x10000) != 0) return 0x10000;
        if ((m & 0x20000) != 0) return 0x20000;
        if ((m & 0x40000) != 0) return 0x40000;
        return (m & 0x10) != 0 ? 0x10u : 0u;
    }

    internal void TouchSide(object owner) // Amedo 2026-09-27
    {
        if (Touch is not null) SendTouch(owner, 0f);
    }

    // Amedo
    internal void SweepRay_14E590(Vector3 dir, float push)
    {
        var from = Position;
        from.Y += 0.965345f;
        var to = from + dir;
        if (_world.SegmentCast(from, to, 0x10, out var ground)) SweepPush_14E590(ground.Point, to, dir, push);
        if (LimbBodies is not { } bodies) return;
        float t = Physics.HullQueries.SegmentBodies_1F1740(bodies(Vector3.Min(from, to), Vector3.Max(from, to)), from, to, out var body);
        if (!(0f <= t && t <= 1f) || body?.Owner is not { } owner) return;
        TouchSide(owner);
        if (((ComponentMaskOf?.Invoke(owner) ?? 0u) & 0x1FF000) != 0 && ((StateFlagsOf?.Invoke(owner) ?? 0u) & 0x1000) == 0) return;
        SweepPush_14E590(new Vector3(from.X * (1f - t) + to.X * t, from.Y * (1f - t) + to.Y * t, from.Z * (1f - t) + to.Z * t), to, dir, push);
    }

    // Amedo
    private void SweepPush_14E590(in Vector3 hit, in Vector3 end, in Vector3 dir, float push)
    {
        float dx = hit.X - end.X, dy = hit.Y - end.Y, dz = hit.Z - end.Z;
        float d = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        float k = d < 0.3f ? push * 0.3f : push * d;
        Limbs.Accumulated += new Vector3(dir.X * k, dir.Y * k, dir.Z * k);
    }

    // Amedo 2026-09-27
    internal bool SolidForClass(object owner)
    {
        int c = ShapeClass();
        return c == 0 || StateFlagsOf is null || (StateFlagsOf(owner) & (1u << c)) != 0;
    }

    // Amedo 2026-09-27
    public int TouchType(float dirY)
    {
        if ((MotionFlagsHigh & (1u << 22)) != 0) return 9;
        int s = MotionState;
        if (s is 7 or 8 or (>= 10 and < 15)) return s;
        if (dirY == -1f && Velocity.Y < -1f) return s == 6 ? 0xF : 4;
        if (dirY == 1f && 0f <= Velocity.Y) return s == 6 ? 0x10 : 5;
        return s == 6 ? 6 : 3;
    }

    // Amedo 2026-09-27
    public float LowestPoint()
    {
        var game = _getMatrix();
        float low = float.PositiveInfinity;
        if (Limbs.ExitPosition(7, 0.45f, game) is { } toeA) low = toeA.Y;
        if (Limbs.ExitPosition(6, 0.45f, game) is { } toeB && toeB.Y < low) low = toeB.Y;
        if (Limbs.ExitPosition(5, 0f, game) is { } hips && hips.Y < low) low = hips.Y;
        if (Limbs.ExitPosition(0, 0f, game) is { } head && head.Y < low) low = head.Y;
        if (float.IsPositiveInfinity(low)) return 0f;
        return low - game.Translation.Y - CenterOffset;
    }

    // Amedo 2026-09-27
    private bool GroundAhead()
    {
        var m = _getMatrix();
        var forward = new Vector3(m.M31, m.M32, m.M33);
        var near = m.Translation + forward * 0.5f;
        near.Y -= 0.8f;
        if (Collision.PointTest(near, 0x800, 0f)) return true;
        var far = m.Translation + forward * 1.6f;
        far.Y -= 0.8f;
        return Collision.PointTest(far, 0x800, 0f);
    }

    // Amedo 2026-09-29
    private bool Platform_1357C0(out Vector3 plat, out float platTurn)
    {
        plat = Vector3.Zero;
        platTurn = 0f;
        if ((State70 & 0xF00) != 0x300)
        {
            PlatformTurn = 0f;
            return false;
        }
        if (StandingOn is not { } support || PartMatrix is null || !ReferenceEquals(ModelOf?.Invoke(support), SupportModel)) return false;
        var m = PartMatrix(support, StandingPart);
        var w = Vector3.Transform(SupportLocal, m);
        plat = w - (Position + new Vector3(0f, CenterOffset, 0f));
        var r = SupportInverse * m;
        platTurn = -GameMath.Asin16_18B7F8(r.M13) * AngleToRadians;
        PlatformTurn = platTurn;
        return true;
    }

    // Amedo 2026-09-29
    private static bool Rigid(in Matrix4x4 m) =>
        !(0.1f < MathF.Abs(GameMath.Length(new Vector3(m.M11, m.M12, m.M13)) - 1f)) &&
        !(0.1f < MathF.Abs(GameMath.Length(new Vector3(m.M21, m.M22, m.M23)) - 1f)) &&
        !(0.1f < MathF.Abs(GameMath.Length(new Vector3(m.M31, m.M32, m.M33)) - 1f));

    private void AfterSlide(int hit, in Vector3 disp, in Vector3 normal, in Vector3 newPos)
    {
        if (hit == -1)
        {
            MotionFlags &= ~Grounded;
            if (!IsGrounded)
            {
                State70 &= ~0xF00ul;
                StandingOn = null; // Amedo 2026-09-27
                StandingPart = -1;
            }
            return;
        }
        if (hit >= 0 && hit < Collision.Solids.Count)
        {
            var rec = Collision.Solids[hit];
            if ((rec.Flags & CharacterCollision.Dynamic) != 0) // Amedo 2026-09-27
            {
                StandingOn = rec.Owner;
                StandingPart = rec.Part;
                SupportModel = rec.Owner is { } owner ? ModelOf?.Invoke(owner) : null; // Amedo 2026-09-29
                State70 = (State70 & ~0xF00ul) | 0x200;
                if (rec.Owner is { } body && InstanceFlagsOf is { } flagsOf && (flagsOf(body) & 0x4000) != 0 && PartMatrix is { } partOf)
                {
                    var m = partOf(body, rec.Part);
                    if (Rigid(m) && Matrix4x4.Invert(m, out var inverse))
                    {
                        State70 = (State70 & ~0xF00ul) | 0x300;
                        SupportInverse = inverse;
                        SupportLocal = Vector3.Transform(newPos, inverse);
                    }
                }
            }
            else
            {
                State70 = (State70 & ~0xF00ul) | 0x100;
                GroundTriangle = rec.Triangle;
                StandingOn = null; // Amedo 2026-09-27
                StandingPart = -1;
            }
        }
        GroundNormal = normal;
        if (disp.Y < 0.001f) MotionFlags |= Grounded;
        else MotionFlags &= ~Grounded;
        if (!IsGrounded) State70 &= ~0xF00ul;
    }

    private void FallAndLand()
    {
        if ((MotionFlags & Falling) != 0)
        {
            LandingCheck();
            return;
        }
        if ((MotionFlags & 1) != 0)
        {
            SnapToGround_13D428(); // Amedo
            return;
        }
        float down = Velocity.X * 0f + Velocity.Y * -1f + Velocity.Z * 0f;
        if (FallSpeed < down) StartFall(true);
    }

    public Action<string, int, string>? Unimplemented; // Amedo 2026-09-27

    public Func<bool>? SnapInstanceToGround; // Amedo

    // Amedo
    public void SnapToGround_13D428()
    {
        if (SnapInstanceToGround is not { } snap) return;
        MotionFlags = (MotionFlags & ~Grounded) | (snap() ? Grounded : 0);
    }

    // Amedo 2026-09-27
    public void Launch(Vector3 velocity, bool relative, float gravity, int slot, Matrix4x4? sourceNode = null) // Amedo
    {
        var m = _getMatrix();
        var v = relative ? Vector3.TransformNormal(velocity, m) : velocity;
        var up = new Vector3(m.M21, m.M22, m.M23);
        float vertical = GameMath.Dot(v, up);
        Jump.RequestLaunch(slot, vertical, gravity);
        var h = v - up * vertical;
        if (MathF.Abs(h.X) <= 0.005f && MathF.Abs(h.Y) <= 0.005f && MathF.Abs(h.Z) <= 0.005f) Velocity = Vector3.Zero;
        else Locomotion.ForceVector(h, h, 0); // Amedo 2026-09-28
        Vehicle?.Push_160168(velocity, relative ? sourceNode ?? m : null); // Amedo
    }

    // Amedo 2026-09-29
    public void Transfer_13BEA0(Matrix4x4 obj)
    {
        GroundPoint = Vector3.Transform(GroundPoint, obj);
        FallStart = Vector3.Transform(FallStart, obj);
        GroundNormal = Vector3.TransformNormal(GroundNormal, obj);
        if (Link is { IsLeader: false } own) // Amedo 2026-10-05
        {
            own.Target1F0 = Vector3.Transform(own.Target1F0, obj);
            var v200 = Vector3.TransformNormal(new Vector3(own.Vec200.X, own.Vec200.Y, own.Vec200.Z), obj);
            own.Vec200 = new Vector4(v200, own.Vec200.W);
        }
        else if (Link is { } lead) lead.Partner1B0 *= obj; // Amedo 2026-10-05
        Vehicle?.Transfer(obj); // Amedo 2026-09-29
        Bones.Transfer_15F500(obj);
        Velocity = Vector3.TransformNormal(Velocity, obj); // Amedo 2026-10-05
    }

    // Amedo
    public void ResetFall_13F820()
    {
        MotionFlags &= ~Falling;
        MotionFlagsHigh |= 1u << 24;
        FallStart = new Vector3(0f, -100000f, 0f);
    }

    // Amedo 2026-09-29
    public void Warped_13FAE0()
    {
        MotionFlags &= ~Falling;
        MotionFlagsHigh |= 1u << 24;
        FallStart = new Vector3(0f, -100000f, 0f);
        Bones.Warped_15F398();
    }

    public void StartFall(bool trigger)
    {
        FallStart = Position;
        if (trigger) _trigger(0x21);
        MotionFlagsHigh = (MotionFlagsHigh & ~(1u << 14)) | (1u << 13);
        MotionFlags |= Falling;
    }

    private void LandingCheck()
    {
        bool allowed = (State70 & 0x2000) == 0;
        float h = FallStart.Y - Position.Y;
        if (allowed)
        {
            if ((MotionFlagsHigh & (1u << 13)) != 0)
            {
                if (10f <= h)
                {
                    _trigger(0x22);
                    MotionFlagsHigh = (MotionFlagsHigh | (1u << 14)) & ~(1u << 13);
                    if (Kind != 4) Bones.Look.DeathTimer = 3f; // Amedo
                }
            }
            else if ((MotionFlagsHigh & (1u << 24)) == 0 && 20f <= h) _trigger(0xB);
        }
        if (IsGrounded) Land(true);
    }

    private void Land(bool trigger)
    {
        if (MotionState is 13 or 14) MotionWord &= ~0x400;
        if (MotionState is not (6 or 10) && trigger)
        {
            int slot = InAirState ? 0x2A
                : (MotionFlagsHigh & (1u << 14)) != 0 ? 0x29
                : (MotionFlags & Moving) != 0 ? 0x28 : 0x27;
            if (slot == 0x29 && Kind != 4) Bones.Look.DeathTimer = 2f; // Amedo
            _trigger(slot);
        }
        MotionFlagsHigh &= ~(1u << 24);
        MotionFlags &= ~Falling;
        State70 &= ~0x2000ul;
    }

    public CharacterPad? Pad;
    public Vector3 CameraRight = Vector3.UnitX, CameraUp = Vector3.UnitY, CameraForward = Vector3.UnitZ;
    public float LookStickX, LookStickY; // Amedo 2026-09-29

    public bool AutomaticInput; // Amedo 2026-09-28
    public Func<Vector3>? AutoDisplacement; // Amedo 2026-09-29
    public Func<Vector3?>? LookTarget; // Amedo 2026-10-02
    public Func<bool>? MotionPaused;

    public void ApplyInput(float dt = 0f)
    {
        if (AutomaticInput) // Amedo 2026-09-28
        {
            AutoInput_163168(0f < dt && AutoDisplacement is { } displacement ? displacement() * (1f / dt) : Vector3.Zero); // Amedo 2026-09-29
            return;
        }
        if (Pad is not { } pad) return;
        if (Binding is { } binding) // Amedo 2026-09-29
        {
            VehicleInput_1623E8(pad, binding);
            return;
        }
        float x = pad.BoundAxis(0), y = pad.BoundAxis(1);
        if (MathF.Abs(x) <= 5e-5f && MathF.Abs(y) <= 5e-5f) Move = Vector3.Zero;
        else
        {
            var m = _getMatrix();
            float len = MathF.Sqrt(x * x + 0f * 0f + y * y);
            var t = (CameraRight * x + CameraUp * 0f + CameraForward * y) * (1f / len);
            var n1 = new Vector3(m.M21, m.M22, m.M23);
            var w = Vector3.Cross(n1, t);
            var dir = Vector3.Cross(w, n1) * len;
            MoveFromDirection(dir, len, m);
        }
        Cross = (InputDisabled & 0x8) == 0 ? pad.BoundButton(0) : 0f;
        Square = (InputDisabled & 0x10) == 0 ? pad.BoundButton(1) : 0f;
        Circle = (InputDisabled & 0x20) == 0 ? pad.BoundButton(2) : 0f;
        RightMinusLeft = (InputDisabled & 0x40) == 0 ? pad.BoundButton(4) - pad.BoundButton(3) : 0f;
    }

    // Amedo 2026-09-29
    private void AutoInput_163168(Vector3 v)
    {
        if (MathF.Abs(v.X) <= 5e-5f && MathF.Abs(v.Y) <= 5e-5f && MathF.Abs(v.Z) <= 5e-5f)
        {
            Move = Vector3.Zero;
            return;
        }
        var s = v * (1f / MaxSpeed_13FDA8());
        float len = MathF.Sqrt(s.X * s.X + s.Y * s.Y + s.Z * s.Z);
        MoveFromDirection(s * GameMath.InverseLength(s), len, _getMatrix());
    }

    // Amedo 2026-09-29
    private float MaxSpeed_13FDA8()
    {
        if (Bones.Kind == 4) return 1f;
        float run = Param(7);
        return run <= 0f ? Param(6) : run;
    }

    private void MoveFromDirection(Vector3 dir, float len, Matrix4x4 m)
    {
        var up = new Vector3(m.M21, m.M22, m.M23);
        var fwd = new Vector3(m.M31, m.M32, m.M33);
        var d = dir * GameMath.InverseLength(dir);
        var qDesired = FrameQuaternion(Vector3.Cross(up, d), up, d);
        var f = fwd * GameMath.InverseLength(fwd);
        var qCurrent = FrameQuaternion(Vector3.Cross(up, f), up, f);
        float qLenSq = qCurrent.X * qCurrent.X + qCurrent.Y * qCurrent.Y + qCurrent.Z * qCurrent.Z + qCurrent.W * qCurrent.W;
        float inv = 1.00000005e-20f < qLenSq ? 1f / MathF.Sqrt(qLenSq) : 0f; // Amedo
        var qInverse = new Quaternion(-qCurrent.X * inv, -qCurrent.Y * inv, -qCurrent.Z * inv, qCurrent.W * inv);
        var delta = qDesired * qInverse;
        var rot = m;
        rot.Translation = Vector3.Zero;
        var qNode = GameMath.MatrixToQuat_186900(rot); // Amedo
        var moved = GameMath.RotateByQuat_186D98(qNode * delta, new Vector4(0f, 0f, len, 1f), false); // Amedo
        var vec = new Vector3(moved.X, moved.Y, moved.Z);
        float vecLen = MathF.Sqrt(vec.X * vec.X + vec.Y * vec.Y + vec.Z * vec.Z); // Amedo
        int angle = GameMath.AxisAngle_186B50(ref delta, out var axis);
        int signed = ((angle - 0x8000) & 0xFFFF) - 0x8000;
        float turn = signed * AngleToRadians * (axis.Y < 0f ? -0.31830987f : 0.31830987f);
        float z = vec.Z, xx = vec.X;
        if (1f < vecLen)
        {
            z = vec.Z / vecLen;
            xx = vec.X / vecLen;
        }
        MoveTurn = (InputDisabled & 1) == 0 ? turn : 0f;
        MoveZ = (InputDisabled & 2) == 0 ? z : 0f;
        MoveX = (InputDisabled & 4) == 0 ? xx : 0f;
        Move = vec;
    }

    private static Quaternion FrameQuaternion(in Vector3 right, in Vector3 up, in Vector3 forward) =>
        GameMath.MatrixToQuat_186900(new Matrix4x4(right.X, right.Y, right.Z, 0f, up.X, up.Y, up.Z, 0f, // Amedo
                                                         forward.X, forward.Y, forward.Z, 0f, 0f, 0f, 0f, 1f));

    public void Trigger(int slot) => _trigger(slot);
}
