using System.Numerics;
using CrashEngine.Runtime.Physics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-29
public sealed partial class AgentLabAgent
{
    private bool _brokenByBody;
    private bool _bodyHitMissing, _bodyOwnerMissing;

    private void InstanceContacts(RigidBody body, CollisionWorld world)
    {
        if (!body.IsBox)
        {
            InstanceContacts_28E780(body, world);
            return;
        }
        BoxContacts_28F3F8(body); // Amedo 2026-10-01
    }

    // Amedo 2026-10-01
    private void BoxContacts_28F3F8(RigidBody body)
    {
        body.Hulls ??= body.BuildHulls?.Invoke();
        if (body.Hulls is not { Count: > 0 } hulls) return;
        var (min, max) = hulls[0].Bounds(body.World);
        for (int i = 1; i < hulls.Count; i++)
        {
            var (hmin, hmax) = hulls[i].Bounds(body.World);
            min = Vector3.Min(min, hmin);
            max = Vector3.Max(max, hmax);
        }
        var hits = World.QueryBodies(min, max, body.CollisionMask, 1, 0, this, requireAll: true, skipLinks: false);
        body.LastContact = null;
        int count = Math.Min(hits.Count, 0x14);
        for (int i = 0; i < count; i++)
        {
            if (hits[i].Owner is DynamicSceneryInstance scenery) // Amedo
            {
                if ((scenery.AgentFlags & 0x10) != 0) BoxVsInstance_28F668(body, scenery, hits[i].Parts, -1);
                continue;
            }
            if (hits[i].Owner is not AgentLabAgent target || ReferenceEquals(target.AttachedTo, this) || ReferenceEquals(target._ref28, this)) continue; // Amedo
            if (target.SteppedBody is { } other)
            {
                BoxPair_2924E8(body, target, other);
                continue;
            }
            if ((target.AgentFlags & 0x10) != 0) // Amedo
            {
                BoxVsInstance_28F668(body, target, hits[i].Parts, target.ControllerType);
                continue;
            }
            if (!(0f < body.Buoyancy) || World.Collision is not { } world) continue;
            if (target.HullCount_1F04B8() == 0) continue;
            var surface = world.SurfaceAt(target.HullSurfaceIndex_1F07D8(0));
            if (((surface.Mask >> 6) & 1) == 0) BoxBuoyancy_28F2A8(body, target.TreeBox.Max.Y);
        }
    }

    // Amedo
    private void BoxVsInstance_28F668(RigidBody body, object target, List<(ConvexHull Hull, Matrix4x4 World)> parts, int controller)
    {
        body.Hulls ??= body.BuildHulls?.Invoke();
        if (body.Hulls is not { Count: > 0 } mine) return;
        bool touched = false;
        var world = World.Collision;
        foreach (var hull in mine)
        {
            for (int j = 0; j < parts.Count; j++)
            {
                var (other, m) = parts[j];
                var (amin, amax) = hull.Bounds(body.World);
                var (bmin, bmax) = other.Bounds(m);
                if (!CollisionWorld.Overlap(amin, amax, bmin, bmax)) continue;
                if (!ConvexHull.HullsContact_282778(hull, body.World, other, m, out var push, out var contact)) continue;
                var normal = push * GameMath.InverseLength(push);
                if (!ReferenceEquals(target, body.IgnoredContact))
                {
                    body.Decelerate(body.Decel, 1f);
                    var local = Vector3.Transform(contact, body.Inverse);
                    var surface = target is AgentLabAgent t && world is not null && t.HullCount_1F04B8() > 0 ? world.SurfaceAt(t.HullSurfaceIndex_1F07D8(j)) : world?.SurfaceAt(0) ?? default;
                    bool respond = target is AgentLabAgent agent ? Response_28C738(body, agent, local, -normal, surface) : SceneryResponse(body, local, -normal, surface);
                    if (respond) body.CorrectInstance_28D330(-push);
                }
                body.Flags |= RigidBody.TouchB;
                if (target is AgentLabAgent a2) body.InstanceContact?.Invoke(a2, j);
                touched = true;
            }
        }
        if (touched && target is AgentLabAgent last) body.LastContact = last;
    }

    // Amedo
    private static bool SceneryResponse(RigidBody body, Vector3 local, Vector3 normal, CollisionWorld.Surface surface)
    {
        body.SurfaceContact_28B7E8(local, normal, surface);
        return true;
    }

    // Amedo 2026-10-01
    private void BoxPair_2924E8(RigidBody box, AgentLabAgent other, RigidBody b)
    {
        if ((b.Flags & RigidBody.Asleep) == 0 && b.Order < box.Order) return;
        if (b.IsBox)
        {
            CollideHullBodies_28FAC8(box, this, b, other); // Amedo
            return;
        }
        if (Body() is { } mine) SphereBox_28E558(b, other, box, this, mine.Parts);
    }

    // Amedo
    private static void CollideHullBodies_28FAC8(RigidBody a, AgentLabAgent ownerA, RigidBody b, AgentLabAgent ownerB)
    {
        a.Hulls ??= a.BuildHulls?.Invoke();
        b.Hulls ??= b.BuildHulls?.Invoke();
        if (a.Hulls is not { Count: > 0 } ha || b.Hulls is not { Count: > 0 } hb) return;
        for (int i = 0; i < ha.Count; i++)
            for (int j = 0; j < hb.Count; j++)
            {
                var (amin, amax) = ha[i].Bounds(a.World);
                var (bmin, bmax) = hb[j].Bounds(b.World);
                if (!CollisionWorld.Overlap(amin, amax, bmin, bmax)) continue;
                if (!ConvexHull.HullsContact_282778(ha[i], a.World, hb[j], b.World, out var push, out var contact)) continue;
                var n = push * GameMath.InverseLength(push);
                a.Decelerate(a.Decel, 1f);
                b.Decelerate(b.Decel, 1f);
                a.CorrectCapped_28D450(-push);
                b.CorrectCapped_28D450(push * 0.50005f);
                BodyResponse_28BE10(a, ownerA, contact, n, b, ownerB);
                a.Flags |= RigidBody.TouchB;
                a.InstanceContact?.Invoke(ownerB, j);
                b.Flags |= RigidBody.TouchB;
                b.InstanceContact?.Invoke(ownerA, i);
            }
    }

    private void InstanceContacts_28E780(RigidBody body, CollisionWorld world)
    {
        body.LastContact = null;
        if (body.CollisionMask == 0) return;
        var p = body.Position;
        var reach = new Vector3(body.Radius);
        var hits = World.QueryBodies(p - reach, p + reach, body.CollisionMask, 1, 0x10, this, requireAll: true, skipLinks: false);
        int count = Math.Min(hits.Count, 0x14);
        for (int i = 0; i < count; i++)
        {
            if (hits[i].Owner is DynamicSceneryInstance scenery) // Amedo 2026-10-06
            {
                ContactsWithScenery_28EBD0(body, scenery, hits[i].Parts, world);
                continue;
            }
            if (hits[i].Owner is not AgentLabAgent target || ReferenceEquals(target.AttachedTo, this)) continue;
            if (target.SteppedBody is { } other)
            {
                BodyPair_292288(body, target, other, hits[i].Parts); // Amedo 2026-10-01
                continue;
            }
            ContactsWithInstance_28EBD0(body, target, hits[i].Parts, world);
        }
    }

    private bool ContactsWithInstance_28EBD0(RigidBody body, AgentLabAgent target, List<(ConvexHull Hull, Matrix4x4 World)> parts, CollisionWorld world)
    {
        bool touched = false;
        for (int i = 0; i < parts.Count; i++)
        {
            var (hull, m) = parts[i];
            if (!body.ContactHull_28E900(hull, m, out var push, out var contact, out var normal)) continue;
            if (!ReferenceEquals(target, body.IgnoredContact))
            {
                body.Decelerate(body.Decel, 1f);
                var surface = target.HullCount_1F04B8() > 0 ? world.SurfaceAt(target.HullSurfaceIndex_1F07D8(i)) : world.SurfaceAt(0); // Amedo
                if (Response_28C738(body, target, contact, normal, surface)) body.CorrectInstance_28D330(push);
            }
            body.Flags |= RigidBody.TouchB;
            body.InstanceContact?.Invoke(target, i);
            touched = true;
        }
        if (touched) body.LastContact = target;
        return touched;
    }

    // Amedo 2026-10-06
    private bool ContactsWithScenery_28EBD0(RigidBody body, DynamicSceneryInstance target, List<(ConvexHull Hull, Matrix4x4 World)> parts, CollisionWorld world)
    {
        bool touched = false;
        for (int i = 0; i < parts.Count; i++)
        {
            var (hull, m) = parts[i];
            if (!body.ContactHull_28E900(hull, m, out var push, out var contact, out var normal)) continue;
            if (!ReferenceEquals(target, body.IgnoredContact))
            {
                body.Decelerate(body.Decel, 1f);
                if (SceneryResponse_28C738(body, target, contact, normal)) body.CorrectInstance_28D330(push);
            }
            body.Flags |= RigidBody.TouchB;
            body.InstanceContact?.Invoke(target, i);
            touched = true;
        }
        if (touched) body.LastContact = target;
        return touched;
    }

    // Amedo 2026-10-06
    private bool SceneryResponse_28C738(RigidBody body, DynamicSceneryInstance target, Vector3 contact, Vector3 normal)
    {
        var (prev, cur, dt) = target.MotionFrames_192360();
        var point = Vector3.Transform(contact, body.World);
        var (surfaceVelocity, surfaceSpin) = SurfaceMotion_28B020(dt, prev, cur, point);
        var n = -normal;
        var local = Vector3.Transform(point, body.Inverse);
        float j = body.ImpulseMagnitude_28AE38(local, surfaceVelocity, n, body.Bounce, body.BounceK);
        var impulse = n * -j;
        BodyOwnerHit_23CD00(target, body.Velocity, -impulse);
        if ((AgentFlags & 0x8000) != 0) BodyHit_23D1E0(null, point, impulse);
        body.Flags2 &= ~RigidBody.ContactThisStep;
        body.RespondMoving_28C358(point, n, surfaceVelocity, surfaceSpin);
        return true;
    }

    private bool Response_28C738(RigidBody body, AgentLabAgent target, Vector3 contact, Vector3 normal, CollisionWorld.Surface surface)
    {
        if (target.HasMotionComponent) return MovingResponse_28C738(body, target, contact, normal); // Amedo 2026-10-01
        float j = body.ImpulseMagnitude(contact, normal, body.Bounce, body.BounceK);
        var impulse = normal * j;
        var reaction = -impulse;
        var point = Vector3.Transform(contact, body.World);
        bool respond = target.BodyHit_23D1E0(this, point, reaction * body.ContactScale);
        BodyOwnerHit_23CD00(target, body.Velocity, reaction);
        if ((AgentFlags & 0x8000) != 0) BodyHit_23D1E0(target, point, impulse);
        body.Flags2 &= ~RigidBody.ContactThisStep;
        if (!respond) return false;
        body.SurfaceContact_28B7E8(contact, normal, surface);
        return true;
    }

    // Amedo 2026-10-01
    private void BodyPair_292288(RigidBody a, AgentLabAgent other, RigidBody b, List<(ConvexHull Hull, Matrix4x4 World)> parts)
    {
        bool asleep = (b.Flags & RigidBody.Asleep) != 0;
        if (b.IsBox)
        {
            if (!asleep && b.Order < a.Order) return;
            SphereBox_28E558(a, this, b, other, parts);
            return;
        }
        if (!asleep && b.Order < a.Order) return;
        if (a.IsEllipsoid || b.IsEllipsoid)
        {
            CollideEllipsoids_28E218(a, other, b); // Amedo
            return;
        }
        SphereSphere_28DF40(a, other, b);
    }

    // Amedo
    private bool CollideEllipsoids_28E218(RigidBody a, AgentLabAgent other, RigidBody b)
    {
        var ea = new Vector3(a.Radius * a.Ellipsoid.X, a.Radius * a.Ellipsoid.Y, a.Radius * a.Ellipsoid.Z);
        var eb = new Vector3(b.Radius * b.Ellipsoid.X, b.Radius * b.Ellipsoid.Y, b.Radius * b.Ellipsoid.Z);
        bool meet = Ellipsoids.Meet_2906D0(a.World, ea, b.World, eb, out var offset, out _);
        Trace($"ellipsoid pair with {other.Entity.Name}: {(meet ? $"meet, offset {offset}" : "apart")}");
        if (!meet) return false;
        var touch = a.Position + offset;
        var push = -(touch - b.Position);
        var back = -(push * 0.50005f);
        a.CorrectCapped_28D450(push);
        b.CorrectCapped_28D450(back);
        var point = (a.Position + b.Position) * 0.5f;
        var n = push * GameMath.InverseLength(push);
        a.Decelerate(a.Decel, 1f);
        b.Decelerate(b.Decel, 1f);
        BodyResponse_28BE10(a, this, point, -n, b, other);
        a.Flags |= RigidBody.TouchB;
        a.InstanceContact?.Invoke(other, 0);
        b.Flags |= RigidBody.TouchB;
        b.InstanceContact?.Invoke(this, 0);
        return true;
    }

    // Amedo 2026-10-01
    private bool SphereSphere_28DF40(RigidBody a, AgentLabAgent other, RigidBody b)
    {
        var pa = a.Position;
        var pb = b.Position;
        var d = pa - pb;
        float depth = a.Radius + b.Radius - GameMath.Length(d);
        if (!(0f < depth)) return false;
        var point = pa + (pb - pa) * a.Radius;
        var n = d * GameMath.InverseLength(d);
        var push = n * depth;
        a.CorrectCapped_28D450(push);
        b.CorrectCapped_28D450(-(push * 0.50005f));
        a.Decelerate(a.Decel, 1f);
        b.Decelerate(b.Decel, 1f);
        BodyResponse_28BE10(a, this, point, -n, b, other);
        a.Flags |= RigidBody.TouchB;
        a.InstanceContact?.Invoke(other, 0);
        b.Flags |= RigidBody.TouchB;
        b.InstanceContact?.Invoke(this, 0);
        return true;
    }

    // Amedo 2026-10-01
    private static bool SphereBox_28E558(RigidBody a, AgentLabAgent ownerA, RigidBody b, AgentLabAgent ownerB, List<(ConvexHull Hull, Matrix4x4 World)> parts)
    {
        bool touched = false;
        for (int i = 0; i < parts.Count; i++)
        {
            if (!a.ContactHull_28E900(parts[i].Hull, b.World, out var push, out var contact, out var normal)) continue;
            a.Decelerate(a.Decel, 1f);
            b.Decelerate(b.Decel, 1f);
            a.CorrectCapped_28D450(push);
            b.CorrectCapped_28D450(-(push * 0.50005f));
            var point = Vector3.Transform(contact, a.World);
            BodyResponse_28BE10(a, ownerA, point, -normal, b, ownerB);
            a.Flags |= RigidBody.TouchB;
            a.InstanceContact?.Invoke(ownerB, i);
            b.Flags |= RigidBody.TouchB;
            b.InstanceContact?.Invoke(ownerA, 0);
            touched = true;
        }
        return touched;
    }

    // Amedo 2026-10-01
    private static void BodyResponse_28BE10(RigidBody a, AgentLabAgent ownerA, Vector3 point, Vector3 n, RigidBody b, AgentLabAgent ownerB)
    {
        float e = a.Bounce * b.Bounce;
        float grip = MathF.Min(b.Grip, a.Grip);
        var la = Vector3.Transform(point, a.Inverse);
        var lb = Vector3.Transform(point, b.Inverse);
        float j = PairImpulse_28AB68(e, a.BounceK, a, la, b, lb, n);
        var impulse = n * -j;
        a.ApplyImpulse_289CC0(impulse, la);
        if ((ownerA.AgentFlags & 0x8000) != 0) ownerA.BodyHit_23D1E0(ownerB, point, impulse);
        if ((ownerB.AgentFlags & 0x8000) != 0) ownerB.BodyHit_23D1E0(ownerA, point, -impulse);
        float limit = j * grip;
        b.ApplyImpulse_289CC0(-impulse, lb);
        var slide = a.PointVelocity(la) - b.PointVelocity(lb);
        slide -= n * GameMath.Dot(slide, n);
        var dir = slide * GameMath.InverseLength(slide);
        var friction = dir * -PairImpulse_28AB68(0f, 0f, a, la, b, lb, dir);
        if (limit < GameMath.Length(friction)) friction = friction * GameMath.InverseLength(friction) * limit;
        a.ApplyImpulse_289CC0(friction, la);
        b.ApplyImpulse_289CC0(-friction, lb);
    }

    // Amedo 2026-10-01
    private static float PairImpulse_28AB68(float e, float k, RigidBody a, in Vector3 la, RigidBody b, in Vector3 lb, in Vector3 n)
    {
        a.UpdateDerived();
        var va = a.PointVelocity(la);
        var vb = b.PointVelocity(lb);
        float vn = GameMath.Dot(vb - va, n);
        if (0f < vn) return 0f;
        if (k != 0f) e *= vn * vn / (vn * vn + k);
        return -(e + 1f) * vn / (a.EffectiveMass(la, n) + b.EffectiveMass(lb, n));
    }

    // Amedo 2026-10-01
    private bool MovingResponse_28C738(RigidBody body, AgentLabAgent target, Vector3 contact, Vector3 normal)
    {
        var (prev, cur, dt) = target.MotionFrames_192360();
        var point = Vector3.Transform(contact, body.World);
        var (surfaceVelocity, surfaceSpin) = SurfaceMotion_28B020(dt, prev, cur, point);
        var n = -normal;
        var local = Vector3.Transform(point, body.Inverse);
        float j = body.ImpulseMagnitude_28AE38(local, surfaceVelocity, n, body.Bounce, body.BounceK);
        var impulse = n * -j;
        var reaction = -impulse;
        bool respond = target.BodyHit_23D1E0(this, point, reaction * body.ContactScale);
        BodyOwnerHit_23CD00(target, body.Velocity, reaction);
        if ((AgentFlags & 0x8000) != 0) BodyHit_23D1E0(target, point, impulse);
        body.Flags2 &= ~RigidBody.ContactThisStep;
        if (!respond) return false;
        body.RespondMoving_28C358(point, n, surfaceVelocity, surfaceSpin);
        return true;
    }

    // Amedo 2026-10-01
    private static (Vector3 Velocity, Vector3 Spin) SurfaceMotion_28B020(float dt, Matrix4x4 prev, Matrix4x4 cur, Vector3 point)
    {
        var onSurface = Vector3.Transform(point, GameMath.RigidInverse_18E808(cur));
        var velocity = (point - Vector3.Transform(onSurface, prev)) * (1f / dt);
        var q = GameMath.MatrixToQuat_186900(cur * GameMath.RigidInverse_18E808(prev));
        int angle = GameMath.AxisAngle_186B50(ref q, out var axis);
        return (velocity, axis * (angle * 9.58738e-05f * (1f / -dt)));
    }

    internal bool BodyHit_23D1E0(AgentLabAgent? sender, Vector3 point, Vector3 force)
    {
        if (sender is not null) HitWhileMoving_23D520(sender, point, force); // Amedo
        float hard = GameMath.LengthSquared(force);
        if (_physics is { Body: not null } && 10f < hard) MakeNoise_209BC8(12f, MathF.Min(hard * 3f, 1f), point);
        return RecordHit_3C(sender, force); // Amedo 2026-09-30
    }

    // Amedo
    private void HitWhileMoving_23D520(AgentLabAgent sender, Vector3 point, Vector3 force)
    {
        if (_contactResponse is not { } args) return;
        if (_wobble is null || !ReferenceEquals(_wobble.Args, args))
        {
            if (((WArg(args, 27) >> 14) & 1) != 0) SetWobble(args);
        }
        else if (_physics is { Body: { } body } && (body.Flags & RigidBody.Asleep) != 0)
        {
            Wake_291308();
            if (1.0000001e-06f < GameMath.LengthSquared(force)) Knock_24BC60(force, point);
        }
        ContactReply_23B5A0(args, sender);
    }

    // Amedo 2026-09-30
    internal bool RecordHit_3C(AgentLabAgent? sender, Vector3 force) => ControllerType switch
    {
        13 => CrateBodyHit_13D220(sender, force),
        12 or 15 => CharacterBodyHit_140C20(sender, force),
        14 or 17 or 18 or 19 or 20 => true, // Amedo
        16 => GenericBodyHit_141150(sender),
        _ => UnknownBodyHit(),
    };

    private bool GenericBodyHit_141150(AgentLabAgent? sender)
    {
        bool respond = ((Motion10 >> 13) & 1) != 0;
        if (respond) TriggerEventSlot(3, sender);
        return respond;
    }

    private bool UnknownBodyHit()
    {
        if (!_bodyHitMissing) World.Unimplemented("physics", ControllerType, "controller vt+0x3C (hit by rigid body)");
        _bodyHitMissing = true;
        return true;
    }

    private bool CrateBodyHit_13D220(AgentLabAgent? sender, Vector3 force)
    {
        float power = force.X * force.X + force.Y * force.Y + force.Z * force.Z;
        var d = force * GameMath.InverseLength(force);
        uint flags = Instance.Source.StateFlags;
        bool respond = !_brokenByBody;
        int slot;
        if (d.Y < -0.707f)
        {
            if (25f < power) return CrateBroken(sender, flags, respond);
            slot = 5f < power ? 5 : 3;
        }
        else if (d.Y <= 0.707f)
        {
            if (25f < power) return CrateBroken(sender, flags, respond);
            slot = 3;
        }
        else
        {
            if (25f < power) return CrateBroken(sender, flags, respond);
            slot = 5f < power ? 4 : 3;
        }
        TriggerEventSlot(slot, sender);
        return respond;
    }

    private bool CrateBroken(AgentLabAgent? sender, uint flags, bool respond)
    {
        TriggerEventSlot(2, sender);
        if ((flags & 0x800) != 0) return respond;
        _brokenByBody = true;
        return false;
    }

    private bool CharacterBodyHit_140C20(AgentLabAgent? sender, Vector3 force)
    {
        float power = force.X * force.X + force.Y * force.Y + force.Z * force.Z;
        if (200f < power)
        {
            TriggerEventSlot(3, sender);
            TriggerEventSlot(2, sender);
        }
        else if (10f < power) TriggerEventSlot(3, sender);
        return true;
    }

    private void BodyOwnerHit_23CD00(object target, Vector3 velocity, Vector3 direction) // Amedo 2026-10-06
    {
        if (OwnCharacter is { } c)
        {
            c.TouchFrom_137858(target, velocity, direction);
            return;
        }
        if (ControllerType is >= 13 and <= 20) return; // Amedo
        if (!_bodyOwnerMissing) World.Unimplemented("physics", ControllerType, "controller vt+0x44 (its rigid body hit something)");
        _bodyOwnerMissing = true;
    }
}
