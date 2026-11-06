using System.Numerics;
using CrashEngine.Runtime.Physics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
internal sealed class SplineFollower_121F48
{
    public Vector4 Offset = new(0f, 0f, 0f, 1f);
    public float Gain = 1f;
    public float Turn = 1f;
    public float Slope;
}

// Amedo
public sealed partial class AgentLabAgent
{
    private SplineFollower_121F48? _spline;

    private void SetSpline_1196A8(List<uint> a)
    {
        if (_spline is not { } f) return;
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        f.Offset = new Vector4(FloatValueOf(Arg(0)), FloatValueOf(Arg(1)), FloatValueOf(Arg(2)), 1f);
        f.Gain = FloatValueOf(Arg(3));
        f.Turn = FloatValueOf(Arg(4));
        f.Slope = FloatValueOf(Arg(6));
    }

    private void SplineUpdate_118E48(SplineFollower_121F48 f, float dt)
    {
        if (_paths.Count == 0 || _paths[0] is not { } path || World.Player is not { } player) return;
        path.Closest(player.GamePosition, out var cp);
        float along = path.Closest(GamePosition, out var cm);
        along = Clamp_18C2A0(along, 0f, 1f);
        var dir = path.DirectionAt(along);
        _pathAlong = along;
        _pathDirection = dir;
        bool grounded = _physics is { } ctrl && (ctrl.State & PhysicsController.StateHadContact) != 0;
        var tilt = new Vector4(dir.X, dir.Y - f.Slope, dir.Z, 1f);
        float k = GameMath.InvLength_11C030(2.4999998e-09f, tilt);
        var aim = new Vector3(tilt.X * k * 6f + cm.X, tilt.Y * k * 6f + cm.Y, tilt.Z * k * 6f + cm.Z);
        SteerBodyTowards_22C330(aim, grounded ? f.Turn * dt : f.Turn * (dt * 0.3f), true, 0f);
        if (_physics is { } track) GroundTrack(track, dt);
        var d = cm - cp;
        float dist = MathF.Sqrt(d.Y * d.Y + d.X * d.X + d.Z * d.Z);
        var m = LocalMatrix;
        float speed = _chaseCurrentSpeed + (dist - f.Offset.Z) * f.Gain * dt;
        var velocity = new Vector3(m.M31 * speed, m.M32 * speed, m.M33 * speed);
        _prevVelocity = _velocity;
        if (grounded) _velocity = velocity;
        else
        {
            _velocity.X = velocity.X;
            _velocity.Z = velocity.Z;
        }
        var move = ClampToRadius(velocity * dt);
        if (MathF.Abs(move.X) <= 5e-05f && MathF.Abs(move.Y) <= 5e-05f && MathF.Abs(move.Z) <= 5e-05f) return;
        SetLocalPosition(m.Translation + move);
    }

    private float SplineGap_225338()
    {
        if (Alive(ref _slot118) is not { } t || CurrentPath is not { } path) return -1f;
        float along = Clamp_18C2A0(path.Closest(t.GamePosition, out _), 0f, 1f);
        return MathF.Abs((along - _pathAlong) * path.TotalLength);
    }
}
