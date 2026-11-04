using System.Numerics;
using CrashEngine.Runtime.Camera;
using CrashEngine.Runtime.Physics;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabAgent
{
    private bool _chaseClimb;

    private void StepClimbingChase_231538(Vector3 here, float dt)
    {
        var ctrl = _physics;
        bool flat = (ctrl is null || 0.3f < ctrl.GroundNormal.Y) && here.Y <= _moveTarget.Y;
        float speed;
        Vector3 velocity;
        if (flat)
        {
            SteerTowards_22CD68(_moveTarget, _homePower * dt, _banking, _maxBank);
            speed = ChaseSpeed_20F1C0(0f);
            var m = LocalMatrix;
            velocity = new Vector3(m.M31, m.M32, m.M33) * speed;
            RecordGroundVelocity(velocity, null);
        }
        else
        {
            if (ctrl is null)
            {
                World.Unimplemented("motion", (int)TwinBehaviourControlPacket.MotionType.UNKNOWN_13, "UNKNOWN_13 without a controller (0x231538 reads [0]+0x40)");
                return;
            }
            var n = ctrl.GroundNormal;
            n *= GameMath.InverseLength(n);
            var (bmin, bmax) = CommittedBox;
            var foot = (bmax - bmin) * 0.5f + bmin - n * _logicalRadius;
            float w = -(n.X * foot.X + n.Y * foot.Y + n.Z * foot.Z);
            var aim = _moveTarget;
            if ((ctrl.Flags & PhysicsController.TouchedTerrain) != 0)
                aim -= n * (n.X * aim.X + n.Y * aim.Y + n.Z * aim.Z + w);
            var dir = aim - foot;
            dir *= GameMath.InverseLength(dir);
            var right = Vector3.Cross(n, dir);
            right *= GameMath.InverseLength(right);
            var up = dir;
            if (_moveTarget.Y - here.Y < 0f)
            {
                up = -up;
                right = -right;
            }
            var facing = new Matrix4x4(right.X, right.Y, right.Z, 1f,
                                       up.X, up.Y, up.Z, 1f,
                                       -n.X, -n.Y, -n.Z, 1f,
                                       0f, 0f, 0f, 1f);
            float t = MathF.Min(_homePower * dt, 1f);
            var from = NormalizedQuat(GameMath.MatrixToQuat_186900(LocalMatrix));
            var to = NormalizedQuat(GameMath.MatrixToQuat_186900(facing));
            SetLocalRotation(GameMath.Slerp_187100(t * dt, from, to));

            speed = ChaseSpeed_20F1C0(0f);
            if (dir.Y < 0f)
            {
                float press = -(MathF.Abs(0.5f - MathF.Abs(0.5f - n.Y)) * 8f + 2f);
                velocity = (dir + n * press) * 0.5f;
            }
            else velocity = dir + -n;
            velocity *= speed;
            _prevVelocity = _velocity;
            _velocity = velocity;
        }

        var move = ClampToRadius(velocity * dt);
        if (MathF.Abs(move.X) <= 5e-5f && MathF.Abs(move.Y) <= 5e-5f && MathF.Abs(move.Z) <= 5e-5f) return;
        SetLocalPosition(LocalMatrix.Translation + move);
    }

    private float ChaseSpeed_20F1C0(float turn)
    {
        float speed = _chaseSpeed;
        if (0f < _damping) speed -= speed * MathF.Min(0.9f, turn * (_damping * 100f));
        _chaseCurrentSpeed = speed;
        return speed;
    }

    private static Quaternion NormalizedQuat(Quaternion q)
    {
        float k = GameMath.QuatInvLength_11C0B8(q, 0f, 1e-10f);
        return new Quaternion(q.X * k, q.Y * k, q.Z * k, q.W * k);
    }
}
