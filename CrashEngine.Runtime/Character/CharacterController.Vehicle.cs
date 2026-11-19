using System.Numerics;
using CrashEngine.Runtime.Character.Vehicles;
using CrashEngine.Runtime.Physics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-09-29
public sealed partial class CharacterController
{
    public const int EnterVehicleSlot = 0x4A, ExitVehicleSlot = 0x4B;

    public Vehicle? Vehicle;
    public RigidBody? Body5;
    public VehicleBinding? Binding;
    public Action<bool>? SetCollidable;
    public Action<(Vector3 Min, Vector3 Max)?>? SetInstanceBox;
    public Action<int>? TriggerForced;
    public Action<int, int>? TriggerOnChannel;

    public void EnterVehicle_13B938(int kind, CharacterController? other, int param4, object? otherObject = null)
    {
        Unlink_132E50();
        Vector3 v;
        if (Vehicle is not { } old)
        {
            v = Velocity;
            if (GameMath.Length(v) < 15f)
            {
                var node = _getMatrix();
                v = new Vector3(node.M31, node.M32, node.M33) * 15f;
            }
        }
        else
        {
            v = old.Velocity ?? Vector3.Zero;
            ExitVehicle_13BCF0(true);
            if (Vehicle is { } left)
            {
                left.Destroy();
                Vehicle = null;
            }
        }
        Vehicle? vehicle = null;
        VehicleBinding? binding = null;
        switch (kind)
        {
            case Vehicle.Rollerbrawl:
                vehicle = new RollerBrawl(this, other);
                binding = new VehicleBinding(0);
                break;
            case Vehicle.Humiliskate: // Amedo
                vehicle = new Humiliskate(this, other);
                binding = new VehicleBinding(0);
                break;
            case Vehicle.Kind7: // Amedo
                vehicle = new WallCling(this);
                binding = new VehicleBinding(0);
                break;
            case Vehicle.Wrestle: // Amedo
                vehicle = new Wrestle(this, otherObject);
                binding = new VehicleBinding(0);
                break;
            case Vehicle.Hoverboard: // Amedo
                vehicle = new Hoverboard(this, otherObject, param4 != 0);
                binding = new VehicleBinding(param4);
                break;
        }
        if (vehicle is not null)
        {
            InstanceBox_1F0168(vehicle.Box);
            vehicle.SetPosition(_getMatrix().Translation);
            vehicle.SetVelocity(v);
            Attach_13B6F8(vehicle);
        }
        Binding = binding;
        if (kind is Vehicle.Rollerbrawl or Vehicle.Humiliskate && other is not null)
            other.Attach_13B6F8(new PassengerVehicle(Vehicle.Passenger, other, this));
        if (other is not null)
        {
            other.Binding = null;
            other.AutomaticInput = false;
        }
        Log?.Invoke($"[vehicle] enter kind {kind}{(other is not null ? " + passenger 8" : "")}, velocity {v}");
    }

    public void Attach_13B6F8(Vehicle vehicle)
    {
        InputDisabled = 0;
        MotionState = 3;
        Vehicle = vehicle;
        NewComponents(); // Amedo
        Claw = null;
        if (!vehicle.KeepsGun) Gun = null;
        if (TriggerForced is { } forced) forced(EnterVehicleSlot);
        else _trigger(EnterVehicleSlot);
        AutomaticInput = false;
        MotionFlags &= ~Falling;
        MotionFlagsHigh |= 1u << 24;
        FallStart = new Vector3(0f, -100000f, 0f);
        Release_132FB0();
        ShiftCenter(-CenterOffset);
        Offset27C = 0f;
    }

    public void ExitVehicle_13BCF0(bool quiet)
    {
        if (Vehicle is not { } vehicle) return;
        int kind = vehicle.Kind;
        if (vehicle.Driver && kind is Vehicle.Rollerbrawl or Vehicle.Humiliskate && vehicle.Other is { Vehicle: not null } other)
        {
            other.Binding = null;
            other.Pad = null;
            other._setMatrix(vehicle.Frame);
            other.DestroyComponents_1351A0();
            other.CreateComponents_134F18();
            if (!quiet) other._trigger(ExitVehicleSlot);
            other.InstanceBox_1F0168(null);
        }
        Binding = null;
        _setMatrix(vehicle.Frame);
        DestroyComponents_1351A0();
        CreateComponents_134F18();
        if (!quiet) _trigger(ExitVehicleSlot);
        InstanceBox_1F0168(null);
        Log?.Invoke($"[vehicle] exit kind {kind}{(quiet ? " (quiet)" : "")} at {vehicle.Frame.Translation}");
    }

    private void InstanceBox_1F0168((Vector3 Min, Vector3 Max)? box)
    {
        SetInstanceBox?.Invoke(box);
        AltShape_1F0168(box);
    }

    private void DestroyComponents_1351A0()
    {
        DestroyLink_14DCA8(); // Amedo 2026-10-05
        if (Vehicle is { } vehicle)
        {
            vehicle.Destroy();
            Vehicle = null;
        }
    }

    public Action<float, float, Vector3>? CameraImpulse;
    public CrashEngine.Core.GameRandom Rng = CrashEngine.Core.GameRandom.Shared; // Amedo
    public Action<byte>? SetBump154; // Amedo
    public Action<Matrix4x4?>? SnowCloud;

    public object? Self;
    public Func<Vector3, Vector3, object?, IReadOnlyList<CharacterBody>>? BallBodies_1ECFC0;
    public Func<Vector3, Vector3, object?, IReadOnlyList<CharacterBody>>? SkateBodies_157FC0; // Amedo
    public Func<object, object?, Vector3, Vector3, bool>? BodyHit_23D1E0; // Amedo
    public Action<float>? TrailLevel; // Amedo
    public Action<object, Matrix4x4>? SetNodeOf; // Amedo
    public Action<object?>? SetAttachedTo_28; // Amedo
    public Func<Vector3, Vector3, object?, Vector3?>? HoverRay_281408; // Amedo
    public Func<Vector3, Vector3, IReadOnlyList<object>>? TowCandidates_1ECFC0; // Amedo
    public Func<object, Vector3?>? PositionOf; // Amedo
    public Func<object, bool>? AliveOf; // Amedo
    public Func<ISkateSounds?>? SkateSounds; // Amedo
    public Action<Importer.SkidMarkRing, bool>? SkidMarks; // Amedo

    public void SetNode_18AF40(Matrix4x4 m) => _setMatrix(m);

    public void TouchFrom_137858(object owner, Vector3 velocity, Vector3 direction)
    {
        var d = direction;
        d *= GameMath.InverseLength(d);
        float dirY;
        if (d.Y < -0.9f && velocity.Y < -1f) dirY = -1f;
        else if (0.9f < d.Y) dirY = 1f;
        else dirY = 0f;
        if (Touch is not null) SendAttack_1327D8(owner, dirY); // Amedo
    }

    public void SurfaceMessage_140958(CollisionWorld.Surface s) => Damage(s.HitSource, s.HitKind, s.HitAmount, this);

    private void VehicleInput_1623E8(CharacterPad pad, VehicleBinding binding)
    {
        float x = pad.BoundAxis(0), y = pad.BoundAxis(1);
        float cross = pad.BoundButton(0), circle = pad.BoundButton(2), square = pad.BoundButton(1);
        if (binding.Mode == 0) Cross = (InputDisabled & 0x8) == 0 ? cross : 0f;
        else
        {
            Cross = (InputDisabled & 0x8) == 0 ? y : 0f;
            y = 0f;
        }
        Square = (InputDisabled & 0x10) == 0 ? square : 0f;
        Circle = (InputDisabled & 0x20) == 0 ? circle : 0f;
        InputDisabled &= ~0x38u;
        if (x == 0f && y == 0f) return;
        var right = new Vector3(CameraRight.X, 0f, CameraRight.Z);
        right *= GameMath.InverseLength(right);
        var fwd = new Vector3(CameraForward.X, 0f, CameraForward.Z);
        fwd *= GameMath.InverseLength(fwd);
        float mx = right.X * x + fwd.X * y;
        float mz = right.Z * x + fwd.Z * y;
        float len = MathF.Sqrt(mx * mx + mz * mz);
        if (1f < len)
        {
            float k = 1f / len;
            mz *= k;
            mx *= k;
        }
        MoveX = (InputDisabled & 0x4) == 0 ? mx : 0f;
        MoveZ = (InputDisabled & 0x2) == 0 ? mz : 0f;
    }

    internal void Release_132FB0()
    {
        if ((State70 & 0xF00) is not (0x200 or 0x300)) return;
        State70 &= ~0xF00UL;
        StandingOn = null;
        StandingPart = -1;
        LastSurface = null;
        GroundSurfaceId = -1; // Amedo 2026-10-05
        if ((MotionFlagsHigh & LeaderBit) != 0) Link?.Follower_15FE98()?.SetSurface_13F658(null);
    }

    // Amedo 2026-10-05
    internal void SetSurface_13F658(CollisionWorld.Surface? surface)
    {
        LastSurface = surface;
        GroundSurfaceId = surface?.Id ?? -1;
        if ((MotionFlagsHigh & LeaderBit) != 0) Link?.Follower_15FE98()?.SetSurface_13F658(surface);
    }
}
