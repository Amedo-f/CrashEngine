using System.Numerics;
using CrashEngine.Runtime.Physics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-30
public sealed partial class AgentLabAgent
{
    private const uint PairMask_309D78 = 0x5A010;
    private const ulong ClampImpulse = 0x200000;
    private const ulong FixedImpulse = 0x400000;
    private const ulong ForceVolumesState = 0x800000; // Amedo

    private List<object> _pairHits = new(); // Amedo
    private Vector3 _pairPush;
    private float _pairCount;
    private object? _pairStanding; // Amedo 2026-10-06

    private bool InstanceCollision_30 => ControllerType is 13 or 16;

    internal void PairsPass_246D00(float dt)
    {
        if (!InChunk_A0) return; // Amedo 2026-11-17
        if (_physics is not { } ctrl || ctrl.PairType >= 9) return; // Amedo
        if (_destroyed || !(SteppedNow || _attachedTo is not null)) return;
        int type = ctrl.PairType;
        _pairPush = Vector3.Zero;
        _pairCount = 0f;
        var standing = _pairStanding;
        _pairStanding = null;

        var center = _sphereCenter;
        float r = _logicalRadius;
        var qmin = center - new Vector3(r);
        var qmax = center + new Vector3(r);
        if (type == 5) // Amedo
        {
            float midX = (qmin.X + qmax.X) * 0.5f, midZ = (qmin.Z + qmax.Z) * 0.5f, side = r * ctrl.Ratio;
            qmax.X = midX + side;
            qmax.Z = midZ + side;
            qmin.X = midX - side;
            qmin.Z = midZ - side;
        }
        _pairHits = World.QueryBoxObjects_1ECFC0(qmin, qmax, PairMask_309D78, 1, 0x10, true, this, 0x40); // Amedo
        if ((ctrl.State & ForceVolumesState) != 0) ForceVolumes_247930(); // Amedo
        if (type == 6) MoveBy_197048(Repel_2480A8(ctrl, center, r)); // Amedo
        else
        {
            foreach (var hit in _pairHits)
            {
                if (hit is DynamicSceneryInstance d) // Amedo 2026-10-06
                {
                    if (!HullPair_248428(ctrl, d, d.AgentFlags, -1, d.Body.Parts, center, qmin, qmax)) continue;
                    ctrl.Flags |= PhysicsController.TouchedAgent | PhysicsController.TouchedTerrainNow;
                    _pairCount += 1f;
                    if ((AgentFlags & 0x8000) != 0) Touched_22FC98(0f, d);
                    continue;
                }
                var other = (AgentLabAgent)hit;
                if (HasParent_1979E8(other) || other.HasParent_1979E8(this)) continue; // Amedo
                var rigid = other.Component5; // Amedo
                if (rigid is { IsBox: false })
                {
                    if (!ControllerVsBody_247410(ctrl, rigid, other, center, r)) continue;
                    ctrl.Flags |= PhysicsController.TouchedAgent | PhysicsController.TouchedTerrainNow;
                    _pairCount += 1f;
                    continue;
                }
                bool contact;
                if (other.InstanceCollision_30) contact = other.Body() is { } body && HullPair_248428(ctrl, other, other.AgentFlags, other.ControllerType, body.Parts, center, qmin, qmax); // Amedo
                else
                {
                    if (other._physics is not { } theirs || Phase <= other.Phase) continue; // Amedo 2026-10-01
                    contact = SpherePair_249138(ctrl, theirs, other, center, r);
                }
                if (!contact) continue;
                ctrl.Flags |= PhysicsController.TouchedAgent | PhysicsController.TouchedTerrainNow;
                _pairCount += 1f;
                if ((other.AgentFlags & 0x8000) != 0) other.Touched_22FC98(0f, this);
                if (rigid is not null) ControllerVsBox_247AE0(ctrl, rigid, other, center); // Amedo
                else if (ctrl.RideObject_80 is null && _pairStanding is null) PairImpulse_247E10(ctrl, other);
                if ((AgentFlags & 0x8000) != 0) Touched_22FC98(0f, other);
            }
        }
        if ((ctrl.Flags & PhysicsController.TouchedAgent) != 0 && 0f < _pairCount)
            Landing(ctrl, _pairPush / _pairCount, dt);
        if (_pairStanding is { } now && Carries_1C(now)) // Amedo
        {
            KeepRidePosition_253F50(ctrl, LocalMatrix.Translation);
            RideMovement_248918(ctrl, now);
        }
        else if (standing is { } last && Carries_1C(last))
        {
            ulong count = (((ctrl.State >> 15) & 0xF) + 1) & 0xF;
            ctrl.State = (ctrl.State & ~PhysicsController.StateRideCount) | (count << 15);
            if (count == 5) StopRiding_253F30(ctrl);
            else
            {
                KeepRidePosition_253F50(ctrl, LocalMatrix.Translation);
                RideMovement_248918(ctrl, last);
                _pairStanding = last;
            }
        }
    }

    // Amedo
    internal bool MotionCarries_18 { get; private set; } = true;

    private static bool Carries_1C(object support) => support switch
    {
        AgentLabAgent a => a.HasMotionComponent && a.MotionCarries_18,
        DynamicSceneryInstance => true,
        _ => false,
    };

    private static (Matrix4x4 Prev, Matrix4x4 Cur) SupportFrames(object support) => support switch
    {
        AgentLabAgent a => a.MotionFrames_192360() is var f ? (f.Prev, f.Cur) : default,
        DynamicSceneryInstance d => d.MotionFrames_192360() is var g ? (g.Prev, g.Cur) : default,
        _ => (Matrix4x4.Identity, Matrix4x4.Identity),
    };

    private static Matrix4x4 SupportMatrix(object support) => support switch
    {
        AgentLabAgent a => a.LocalMatrix,
        DynamicSceneryInstance d => d.GameMatrix,
        _ => Matrix4x4.Identity,
    };

    // Amedo
    private static void KeepRidePosition_253F50(PhysicsController ctrl, Vector3 position)
    {
        if (ctrl.RideObject_80 is not { } ride) return;
        ctrl.RideLocal_70 = Vector3.Transform(position, GameMath.RigidInverse_18E808(SupportFrames(ride).Cur)); // Amedo
    }

    // Amedo
    private void StopRiding_253F30(PhysicsController ctrl)
    {
        ctrl.RideObject_80 = null;
        _pairStanding = null;
        _keep88 &= ~0x40u;
    }

    // Amedo
    private void ResetContacts_2541B8(PhysicsController ctrl)
    {
        bool touched = (ctrl.Flags & (PhysicsController.TouchedAgent | PhysicsController.TouchedTerrain)) != 0;
        ctrl.State = (ctrl.State & ~(PhysicsController.StateHadContact | PhysicsController.StateFloor | PhysicsController.StateWall)) | (touched ? PhysicsController.StateHadContact : 0UL);
        ctrl.Flags &= ~(PhysicsController.TouchedAgent | PhysicsController.TouchedTerrain | PhysicsController.TouchedTerrainNow);
        StopRiding_253F30(ctrl);
    }

    // Amedo
    private void RideMovement_248918(PhysicsController ctrl, object support)
    {
        var position = LocalMatrix.Translation;
        if (!ReferenceEquals(support, ctrl.RideObject_80))
        {
            _keep88 |= 0x40;
            ctrl.RideObject_80 = support;
            ctrl.RideLocal_70 = Vector3.Transform(position, GameMath.RigidInverse_18E808(SupportMatrix(support))); // Amedo
            Trace($"rides {(support as AgentLabAgent)?.Entity.Name ?? "dynamic scenery"}");
            return;
        }
        var (prev, cur) = SupportFrames(support);
        var delta = Vector3.Transform(ctrl.RideLocal_70, cur) - position;
        int yaw = -GameMath.Asin16_18B7F8((GameMath.RigidInverse_18E808(prev) * cur).M13); // Amedo
        if (!(MathF.Abs(delta.X) <= 5e-5f && MathF.Abs(delta.Y) <= 5e-5f && MathF.Abs(delta.Z) <= 5e-5f)) SetLocalPosition(position + delta);
        if (yaw != 0) SetLocalRotation(Quaternion.Multiply(GameMath.RotationY_18D620(yaw), LocalRotation));
        float k = (int)Math.Round(World.Dt * 9000.0) * (1f / 9000f);
        var next = _prevVelocity + delta * k;
        _prevVelocity = _velocity;
        _velocity = next;
    }

    private bool HullPair_248428(PhysicsController ctrl, object other, uint otherFlags, int otherController, List<(ConvexHull Hull, Matrix4x4 World)> parts, Vector3 center, Vector3 qmin, Vector3 qmax) // Amedo
    {
        bool contact = false;
        float r = qmax.Y - center.Y; // Amedo
        var frame = LocalMatrix;
        frame.Translation += center - frame.Translation; // Amedo
        for (int hull = 0; hull < parts.Count; hull++) // Amedo
        {
            var (shape, m) = parts[hull];
            var (hmin, hmax) = shape.Bounds(m);
            if (!CollisionWorld.Overlap(qmin, qmax, hmin, hmax)) continue; // Amedo
            Vector3 push;
            if (ctrl.PairType == 5) // Amedo
            {
                if (!HullQueries.EllipsoidHull_1FD178(shape, frame, new Vector3(r * ctrl.Ratio, r, r * ctrl.Ratio), m, out push)) continue;
            }
            else
            {
                if (!Matrix4x4.Invert(m, out var inverse)) continue;
                if (!HullQueries.SphereHull_1FC818(r, shape, Vector3.Transform(center, inverse), out var pushedLocal)) continue;
                push = Vector3.TransformNormal(pushedLocal, m);
            }
            contact = true;
            if (ctrl.Has(PhysicsController.Bit55)) continue;
            push *= 0.99f;
            if (!(MathF.Abs(push.X) <= 5e-5f && MathF.Abs(push.Y) <= 5e-5f && MathF.Abs(push.Z) <= 5e-5f))
                SetLocalPosition(LocalMatrix.Translation + push);
            ContactLanding_24A9D8(ctrl, other, otherFlags, push); // Amedo 2026-10-06
            if (HullSurface_1EB3B0(other, hull) is { HurtsAgents: true } surface) SurfaceContact_vt4C(surface, other as AgentLabAgent); // Amedo
        }
        return contact;
    }

    // Amedo
    private CollisionWorld.Surface? HullSurface_1EB3B0(object owner, int hull)
    {
        if (Chunk.Collision is not { } world) return null;
        return owner switch
        {
            AgentLabAgent a => a.HullCount_1F04B8() == 0 ? null : world.SurfaceAt(a.HullSurfaceIndex_1F07D8(hull)),
            DynamicSceneryInstance => world.SurfaceAt(0), // Amedo
            _ => null,
        };
    }

    // Amedo
    private void SurfaceContact_vt4C(CollisionWorld.Surface surface, AgentLabAgent? from)
    {
        if (ControllerType < 0) return;
        Trace($"surface {surface.Id} contact (kind 0x{surface.HitKind:X}, damage {surface.HitAmount}) from {from?.Entity.Name ?? "scenery"}");
        if (ControllerType == 12 && OwnCharacter is { } c) c.Damage(surface.HitSource, surface.HitKind, surface.HitAmount, from ?? (object)this);
        else if (from is not null) ReceiveDamage(surface.HitKind, surface.HitAmount, 0f, from, false);
    }

    private List<uint>? _ownedResponse;

    private List<uint> WritableResponse()
    {
        if (ReferenceEquals(_contactResponse, _ownedResponse) && _ownedResponse is not null) return _ownedResponse;
        var copy = new List<uint>(_contactResponse!);
        while (copy.Count < MotionBlockWords) copy.Add(0u); // Amedo
        if (_wobble is { } w && ReferenceEquals(w.Args, _contactResponse)) w.Args = copy;
        _contactResponse = _ownedResponse = copy;
        return copy;
    }

    private void SetTouchMessage_253498(List<uint> a)
    {
        if (_contactResponse is null)
        {
            _contactResponse = a.Count > 1 ? a.GetRange(1, a.Count - 1) : new List<uint>();
            AgentFlags |= 0x8000;
        }
        uint message = a.Count > 0 ? a[0] : 0u;
        if (message == 0) return;
        var p = WritableResponse();
        p[30] = message;
        p[27] |= 0x10000;
        Trace($"touch message {message}");
    }

    private void ClearTouchMessage_2534F8()
    {
        if (_contactResponse is null) return;
        var p = WritableResponse();
        p[30] = 0;
        p[27] &= ~0x10000u;
    }

    private bool SpherePair_249138(PhysicsController ctrl, PhysicsController theirs, AgentLabAgent other, Vector3 center, float r)
    {
        if (ctrl.PairType == 5 || theirs.PairType == 5) return EllipsoidPair_2498E8(ctrl, theirs, other, center, r); // Amedo 2026-10-05
        var d = center - other._sphereCenter;
        float dist = GameMath.Length(d);
        if (!(0f < dist)) return false;
        float ro = other._logicalRadius;
        float overlap = r + ro - dist;
        if (!(0f < overlap)) return false;
        float k = overlap * 0.5f / dist;
        var half = d * k;
        Vector3 push;
        if (ctrl.Has(PhysicsController.Bit55)) other.MoveBy_197048(push = ClampLength_254080(half * (-overlap / dist), ro));
        else if (theirs.Has(PhysicsController.Bit55)) MoveBy_197048(push = ClampLength_254080(half * (overlap / dist), r));
        else
        {
            push = ClampLength_254080(half * k, MathF.Min(ro, r)); // Amedo 2026-10-05
            MoveBy_197048(push);
            push = -push;
            other.MoveBy_197048(push);
        }
        PairImpact_249138(push, other);
        return true;
    }

    // Amedo 2026-10-05
    private void PairImpact_249138(Vector3 push, AgentLabAgent other)
    {
        float into = (push.X * _velocity.X + push.Y * _velocity.Y + push.Z * _velocity.Z) * 0.3f;
        Impact_23D6A8((into + into) * (push.X * push.X + push.Z * push.Z));
        var mine = HullSurface_1EB3B0(this, 0); // Amedo
        var theirs = HullSurface_1EB3B0(other, 0);
        if (mine is { HurtsAgents: true } m) other.SurfaceContact_vt4C(m, this);
        if (theirs is { HurtsAgents: true } t) SurfaceContact_vt4C(t, other);
    }

    // Amedo 2026-10-05
    private bool EllipsoidPair_2498E8(PhysicsController ctrl, PhysicsController theirs, AgentLabAgent other, Vector3 center, float r)
    {
        var oc = other._sphereCenter;
        float ro = other._logicalRadius;
        if (oc.Y + ro < center.Y - r || center.Y + r < oc.Y - ro) return false;
        float reach = r * ctrl.Ratio + ro * theirs.Ratio;
        float dx = center.X - oc.X, dz = center.Z - oc.Z;
        float dist = MathF.Sqrt(dx * dx + dz * dz);
        float overlap = reach - dist;
        if (!(0f < overlap)) return false;
        float k = dist != 0f ? overlap * 0.5f / dist : 0f;
        var half = new Vector3(dx * k, 0f, dz * k);
        Vector3 push;
        if (ctrl.Has(PhysicsController.Bit55)) other.MoveBy_197048(push = ClampLength_254080(half * (dist != 0f ? -overlap / dist : 0f), ro));
        else if (theirs.Has(PhysicsController.Bit55)) MoveBy_197048(push = ClampLength_254080(half * (dist != 0f ? overlap / dist : 0f), r));
        else
        {
            push = ClampLength_254080(half * k, MathF.Min(ro, r));
            MoveBy_197048(push);
            push = -push;
            other.MoveBy_197048(push);
        }
        PairImpact_249138(push, other);
        return true;
    }

    private static Vector3 ClampLength_254080(Vector3 v, float max)
    {
        float len = GameMath.Ps2Sqrt(GameMath.Ps2Add(GameMath.Ps2Add(GameMath.Ps2Mul(v.X, v.X), GameMath.Ps2Mul(v.Y, v.Y)), GameMath.Ps2Mul(v.Z, v.Z))); // Amedo 2026-11-21
        if (!(max < len)) return v;
        float k = GameMath.Ps2Div(max, len);
        return new Vector3(GameMath.Ps2Mul(v.X, k), GameMath.Ps2Mul(v.Y, k), GameMath.Ps2Mul(v.Z, k));
    }

    private void MoveBy_197048(Vector3 push)
    {
        if (MathF.Abs(push.X) <= 5e-5f && MathF.Abs(push.Y) <= 5e-5f && MathF.Abs(push.Z) <= 5e-5f) return;
        SetLocalPosition(LocalMatrix.Translation + push);
    }

    private bool ContactLanding_24A9D8(PhysicsController ctrl, object other, uint otherFlags, Vector3 push) // Amedo 2026-10-06
    {
        bool airborne = ctrl.Has(PhysicsController.Airborne);
        if (!airborne && 0f <= GameMath.Dot(_velocity, push)) return true;
        var n = push * GameMath.InverseLength(push);
        if (ctrl.Has(PhysicsController.Gripped) && ctrl.GripNow == 0f) ctrl.GripNow = 0.8f;
        if (0.7f < n.Y)
        {
            ctrl.State |= PhysicsController.StateFloor;
            if ((otherFlags & 0x4000) != 0 && (other is DynamicSceneryInstance || other is AgentLabAgent { HasMotionComponent: true })) _pairStanding = other; // Amedo
        }
        else if (MathF.Abs(n.Y) < 0.3f) ctrl.State |= PhysicsController.StateWall;
        if (ctrl.Has(PhysicsController.MaxTiltSet) && n.Y < ctrl.MaxTilt)
        {
            _velocity.Y = 0f;
            return false;
        }
        if (airborne || (ctrl.State & PhysicsController.StateSmoothNormal) != 0)
        {
            if (-0.1f < GameMath.Dot(n, ctrl.GroundNormal))
            {
                _pairPush += n;
                _pairCount += 1f;
            }
            ctrl.AirTime = 0f;
        }
        return true;
    }

    private void PairImpulse_247E10(PhysicsController ctrl, AgentLabAgent other)
    {
        if (other.ControllerType < 0) return;
        var mine = _prevVelocity; // Amedo
        var theirs = other.OwnCharacter is { } oc ? oc.Velocity : other._velocity;
        var impulse = LimitedImpulse(ctrl, (mine - theirs) * FloatVar(0)); // Amedo
        if (!(0.001f < GameMath.LengthSquared(impulse))) return;
        Trace($"pair impulse {impulse} -> {other.Entity.Name}");
        other.RecordHit_3C(this, impulse);
        RecordHit_3C(other, -impulse);
    }

    // Amedo
    private static Vector3 LimitedImpulse(PhysicsController ctrl, Vector3 impulse)
    {
        float limit = ctrl.ParamBC;
        if ((ctrl.State & ClampImpulse) != 0)
        {
            if (limit < GameMath.Length(impulse)) impulse = impulse * GameMath.InverseLength(impulse) * limit;
        }
        else if ((ctrl.State & FixedImpulse) != 0) impulse = impulse * GameMath.InverseLength(impulse) * limit;
        else impulse *= limit;
        return impulse;
    }

    // Amedo
    private bool ControllerVsBody_247410(PhysicsController ctrl, RigidBody body, AgentLabAgent owner, Vector3 center, float r)
    {
        var d = body.Position - center;
        float reach = r + body.Radius;
        float dist = MathF.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
        if (!(dist < reach)) return false;
        float m = FloatVar(0);
        float inv = 1f / dist;
        float k = m / (m + body.Mass);
        var n = d * inv;
        body.SetInstancePosition_291B60(body.Position + n * ((reach - dist) * k));
        var rel = _prevVelocity - body.Velocity;
        var relk = rel * k;
        bool approaching = 0f < _prevVelocity.X * relk.X + _prevVelocity.Y * relk.Y + _prevVelocity.Z * relk.Z;
        if (approaching) body.ApplyImpulse_289CC0(relk, Vector3.Transform(center, GameMath.RigidInverse_18E808(body.World)));
        var impulse = LimitedImpulse(ctrl, relk);
        if (0.01f < impulse.X * impulse.X + impulse.Y * impulse.Y + impulse.Z * impulse.Z)
        {
            impulse = -impulse;
            Trace($"rigid body {owner.Entity.Name} pushes back {impulse}");
            BodyHit_23D1E0(owner, center, impulse);
            RecordHit_3C(owner, impulse);
        }
        if (approaching) _velocity += rel * -(1f - k);
        return true;
    }

    // Amedo
    private void ControllerVsBox_247AE0(PhysicsController ctrl, RigidBody body, AgentLabAgent owner, Vector3 center)
    {
        float m = FloatVar(0);
        float k = m / (m + body.Mass);
        var rel = _prevVelocity - body.Velocity;
        var relk = rel * k;
        body.ApplyImpulse_289CC0(relk, Vector3.Transform(center, GameMath.RigidInverse_18E808(body.World)));
        var impulse = LimitedImpulse(ctrl, relk);
        if (0.001f < impulse.X * impulse.X + impulse.Y * impulse.Y + impulse.Z * impulse.Z)
        {
            Trace($"box body {owner.Entity.Name} pushes back {-impulse}");
            RecordHit_3C(owner, -impulse);
        }
        _velocity += rel * -(1f - k);
    }

    // Amedo
    private Vector3 Repel_2480A8(PhysicsController ctrl, Vector3 center, float r)
    {
        var c = center;
        float r2 = r * r;
        void From(Vector3 min, Vector3 max)
        {
            var d = (min + max) * 0.5f - c;
            var ext = max - min;
            float e = ext.X;
            if (e < ext.Y) e = ext.Y;
            if (e < ext.Z) e = ext.Z;
            float q = MathF.Max(d.X * d.X + d.Y * d.Y + d.Z * d.Z - e * e, 0.01f);
            float s = ctrl.Grip * r2 * (1f / q);
            if (r < s) s = r;
            c += d * GameMath.InverseLength(d) * s;
        }
        foreach (var hit in _pairHits) // Amedo
        {
            var cell = hit is AgentLabAgent a ? (InstanceCell)a.Cell : ((DynamicSceneryInstance)hit).Cell;
            From(cell.BoxMin_40, cell.BoxMax_50);
        }
        return center - c;
    }

    // Amedo
    private void ForceVolumes_247930()
    {
        if (_physics is not { } ctrl) return;
        var (min, max) = CommittedBox; // Amedo
        var position = GameMatrix.Translation;
        foreach (var t in World.TriggersIn_1ECFC0(min, max, Chunk))
        {
            if (!t.ForceOn) continue;
            if (ctrl.Body is not null) ActivateResponse_23D648();
            ApplyForce_24BD78(ctrl, Vector3.TransformNormal(new Vector3(t.Force.X, t.Force.Y, t.Force.Z), t.Matrix), position);
        }
    }

    // Amedo
    private void SetPhysicsSizes_253380(float value98, float magnet, uint direction, uint strength)
    {
        var ctrl = RigidBody_23DE78();
        ctrl.Magnet_9C = magnet;
        ctrl.State = (ctrl.State & ~0xC0UL) | ((ulong)(strength & 3) << 6);
        ctrl.State = (ctrl.State & ~0x300UL) | ((ulong)(direction & 3) << 8);
    }

    // Amedo
    private Vector3 MagnetForce_24BF00(PhysicsController ctrl, Vector3 point)
    {
        var p = GamePosition;
        var d = Vector3.Zero;
        switch ((ctrl.State >> 8) & 3)
        {
            case 0: d = point - p; break;
            case 1: d = new Vector3(GameMatrix.M31, GameMatrix.M32, GameMatrix.M33); break;
        }
        d *= GameMath.InverseLength(d);
        ulong strength = (ctrl.State >> 6) & 3;
        return strength is 1 or 2 ? Vector3.Zero : d * ctrl.Magnet_9C;
    }

    // Amedo
    private void MagnetPullToFocus_245340()
    {
        if (FocusObject is not { } focus) return;
        var me = GamePosition;
        var force = focus.MagnetForce_24BF00(focus.RigidBody_23DE78(), me) * StepDt;
        ActivateResponse_23D648();
        if (_physics is not { } ctrl) return;
        ctrl.Flags |= PhysicsController.Launched | PhysicsController.Bit57;
        ctrl.State &= ~PhysicsController.StateCounter;
        var point = _sphereCenter;
        AgentLabAgent? holder = null;
        PhysicsController? held = null;
        if ((focus.AgentFlags & 0x40) != 0 && focus._attachedTo is { } h && h._physics is { } hc)
        {
            holder = h;
            held = hc;
            point = h._sphereCenter;
        }
        if (holder is null)
        {
            ApplyForce_24BD78(ctrl, force, point);
            return;
        }
        point = (point + me) * 0.5f;
        force *= 0.5f;
        ApplyForce_24BD78(ctrl, force, point);
        holder.ApplyForce_24BD78(held!, -force, point);
    }

    // Amedo
    private void ApplyForce_24BD78(PhysicsController ctrl, Vector3 force, Vector3 point)
    {
        switch (ctrl.PairType)
        {
            case >= 1 and <= 5:
            case 8:
                _velocity += force * StepDt;
                break;
            case >= 9 and <= 11:
                if (ctrl.Body is { } body && Matrix4x4.Invert(GameMatrix, out var inverse)) body.AddForce_2918B0(force, Vector3.Transform(point, inverse));
                break;
        }
    }
}
