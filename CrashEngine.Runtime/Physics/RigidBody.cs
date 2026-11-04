using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Physics;

// Amedo 2026-09-27
public sealed class RigidBody
{
    public const uint MomentumDirty = 0x1;
    public const uint OrientationDirty = 0x2;
    public const uint TouchA = 0x4;
    public const uint Asleep = 0x8;
    public const uint TouchB = 0x10;

    public const ulong SyncTransform = 0x1;
    public const ulong NoTerrain = 0x2;
    public const ulong ContactThisStep = 0x4;
    public const ulong HardImpact = 0x8;
    public const ulong LandedHardStep = 0x10; // Amedo
    public const ulong ScrapedStep = 0x20; // Amedo
    public const ulong PositionOnly = 0x40;

    public uint Flags = 3;
    private static int _nextOrder; // Amedo 2026-10-01
    public readonly int Order = Interlocked.Increment(ref _nextOrder); // Amedo 2026-10-01
    public float Mass = 1f;
    public float InvMass = 1f;
    public float MaxSpeed = 1e30f;
    public float MaxSpin = 1e30f;
    public Vector3 Inertia;
    public Vector3 InvInertia;
    public Vector3 InvInertiaRest;
    public int Substeps = 1;
    public Vector3 Momentum;
    public Quaternion Orientation = Quaternion.Identity;
    public Vector3 AngularMomentum;
    public Matrix4x4 World = Matrix4x4.Identity;
    public Matrix4x4 Inverse = Matrix4x4.Identity;
    public Quaternion OrientationRate;
    public Vector3 Spin;
    public Vector3 Velocity;
    public Vector3 PrevVelocity;
    public Matrix4x4 InvInertiaWorld = Matrix4x4.Identity;
    public Vector3 ImpulseTrail;
    public Vector3 Force;
    public Vector3 Torque;
    public Vector3 Force2;
    public Vector3 Torque2;
    public float ImpulseSum;
    public ulong Flags2 = 0x5;
    public Vector3 ComOffset;
    public TriangleCache? Terrain;
    public byte IdleFrames;
    public float Bounce = 0.3f;
    public float BounceK = 4f;
    public float Grip = 0.5f;
    public float Twist = 0.01f;
    public float Roll = 0.01f;
    public float Damping = 0.001f;
    public float Decel = 0.001f;
    public float Buoyancy = -1f;
    public uint CollisionMask;
    public Vector3 Correction;
    public float CorrectionSum;
    public float Radius = 0.5f;
    public Vector3 Ellipsoid = Vector3.One;
    public bool IsEllipsoid;
    public bool IsBox; // Amedo 2026-09-27
    public List<ConvexHull>? Hulls;
    public RigidConstraint? Constraint; // Amedo 2026-09-29
    public Func<List<ConvexHull>?>? BuildHulls;
    public Action<CollisionWorld.Triangle>? Contact; // Amedo 2026-09-29
    public Action<CollisionWorld.Surface>? SurfaceEvent; // Amedo 2026-09-30
    public Func<CollisionWorld.Surface, Vector3, Vector3, bool>? OnLanded, OnLandedHard, OnScraped; // Amedo
    public float ContactScale = 1f;
    public Vector3 InstanceCorrection;
    public object? LastContact;
    public object? IgnoredContact;
    public Action<object, int>? InstanceContact;
    public float WaterLevel = BitConverter.Int32BitsToSingle(unchecked((int)0xF149F2CA)); // Amedo

    public Vector3 Position
    {
        get => new(World.M41, World.M42, World.M43);
        set { World.M41 = value.X; World.M42 = value.Y; World.M43 = value.Z; }
    }

    public bool Touching => (Flags & (TouchA | TouchB)) != 0;

    public RigidBody()
    {
        SetBoxInertia(1f, 1.5f, 1.5f, 1.5f); // Amedo
    }

    public void SetBoxInertia(float mass, float ex, float ey, float ez)
    {
        const float third = 0.33333334f;
        float a = ey * third, b = ez * third, c = ex * third;
        float hi = a < b ? b : a;
        if (hi < ex) hi = ex;
        float mid = c < b ? b : c;
        if (mid < ey) mid = ey;
        float lo = c < a ? a : c;
        if (lo < ez) lo = ez;
        lo *= lo; mid *= mid; hi *= hi;
        Mass = mass;
        InvMass = 1f / mass;
        float k = mass * 0.083333336f;
        Inertia = new Vector3(k * (mid + lo), k * (hi + lo), k * (hi + mid));
        InvInertia = new Vector3(1f / Inertia.X, 1f / Inertia.Y, 1f / Inertia.Z);
        InvInertiaRest = InvInertia;
        UpdateDerived();
    }

    public void InitFromTransform(Matrix4x4 scene)
    {
        var com = Vector3.TransformNormal(ComOffset, scene) + scene.Translation;
        var rotation = Matrix4x4.Transpose(new Matrix4x4(scene.M11, scene.M12, scene.M13, 0f,
                                                          scene.M21, scene.M22, scene.M23, 0f,
                                                          scene.M31, scene.M32, scene.M33, 0f,
                                                          0f, 0f, 0f, 1f));
        Orientation = Quaternion.CreateFromRotationMatrix(rotation);
        World.M41 = com.X; World.M42 = com.Y; World.M43 = com.Z;
        Flags |= MomentumDirty | OrientationDirty;
    }

    public void UpdateDerived()
    {
        if ((Flags & MomentumDirty) != 0)
        {
            Velocity = Momentum * InvMass;
            if (MaxSpeed * MaxSpeed < GameMath.LengthSquared(Velocity))
            {
                Velocity = Velocity * GameMath.InverseLength(Velocity) * MaxSpeed;
                Momentum = Velocity * Mass;
            }
            Flags &= ~MomentumDirty;
        }
        if ((Flags & OrientationDirty) == 0) return;
        Constraint?.Apply_288F58(this); // Amedo 2026-09-29
        var position = Position;
        var r = Matrix4x4.Transpose(Matrix4x4.CreateFromQuaternion(Orientation));
        World = r;
        World.M41 = position.X; World.M42 = position.Y; World.M43 = position.Z;
        Matrix4x4.Invert(World, out Inverse);
        var scaled = Inverse;
        scaled.M11 *= InvInertia.X; scaled.M12 *= InvInertia.Y; scaled.M13 *= InvInertia.Z;
        scaled.M21 *= InvInertia.X; scaled.M22 *= InvInertia.Y; scaled.M23 *= InvInertia.Z;
        scaled.M31 *= InvInertia.X; scaled.M32 *= InvInertia.Y; scaled.M33 *= InvInertia.Z;
        scaled.M41 = 0f; scaled.M42 = 0f; scaled.M43 = 0f;
        var rot = World;
        rot.M41 = 0f; rot.M42 = 0f; rot.M43 = 0f;
        InvInertiaWorld = scaled * rot;
        Spin = Vector3.TransformNormal(AngularMomentum, InvInertiaWorld);
        if (MaxSpin * MaxSpin < GameMath.LengthSquared(Spin))
        {
            Spin = Spin * GameMath.InverseLength(Spin) * MaxSpin;
            AngularMomentum = Vector3.TransformNormal(Spin, Matrix4x4.Transpose(InvInertiaWorld));
        }
        var q = Orientation;
        var w = Spin;
        OrientationRate = new Quaternion(
            0.5f * (q.W * w.X - q.Z * w.Y + q.Y * w.Z),
            0.5f * (q.Z * w.X + q.W * w.Y - q.X * w.Z),
            0.5f * (-q.Y * w.X + q.X * w.Y + q.W * w.Z),
            0.5f * (-q.X * w.X - q.Y * w.Y - q.Z * w.Z));
        Flags &= ~OrientationDirty;
    }

    public void Substep(float h)
    {
        UpdateDerived();
        var step = Velocity * h;
        float len = GameMath.Length(step);
        if (0.5f < len) step = step * GameMath.InverseLength(step) * 0.5f;
        Position += step;
        Momentum += Force * h;
        AngularMomentum += Torque * h;
        Momentum += Force2 * h;
        AngularMomentum += Torque2 * h;
        var q = new Quaternion(Orientation.X + OrientationRate.X * h, Orientation.Y + OrientationRate.Y * h,
                               Orientation.Z + OrientationRate.Z * h, Orientation.W + OrientationRate.W * h);
        float qLenSq = q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W;
        float inv = qLenSq > 1e-10f ? 1f / MathF.Sqrt(qLenSq) : 0f;
        Orientation = new Quaternion(q.X * inv, q.Y * inv, q.Z * inv, q.W * inv);
        Flags |= MomentumDirty | OrientationDirty;
        InvInertia = InvInertiaRest;
        UpdateDerived();
        Force2 = Vector3.Zero;
        Torque2 = Vector3.Zero;
    }

    public void Decelerate(float a, float dt)
    {
        float drop = a * dt;
        float len = GameMath.Length(Momentum);
        if (!(MathF.Abs(len) <= 5e-5f))
        {
            float k = MathF.Max(0f, len - drop) / len;
            if (k < 1f) Momentum *= k;
        }
        len = GameMath.Length(AngularMomentum);
        if (MathF.Abs(len) <= 5e-5f) return;
        float kk = MathF.Max(0f, len - drop) / len;
        if (kk < 1f) AngularMomentum *= kk;
        Flags |= MomentumDirty;
    }

    public void Integrate(float dt, bool sphere)
    {
        Decelerate(Decel, dt);
        float keep;
        if (Buoyancy < 0f) keep = 1f - Damping * dt * 60f;
        else
        {
            if (!(0.01f < GameMath.LengthSquared(Force2))) return;
            keep = 1f - Damping * (sphere ? 4f : 70f) * dt * 60f;
        }
        if (keep < 0f) keep = 0f;
        Momentum *= keep;
        AngularMomentum *= keep;
        Flags |= MomentumDirty;
    }

    public Vector3 PointVelocity(Vector3 local)
    {
        UpdateDerived();
        var r = Vector3.TransformNormal(local, World);
        return Velocity + Vector3.Cross(r, Spin);
    }

    // Amedo 2026-09-30
    public Vector3 PointVelocityAt_289408(Vector3 point)
    {
        UpdateDerived();
        return Velocity + Vector3.Cross(point - Position, Spin);
    }

    public float EffectiveMass(Vector3 local, Vector3 n)
    {
        var nb = Vector3.TransformNormal(n, Inverse);
        var t = Vector3.TransformNormal(Vector3.Cross(nb, local), World);
        var u = Vector3.TransformNormal(t, InvInertiaWorld);
        var r = Vector3.TransformNormal(local, World);
        return GameMath.Dot(n, Vector3.Cross(r, u)) + InvMass;
    }

    public float ImpulseMagnitude(Vector3 local, Vector3 n, float e, float k)
    {
        UpdateDerived();
        UpdateDerived();
        float vn = GameMath.Dot(PointVelocity(local), n);
        if (0f < vn) return 0f;
        if (k != 0f) e *= vn * vn / (vn * vn + k);
        return -(e + 1f) * vn / EffectiveMass(local, n);
    }

    public void ApplyImpulse_289CC0(Vector3 j, Vector3 local) // Amedo
    {
        UpdateDerived();
        Momentum += j;
        Flags |= MomentumDirty;
        var jb = Vector3.TransformNormal(j, Inverse);
        var dl = Vector3.TransformNormal(Vector3.Cross(local, jb), World);
        ImpulseTrail = ImpulseTrail * 0.7f + dl * 0.3f;
        AngularMomentum -= dl;
        Flags |= OrientationDirty;
        ImpulseSum += GameMath.Length(j);
    }

    // Amedo
    public void SetInstancePosition_291B60(Vector3 position)
    {
        Position = position + ComOffset;
        Matrix4x4.Invert(World, out Inverse);
    }

    // Amedo
    public void SetPosition_2914B0(Vector3 position)
    {
        Position = position;
        Matrix4x4.Invert(World, out Inverse);
    }

    // Amedo
    public void SetCenterOfMass_28B6F0(Vector3 com)
    {
        var d = Vector3.TransformNormal(com - ComOffset, World);
        Position += d;
        Matrix4x4.Invert(World, out Inverse);
        ComOffset = com;
        if (IsBox && BuildHulls is { } build) Hulls = build();
    }

    // Amedo
    public void Spring_28A198(in Vector3 target, in Vector3 local, bool pullOnly, float length, float stiffness, float damping)
    {
        var d = target - Vector3.Transform(local, World);
        float stretch = MathF.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z) - length;
        if (pullOnly && stretch < 0f) return;
        d *= GameMath.InverseLength(d);
        UpdateDerived();
        var v = Velocity + Vector3.Cross(Vector3.TransformNormal(local, World), Spin);
        float f = stretch * stiffness - (v.X * d.X + v.Y * d.Y + v.Z * d.Z) * damping;
        AddForce_2918B0(d * f, local);
    }

    // Amedo
    public void LevelSpring_28A4F0(in Vector3 target, in Vector3 local, float stiffness, float damping, float sideways)
    {
        var p = Vector3.Transform(local, World);
        var h = new Vector3(target.X - p.X, 0f, target.Z - p.Z);
        float rise = target.Y - p.Y;
        float across = MathF.Sqrt(h.X * h.X + h.Z * h.Z);
        h *= GameMath.InverseLength(h);
        UpdateDerived();
        var v = Velocity + Vector3.Cross(Vector3.TransformNormal(local, World), Spin);
        float up = rise * stiffness - v.Y * damping;
        float side = across * (stiffness * sideways) - (v.X * h.X + v.Y * h.Y + v.Z * h.Z) * damping * sideways;
        AddForce_2918B0(new Vector3(h.X * side, up + h.Y * side, h.Z * side), local);
    }

    // Amedo 2026-09-29
    public void AddForce_2918B0(Vector3 force, Vector3 local)
    {
        var fb = Vector3.TransformNormal(force, Inverse);
        UpdateDerived();
        Force += Vector3.TransformNormal(fb, World);
        Torque += Vector3.TransformNormal(Vector3.Cross(fb, local), World);
    }

    // Amedo 2026-09-29
    public void AddForceAtPoint_289F08(Vector3 force, Vector3 point)
    {
        var fb = Vector3.TransformNormal(force, Inverse);
        var pb = Vector3.Transform(point, Inverse);
        UpdateDerived();
        Force += Vector3.TransformNormal(fb, World);
        Torque += Vector3.TransformNormal(Vector3.Cross(fb, pb), World);
    }

    public void ApplyForceAt(Vector3 force, Vector3 point)
    {
        var fb = Vector3.TransformNormal(force, Inverse);
        var pb = Vector3.Transform(point, Inverse);
        UpdateDerived();
        Force2 += Vector3.TransformNormal(fb, World);
        Torque2 += Vector3.TransformNormal(Vector3.Cross(fb, pb), World);
    }

    public Matrix4x4 WorldInertia()
    {
        var m = Inverse;
        m.M11 *= Inertia.X; m.M12 *= Inertia.X; m.M13 *= Inertia.X;
        m.M21 *= Inertia.Y; m.M22 *= Inertia.Y; m.M23 *= Inertia.Y;
        m.M31 *= Inertia.Z; m.M32 *= Inertia.Z; m.M33 *= Inertia.Z;
        var rot = World;
        rot.M41 = 0f; rot.M42 = 0f; rot.M43 = 0f;
        m.M41 = 0f; m.M42 = 0f; m.M43 = 0f;
        return m * rot;
    }

    public void SpinFriction(Vector3 j, Vector3 targetSpin, float twist, float roll)
    {
        var inertia = WorldInertia();
        var dl = Vector3.TransformNormal(targetSpin - Spin, inertia);
        var n = j * GameMath.InverseLength(j);
        var along = n * GameMath.Dot(n, dl);
        float magnitude = GameMath.Length(j);
        float limit = magnitude * twist;
        var a = along;
        if (limit < GameMath.Length(a)) a = a * GameMath.InverseLength(a) * limit;
        limit = magnitude * roll;
        var b = dl - along;
        if (limit < GameMath.Length(b)) b = b * GameMath.InverseLength(b) * limit;
        AngularMomentum += a;
        AngularMomentum += b;
    }

    public void Correct(Vector3 push)
    {
        var half = push * 0.5f;
        Correction += half;
        CorrectionSum += GameMath.Length(half);
        Position += half;
        Matrix4x4.Invert(World, out Inverse);
    }

    public bool TerrainContacts(CollisionWorld world, Action<CollisionWorld.Triangle, Vector3>? water, out bool wet)
    {
        wet = false;
        if ((Flags2 & NoTerrain) != 0) return false;
        if (IsBox) return BoxTerrainContacts(world, water, out wet);
        Terrain ??= new TriangleCache();
        UpdateDerived();
        var reach = new Vector3(Radius);
        var center = Position;
        var min = center - reach;
        var max = center + reach;
        var margin = new Vector3(0.01f);
        Terrain.Refresh(world, min - margin, max + margin);
        float deepest = 0f;
        var scale = Matrix4x4.CreateScale(1f / Ellipsoid.X, 1f / Ellipsoid.Y, 1f / Ellipsoid.Z);
        foreach (var tri in Terrain.Triangles)
        {
            var local = ToLocal(tri, Inverse);
            if (IsEllipsoid) local = ToLocal(local, scale);
            if (!CollisionWorld.SphereTriangle(local, Vector3.Zero, Radius, out var pushed)) continue;
            var surface = world.SurfaceAt(tri.Surface);
            if (((surface.Mask >> 6) & 1) != 0)
            {
                var contact = pushed * GameMath.InverseLength(pushed) * -Radius;
                var push = pushed;
                if (IsEllipsoid)
                {
                    contact *= Ellipsoid;
                    push *= Ellipsoid;
                }
                var pushWorld = Vector3.TransformNormal(push, World);
                Correct(pushWorld);
                var normal = pushed * GameMath.InverseLength(pushed);
                if (IsEllipsoid) normal /= Ellipsoid;
                normal = Vector3.TransformNormal(normal, World);
                normal *= GameMath.InverseLength(normal);
                Decelerate(Decel, 1f);
                Flags2 |= ContactThisStep;
                SurfaceContact_28B7E8(contact, normal, surface);
                Flags |= TouchA;
                Contact?.Invoke(tri); // Amedo 2026-09-29
                continue;
            }
            wet = true;
            water?.Invoke(tri, Position);
            if (0f < Buoyancy)
            {
                var sunk = IsEllipsoid ? pushed * Ellipsoid : pushed;
                float depth = Vector3.TransformNormal(sunk, World).Y;
                if (deepest < depth) deepest = depth;
            }
            Flags |= TouchA;
            Contact?.Invoke(tri); // Amedo 2026-09-29
        }
        if (wet && 0f < deepest)
            ApplyForceAt(new Vector3(0f, deepest * Mass * Buoyancy - Velocity.Y * 0.3f, 0f), Position);
        return true;
    }

    private bool BoxTerrainContacts(CollisionWorld world, Action<CollisionWorld.Triangle, Vector3>? water, out bool wet) // Amedo 2026-09-27
    {
        wet = false;
        Hulls ??= BuildHulls?.Invoke();
        if (Hulls is not { Count: > 0 } hulls) return true;
        float ownerY = (Position - Vector3.TransformNormal(ComOffset, World)).Y;
        var (ownerMin, ownerMax) = OwnerBox?.Invoke() ?? (new Vector3(ownerY), new Vector3(ownerY)); // Amedo
        float deepest = 0f;
        foreach (var hull in hulls)
        {
            Terrain ??= new TriangleCache();
            var (min, max) = hull.Bounds(World);
            UpdateDerived();
            Terrain.Refresh(world, min, max);
            foreach (var tri in Terrain.Triangles)
            {
                var tmin = Vector3.Min(Vector3.Min(tri.A, tri.B), tri.C);
                var tmax = Vector3.Max(Vector3.Max(tri.A, tri.B), tri.C);
                if (!CollisionWorld.Overlap(tmin, tmax, min, max)) continue;
                CollisionWorld.Plane(tri.A, tri.B, tri.C, out var n, out float w);
                if (hull.Side(World, n, w) != 0) continue;
                if (!hull.Collide(tri, World, out var push, out var contact)) continue;
                var surface = world.SurfaceAt(tri.Surface);
                if (((surface.Mask >> 6) & 1) != 0)
                {
                    var normal = push * GameMath.InverseLength(push);
                    Correct(push);
                    Flags2 |= ContactThisStep;
                    SurfaceContact_28B7E8(Vector3.Transform(contact, Inverse), normal, surface);
                    Decelerate(Decel, 1f);
                    Flags |= TouchA;
                    continue;
                }
                wet = true;
                water?.Invoke(tri, contact);
                if (0f < Buoyancy && deepest < contact.Y - ownerMin.Y) // Amedo
                {
                    WaterLevel = contact.Y;
                    deepest = contact.Y - ownerMin.Y; // Amedo
                }
                Flags |= TouchA;
            }
        }
        if (0f < Buoyancy && (wet ? 0f < deepest : ownerMax.Y < WaterLevel)) BoxFloat?.Invoke(); // Amedo
        return true;
    }

    public Action? BoxFloat;
    public Func<(Vector3 Min, Vector3 Max)>? OwnerBox; // Amedo

    // Amedo 2026-09-29
    public bool ContactHull_28E900(ConvexHull hull, Matrix4x4 m, out Vector3 push, out Vector3 contact, out Vector3 normal)
    {
        push = contact = normal = Vector3.Zero;
        var p = Position;
        var reach = new Vector3(Radius);
        var (hmin, hmax) = hull.Bounds(m);
        if (!CollisionWorld.Overlap(p - reach, p + reach, hmin, hmax)) return false;
        if (IsEllipsoid) return false;
        var local = Vector3.Transform(p, RigidInverse(m));
        if (!HullQueries.SphereHull_1FC818(Radius, hull, local, out var pushed)) return false;
        push = Vector3.TransformNormal(pushed, m);
        normal = push * GameMath.InverseLength(push);
        contact = Vector3.TransformNormal(normal, RigidInverse(World)) * -Radius;
        return true;
    }

    // Amedo 2026-10-01
    public float ImpulseMagnitude_28AE38(in Vector3 local, in Vector3 surfaceVelocity, in Vector3 n, float e, float k)
    {
        UpdateDerived();
        UpdateDerived();
        float vn = GameMath.Dot(surfaceVelocity - PointVelocity(local), n);
        if (0f < vn) return 0f;
        if (k != 0f) e *= vn * vn / (vn * vn + k);
        return -(e + 1f) * vn / EffectiveMass(local, n);
    }

    // Amedo 2026-10-01
    public void RespondMoving_28C358(in Vector3 point, in Vector3 n, in Vector3 surfaceVelocity, in Vector3 surfaceSpin)
    {
        float grip = MathF.Min(1f, Grip), twist = MathF.Min(1f, Twist), roll = MathF.Min(1f, Roll);
        var local = Vector3.Transform(point, Inverse);
        float j = ImpulseMagnitude_28AE38(local, surfaceVelocity, n, Bounce, BounceK);
        ApplyImpulse_289CC0(n * -j, local);
        float limit = j * grip;
        UpdateDerived();
        var slide = PointVelocity(local) - surfaceVelocity;
        slide -= n * GameMath.Dot(slide, n);
        var dir = slide * GameMath.InverseLength(slide);
        var friction = dir * -ImpulseMagnitude_28AE38(local, surfaceVelocity, dir, 0f, 0f);
        if (limit < GameMath.Length(friction)) friction = friction * GameMath.InverseLength(friction) * limit;
        ApplyImpulse_289CC0(friction, local);
        SpinFriction(friction, surfaceSpin, twist, roll);
    }

    public void CorrectInstance_28D330(Vector3 push)
    {
        var half = push * 0.5f;
        Correction += half;
        CorrectionSum += GameMath.Length(half);
        CorrectCapped_28D450(half); // Amedo 2026-10-01
    }

    // Amedo 2026-10-01
    public void CorrectCapped_28D450(Vector3 v)
    {
        InstanceCorrection += v;
        float total = GameMath.Length(InstanceCorrection);
        if (0.2f < total)
        {
            float left = 0.2f - (total - GameMath.Length(v));
            if (!(0f < left)) return;
            Position += v * GameMath.InverseLength(v) * left * 0.5f;
        }
        else Position += v * 0.5f;
        Matrix4x4.Invert(World, out Inverse);
    }

    private static Matrix4x4 RigidInverse(Matrix4x4 m)
    {
        float tx = m.M41, ty = m.M42, tz = m.M43;
        return new Matrix4x4(m.M11, m.M21, m.M31, 0f, m.M12, m.M22, m.M32, 0f, m.M13, m.M23, m.M33, 0f,
                             -(m.M11 * tx + m.M12 * ty + m.M13 * tz), -(m.M21 * tx + m.M22 * ty + m.M23 * tz),
                             -(m.M31 * tx + m.M32 * ty + m.M33 * tz), m.M44);
    }

    public Vector3? LastImpulse;

    // Amedo
    public void SurfaceContact_28B7E8(Vector3 contact, Vector3 normal, CollisionWorld.Surface surface)
    {
        float limitK = MathF.Min(surface.Grip, Grip);
        float e = Bounce * surface.Bounce;
        float twist = Twist * surface.Twist;
        float roll = Roll * surface.Roll;
        float j = ImpulseMagnitude(contact, normal, e, BounceK);
        var impulse = normal * j;
        ApplyImpulse_289CC0(impulse, contact);
        LastImpulse = impulse;
        if (((surface.Mask >> 8) & 1) != 0) SurfaceEvent?.Invoke(surface); // Amedo 2026-09-30
        var before = Velocity;
        if ((Flags2 & ContactThisStep) != 0)
        {
            var change = impulse * InvMass;
            if (1.8f < GameMath.LengthSquared(change))
            {
                bool played = OnLanded?.Invoke(surface, Vector3.Transform(contact, World), change) ?? false;
                Flags2 = played ? Flags2 | HardImpact : Flags2 & ~HardImpact;
            }
        }
        UpdateDerived();
        var slide = -PointVelocity(contact);
        slide -= normal * GameMath.Dot(slide, normal);
        if (0.01f < GameMath.Length(slide))
        {
            float limit = j * limitK;
            var dir = slide * GameMath.InverseLength(slide);
            var friction = dir * ImpulseMagnitude(contact, dir, 0f, 0f);
            if (limit < GameMath.Length(friction)) friction = friction * GameMath.InverseLength(friction) * limit;
            ApplyImpulse_289CC0(friction, contact);
        }
        var at = Vector3.Transform(contact, World);
        if (!IsBox)
        {
            bool played = OnScraped?.Invoke(surface, at, before * 0.3f) ?? false;
            Flags2 = played ? Flags2 | ScrapedStep : Flags2 & ~ScrapedStep;
        }
        else if ((Flags2 & HardImpact) == 0)
        {
            bool played = OnLandedHard?.Invoke(surface, at, before) ?? false;
            Flags2 = played ? Flags2 | LandedHardStep : Flags2 & ~LandedHardStep;
        }
        SpinFriction(impulse, Vector3.Zero, twist, roll);
    }

    private static CollisionWorld.Triangle ToLocal(in CollisionWorld.Triangle tri, Matrix4x4 m) =>
        tri with { A = Vector3.Transform(tri.A, m), B = Vector3.Transform(tri.B, m), C = Vector3.Transform(tri.C, m) };

    // Amedo 2026-09-29
    public void Transfer_28D698(Matrix4x4 obj)
    {
        Momentum = Vector3.TransformNormal(Momentum, obj);
        AngularMomentum = Vector3.TransformNormal(AngularMomentum, obj);
        var m = World * obj;
        Orientation = Quaternion.CreateFromRotationMatrix(Matrix4x4.Transpose(new Matrix4x4(m.M11, m.M12, m.M13, 0f,
                                                                                            m.M21, m.M22, m.M23, 0f,
                                                                                            m.M31, m.M32, m.M33, 0f,
                                                                                            0f, 0f, 0f, 1f)));
        Position = Vector3.Transform(Position, obj);
        Matrix4x4.Invert(World, out Inverse);
        Flags |= MomentumDirty | OrientationDirty;
    }

    // Amedo
    public void MoveBy_291500(Vector3 d)
    {
        Position += d;
        Matrix4x4.Invert(World, out Inverse);
    }

    // Amedo
    public void SetEllipsoid_292348(float x, float y, float z)
    {
        IsEllipsoid = true;
        Radius = x;
        if (x < y) Radius = y;
        if (Radius < z) Radius = z;
        float r = Radius;
        Ellipsoid = new Vector3(x / r, y / r, z / r);
    }

    // Amedo
    public void Slow_291650(float amount, float dt)
    {
        float keep = 1f - amount * dt * 60f;
        if (keep < 0f) keep = 0f;
        Momentum = new Vector3(Momentum.X * keep, Momentum.Y * keep, Momentum.Z * keep);
        AngularMomentum = new Vector3(AngularMomentum.X * keep, AngularMomentum.Y * keep, AngularMomentum.Z * keep);
        Flags |= MomentumDirty;
    }

    // Amedo 2026-09-30
    public void SetVelocity_2915D0(Vector3 v)
    {
        Velocity = v;
        Flags &= ~MomentumDirty;
        Momentum = v * Mass;
        UpdateDerived();
    }

    // Amedo 2026-09-30
    public void SetSpin_289528(Vector3 spin)
    {
        Spin = spin;
        AngularMomentum = Vector3.TransformNormal(spin, WorldInertia());
    }

    // Amedo
    public void Stop_291398()
    {
        SetSpin_289528(Vector3.Zero);
        SetVelocity_2915D0(Vector3.Zero);
    }

    // Amedo 2026-09-30
    public void SetOrientation_291588(Matrix4x4 m)
    {
        Orientation = Quaternion.CreateFromRotationMatrix(Matrix4x4.Transpose(new Matrix4x4(m.M11, m.M12, m.M13, 0f,
                                                                                            m.M21, m.M22, m.M23, 0f,
                                                                                            m.M31, m.M32, m.M33, 0f,
                                                                                            0f, 0f, 0f, 1f)));
        Flags |= MomentumDirty | OrientationDirty;
    }

    public void WriteBack(ref Matrix4x4 scene)
    {
        var com = Vector3.TransformNormal(ComOffset, World);
        var origin = Position - com;
        if ((Flags2 & PositionOnly) != 0)
        {
            scene.M41 = origin.X; scene.M42 = origin.Y; scene.M43 = origin.Z;
            return;
        }
        scene = World;
        scene.M41 = origin.X; scene.M42 = origin.Y; scene.M43 = origin.Z;
    }
}
