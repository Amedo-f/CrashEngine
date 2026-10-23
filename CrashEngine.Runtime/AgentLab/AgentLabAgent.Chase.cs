using System.Numerics;
using CrashEngine.Runtime.Camera;
using CrashEngine.Runtime.Physics;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed partial class AgentLabAgent
{
    private const int PacketHomePower = 11;
    private const int PacketBanking = 16;
    private const int PacketDamping = 17;
    private const int PacketMaxBank = 20;

    private bool _chasing;
    private bool _chaseAir;
    private float _chaseSpeed;
    private float _chaseCurrentSpeed;
    private float _homePower = 1f;
    private float _banking;
    private float _damping;
    private float _maxBank = 80f;
    private float _sqrTolerance;

    public float ChaseSpeed => _chaseCurrentSpeed;

    private void StartChase(TwinBehaviourControlPacket packet)
    {
        _chaseAir = packet.Motion is TwinBehaviourControlPacket.MotionType.AIR_CHASE or TwinBehaviourControlPacket.MotionType.UNKNOWN_12; // Amedo
        _chaseClimb = packet.Motion == TwinBehaviourControlPacket.MotionType.UNKNOWN_13; // Amedo
        PacketFloat(packet, PacketMoveSpeed, out _chaseSpeed);
        _chaseCurrentSpeed = PacketHas(packet, PacketDamping) ? 0f : _chaseSpeed;
        _homePower = PacketFloat(packet, PacketHomePower, out float hp) ? hp : 1f;
        _banking   = PacketFloat(packet, PacketBanking, out float bank) ? bank : 0f;
        _maxBank   = PacketFloat(packet, PacketMaxBank, out float maxBank) ? maxBank : 80f;
        _damping   = PacketFloat(packet, PacketDamping, out float damping) ? damping : 0f;
        _physics?.SetState(PhysicsController.StateSmoothNormal, _chaseClimb); // Amedo
        _chasing   = true;
    }

    private void UpdateChase(float dt)
    {
        var here = LocalMatrix.Translation;
        if (_sqrTolerance != 0f && Vector3.DistanceSquared(here, _moveTarget) < _sqrTolerance)
        {
            _translationDone = _rotationDone = true;
            return;
        }
        if (_chaseClimb) // Amedo
        {
            StepClimbingChase_231538(here, dt);
            return;
        }
        if (_chaseAir && _physics?.Body is { } ridden) // Amedo
        {
            StepRiddenAirChase_2310B0(ridden, dt);
            return;
        }

        var tracking = !_chaseAir && _physics is { } physics && physics.Has(PhysicsController.Airborne) ? physics : null; // Amedo 2026-09-27
        float turn = _chaseAir || tracking is not null ? SteerBodyTowards_22C330(_moveTarget, _homePower * dt, _chaseAir, _banking)
                                                       : SteerTowards_22CD68(_moveTarget, _homePower * dt, _banking, _maxBank);

        float speed = _chaseSpeed;
        if (0f < _damping) // Amedo
        {
            float slow = dt * turn * (_damping * 100f);
            if (0.9f < slow) slow = 0.9f;
            speed -= speed * slow;
        }
        _chaseCurrentSpeed = speed;

        if (tracking is not null) GroundTrack(tracking, dt);
        var m = LocalMatrix;
        var velocity = new Vector3(m.M31, m.M32, m.M33) * speed;
        if (!_chaseAir) RecordGroundVelocity(velocity, tracking);
        var move = ClampToRadius(velocity * dt);
        if (MathF.Abs(move.X) <= 5e-5f && MathF.Abs(move.Y) <= 5e-5f && MathF.Abs(move.Z) <= 5e-5f) return;
        SetLocalPosition(m.Translation + move);
    }

    private void RecordGroundVelocity(Vector3 velocity, PhysicsController? tracking) // Amedo 2026-09-27
    {
        _prevVelocity = _velocity;
        if (tracking is not null)
        {
            if ((tracking.State & PhysicsController.StateHadContact) != 0) _velocity = velocity;
            else { _velocity.X = velocity.X; _velocity.Z = velocity.Z; }
            return;
        }
        float oldY = _velocity.Y;
        _velocity = velocity;
        _velocity.Y += oldY;
    }

    private void GroundTrack(PhysicsController ctrl, float dt) // Amedo 2026-09-27 = RightRigidBody 0x24B078
    {
        if ((ctrl.Flags & (PhysicsController.TouchedAgent | PhysicsController.TouchedTerrain)) == 0 && 0.2f < ctrl.AirTime)
        {
            float t = (ctrl.Has(PhysicsController.TiltPowerSet) ? ctrl.ParamB4 * 1.5f : 21f) * dt;
            var relaxed = ctrl.GroundNormal + (Vector3.UnitY - ctrl.GroundNormal) * t;
            ctrl.GroundNormal = relaxed * InverseLength(relaxed);
        }
        var n = ctrl.GroundNormal;
        var m = LocalMatrix;
        var right = new Vector3(m.M11, m.M12, m.M13);
        var forward = Vector3.Cross(right, n);
        var side = Vector3.Cross(n, forward);
        var q = CamMath.MatrixToQuat_186900(new Matrix4x4(side.X, side.Y, side.Z, 0f,
                                                          n.X, n.Y, n.Z, 0f,
                                                          forward.X, forward.Y, forward.Z, 0f,
                                                          0f, 0f, 0f, 1f)); // Amedo
        if (q != LocalRotation) SetLocalRotation(q);
        var press = n * -0.07f;
        if (MathF.Abs(press.X) <= 5e-5f && MathF.Abs(press.Y) <= 5e-5f && MathF.Abs(press.Z) <= 5e-5f) return;
        SetLocalPosition(LocalMatrix.Translation + press);
    }

    // Amedo
    private void StepRiddenAirChase_2310B0(RigidBody body, float dt)
    {
        var m = LocalMatrix;
        var forward = new Vector3(m.M31, m.M32, m.M33);
        var bodyForward = new Vector3(body.World.M31, body.World.M32, body.World.M33);
        float pull = -(_homePower * dt * (body.Mass * 60f));
        var d = _moveTarget - m.Translation;
        if (d.X == 0f && d.Z == 0f) d.X = 0.01f;
        float inv = InverseLength(d);
        d = new Vector3(d.X * inv, d.Y * inv, d.Z * inv);
        int yaw = CamMath.AngleBetween_187EE0(new Vector4(bodyForward, 1f), new Vector4(d, 1f), 1);
        body.Torque += new Vector3(0f, yaw * 9.58738e-5f * pull, 0f);
        var w = Vector3.Cross(Vector3.Cross(Vector3.UnitY, d), d);
        float iw = InverseLength(w);
        w = new Vector3(-(w.X * iw), -(w.Y * iw), -(w.Z * iw));
        var level = Vector3.Cross(new Vector3(body.World.M21, body.World.M22, body.World.M23), w);
        body.Torque += new Vector3(level.X * pull, level.Y * pull, level.Z * pull);
        float push = _chaseCurrentSpeed + _chaseCurrentSpeed;
        body.Force += new Vector3(forward.X * push * body.Mass, forward.Y * push * body.Mass, forward.Z * push * body.Mass);
    }

    // Amedo
    internal float SteerBodyTowards_22C330(Vector3 target, float rate, bool upright, float banking)
    {
        var m = LocalMatrix;
        var d = target - m.Translation;
        float inv = InverseLength(d);
        var dir = new Vector3(d.X * inv, d.Y * inv, d.Z * inv);
        float t = MathF.Min(rate, 1f);
        var forward = new Vector3(m.M31, m.M32, m.M33);
        var up = new Vector3(m.M21, m.M22, m.M23);
        Vector3 wanted = up;
        if (banking != 0f)
        {
            wanted = new Vector3(dir.X * banking, dir.Y * banking + 1.5f, dir.Z * banking);
            wanted = new Vector3(wanted.X + up.X * 1.25f, wanted.Y + up.Y * 1.25f, wanted.Z + up.Z * 1.25f);
            float iw = InverseLength(wanted);
            wanted = new Vector3(wanted.X * iw, wanted.Y * iw, wanted.Z * iw);
        }
        var next = CamMath.Slerp_188300(t, forward, dir);
        float k = InverseLength(next);
        next = new Vector3(next.X * k, next.Y * k, next.Z * k);
        float turn = 1f - (forward.X * next.X + forward.Y * next.Y + forward.Z * next.Z);
        Matrix4x4 rot;
        if (banking == 0f && upright) rot = Along_185A40(next);
        else
        {
            var right = Vector3.Cross(wanted, next);
            var newUp = Vector3.Cross(next, right);
            rot = new Matrix4x4(right.X, right.Y, right.Z, 0f,
                                newUp.X, newUp.Y, newUp.Z, 0f,
                                next.X, next.Y, next.Z, 0f,
                                0f, 0f, 0f, 1f);
        }
        var q = CamMath.MatrixToQuat_186900(rot);
        if (q != LocalRotation) SetLocalRotation(q);
        return turn;
    }

    // Amedo
    private float SteerTowards_22CD68(Vector3 target, float rate, float roll, float maxRoll)
    {
        bool rolls = roll != 0f;
        float t = MathF.Min(rate, 1f);
        var m = LocalMatrix;
        var d = target - m.Translation;
        d.Y = 0f;
        float id = InverseLength(d); // Amedo
        var dir = new Vector3(d.X * id, d.Y * id, d.Z * id);
        if (dir.X == 0f && dir.Z == 0f) return 0f;
        var forward = new Vector3(m.M31, 0f, m.M33);
        if (0.99999f < forward.X * dir.X + forward.Y * dir.Y + forward.Z * dir.Z) return 0f;
        var next = CamMath.Slerp_188300(t, forward, dir);
        float inext = InverseLength(next); // Amedo
        next = new Vector3(next.X * inext, next.Y * inext, next.Z * inext);
        float turn = 1f - (forward.X * next.X + forward.Y * next.Y + forward.Z * next.Z);
        float side = 1f;
        if (rolls && 0f < next.Z * dir.X - next.X * dir.Z) side = -1f;
        CamMath.Frame_1879D0(next, out var right, out var up);
        SetLocalRotation(CamMath.MatrixToQuat_186900(new Matrix4x4(right.X, right.Y, right.Z, 0f,
                                                                    up.X, up.Y, up.Z, 0f,
                                                                    next.X, next.Y, next.Z, 0f,
                                                                    0f, 0f, 1f, 1f)));
        if (!rolls) return turn;
        float angle = turn * side * roll;
        if (maxRoll < angle) angle = maxRoll;
        else if (angle < -maxRoll) angle = -maxRoll;
        int units = CamMath.FromValue_11BFA0(angle, 1);
        if (units != 0) SetLocalRotation(CamMath.Mul_18D558(LocalRotation, CamMath.RotationZ_18D6B0(units)));
        return turn;
    }

    private static (Vector3 Right, Vector3 Up) FacingAxes(Vector3 forward)
    {
        float k = 1f - forward.Y * forward.Y;
        if (k == 0f)
        {
            float s = forward.Y > 0f ? 1f : -1f;
            return (new Vector3(0f, 0f, s), new Vector3(s, 0f, 0f));
        }
        var up = Unit(new Vector3(-forward.X * forward.Y, k, -forward.Y * forward.Z));
        return (Unit(Vector3.Cross(up, forward)), up);
    }

    private void SetFacing(Vector3 forward)
    {
        var (right, up) = FacingAxes(forward);
        var rot = new Matrix4x4(right.X, right.Y, right.Z, 0f,
                                up.X, up.Y, up.Z, 0f,
                                forward.X, forward.Y, forward.Z, 0f,
                                0f, 0f, 0f, 1f);
        SetLocalRotation(Quaternion.CreateFromRotationMatrix(rot));
    }

    private static Vector3 Unit(Vector3 v)
    {
        float lenSq = v.LengthSquared();
        return lenSq > 2.4999998e-09f ? v / MathF.Sqrt(lenSq) : Vector3.Zero;
    }
}
