using System.Numerics;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed partial class AgentLabAgent
{
    private const int PacketDuration = 11;
    private const int PacketGravity = 16;
    private const int PacketBounce = 20;

    private bool _projectile;
    private bool _projectileKeepsPath;
    private bool _projectileTurn; // Amedo
    private Vector3 _projectileVelocity;
    private float _projectileGravity;
    private float _projectileDuration;

    private void StartProjectile(TwinBehaviourControlPacket packet)
    {
        float g = PacketFloat(packet, PacketGravity, out float gravity) ? gravity : 40f;
        var delta = _moveTarget - _moveStart;
        float dy = delta.Y;
        float dist = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
        float vy;
        float time = PacketFloat(packet, PacketDuration, out float duration) ? duration : 0f;
        if (time != 0f) vy = (dy + g * (time * 0.5f) * time) / time;
        else
        {
            float apex;
            if (PacketFloat(packet, PacketBounce, out float bounce) && 0f < bounce)
            {
                vy = -_velocity.Y * bounce; // Amedo
                apex = vy * vy / (g + g);
            }
            else
            {
                PacketFloat(packet, PacketMoveSpeed, out apex);
                vy = MathF.Sqrt(MathF.Abs((g + g) * apex));
            }
            time = (MathF.Sqrt(MathF.Abs((g + g) * (apex - dy))) + vy) / g;
        }

        _projectile          = true;
        _projectileKeepsPath = ((int)packet.ContinuousRotate & 1) != 0;
        _projectileGravity   = g;
        _projectileDuration  = time;
        _moveRate            = 1f / time; // Amedo
        if (!(time > 0f))
        {
            _translationDone = true;
            MoveInstance_23C280(_moveTarget);
            return;
        }
        float speed = dist * (1f / time);
        float lenSq = delta.X * delta.X + delta.Z * delta.Z;
        float inv = lenSq > 2.4999998e-09f ? 1f / MathF.Sqrt(lenSq) : 0f;
        _projectileVelocity = new Vector3(delta.X * inv * speed, vy, delta.Z * inv * speed);
        _velocity = _projectileVelocity; // Amedo
    }

    private void UpdateProjectile(float dt)
    {
        var v = _projectileVelocity;
        var p = GamePosition;
        p = new Vector3(p.X + v.X * dt, (p.Y + v.Y * dt) - _projectileGravity * (dt * dt * 0.5f), p.Z + v.Z * dt);
        _projectileVelocity.Y = v.Y - _projectileGravity * dt;
        _prevVelocity = _velocity; // Amedo
        _velocity = _projectileVelocity;

        if (_sqrTolerance != 0f)
        {
            MoveInstance_23C280(p);
            if (Vector3.DistanceSquared(p, _moveTarget) < _sqrTolerance) _translationDone = true;
            return;
        }
        if (_projectileDuration <= (float)(World.Now - _packetStart))
        {
            MoveInstance_23C280(_projectileKeepsPath ? p : _moveTarget);
            if (ControlPacket is { InterpolatesAngles: true } rp && ((int)rp.ContinuousRotate & 2) == 0 && _projectileTurn)
                TurnInstance_23C468(_rotTarget); // Amedo
            _translationDone = true;
            return;
        }
        MoveInstance_23C280(p);
    }
}
