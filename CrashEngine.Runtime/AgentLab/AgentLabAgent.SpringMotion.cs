using System.Numerics;
using CrashEngine.Runtime.Physics;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabAgent
{
    private const int PacketPower = 16;
    private const float SpringRest = 0.08f;

    private bool _springing;
    private float _springK, _springDamping;
    private Vector3 _springForce;
    private bool _springNoOvershoot;

    private void StartSpring_20CDD4(TwinBehaviourControlPacket packet, Vector3 target)
    {
        PacketFloat(packet, PacketPower, out float k);
        PacketFloat(packet, PacketDamping, out float damping);
        PacketFloat(packet, PacketDecDist, out float dec);
        _moveTarget = target;
        _springK = k;
        _springDamping = damping;
        _springForce = (target - _moveStart) * k;
        _springNoOvershoot = dec < 0f;
        _springing = true;
        Trace($"spring motion to {target} (k {k}, damping {damping}{(_springNoOvershoot ? ", no overshoot" : "")})");
    }

    private bool SpringFlat => _gravity88; // Amedo

    private void StepSpringPhysics_20B920(Vector3 force, float dt)
    {
        _springForce = force;
        _prevVelocity = _velocity;
        float damping = _springDamping;
        if (0f <= damping)
        {
            _springForce = force - _velocity * damping;
            _velocity += _springForce * dt;
            return;
        }
        _velocity += force * dt;
        if (damping * damping < GameMath.LengthSquared(_velocity))
            _velocity *= GameMath.InverseLength(_velocity) * -damping;
    }

    private void StepSpring_232260(float dt)
    {
        var pos = GamePosition;
        var d = _moveTarget - pos;
        if (SpringFlat) d.Y = 0f;
        StepSpringPhysics_20B920(d * _springK, dt);
        var step = _velocity * dt;
        var next = pos + step;
        SetTrajectoryRest50(next); // Amedo
        if (_sqrTolerance != 0f && GameMath.DistanceSq(next, _moveTarget) < _sqrTolerance)
        {
            MoveInstance_23C280(next);
            _translationDone = true;
            return;
        }
        if (_springNoOvershoot)
        {
            if (MathF.Abs(_moveTarget.X - pos.X) < MathF.Abs(step.X)) next.X = _moveTarget.X;
            if (MathF.Abs(_moveTarget.Y - pos.Y) < MathF.Abs(step.Y)) next.Y = _moveTarget.Y;
            if (MathF.Abs(_moveTarget.Z - pos.Z) < MathF.Abs(step.Z)) next.Z = _moveTarget.Z;
            MoveInstance_23C280(next);
            if (next == _moveTarget) _translationDone = true;
            return;
        }
        if (GameMath.LengthSquared(_velocity) + GameMath.LengthSquared(_springForce) < SpringRest)
        {
            MoveInstance_23C280(_moveTarget);
            _translationDone = true;
            return;
        }
        MoveInstance_23C280(next);
    }
}
