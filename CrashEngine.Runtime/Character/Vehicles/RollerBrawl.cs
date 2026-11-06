using System.Numerics;
using CrashEngine.Runtime.Physics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Character.Vehicles;

// Amedo 2026-09-29
public sealed class RollerBrawl : Vehicle
{
    public const uint BodyMask = 0x1A030;

    public int State;
    public float Radius;
    public float CloudScale;
    public float Size;
    public bool OnSnow;
    public Vector3 Heading;
    public Vector3 LastVelocity;
    public Vector3 WobbleAxis;
    public float Wobble;
    public Matrix4x4 Squash = Matrix4x4.Identity;
    public Vector3 SquashOffset;
    public float WobblePhase;
    public float Timer;
    public float ShrinkTimer;
    public Vector3 StopPosition;
    public Quaternion StopFrom = Quaternion.Identity;
    public Quaternion StopTo = Quaternion.Identity;
    public Matrix4x4 CrashMatrix = Matrix4x4.Identity;
    public Matrix4x4 PartnerMatrix = Matrix4x4.Identity;
    public readonly SkidMark Marks1 = new(), Marks2 = new();

    public RollerBrawl(CharacterController owner, CharacterController? other) : base(owner, other, true)
    {
        Init_152B68();
        Owner.SkidMarks?.Invoke(Marks1.Ring, true); // Amedo
        Owner.SkidMarks?.Invoke(Marks2.Ring, true);
    }

    public override int Kind => Rollerbrawl;
    public override bool HasBody => true;
    public RigidBody? Body => Owner.Body5;

    public override Vector3? Velocity => Body?.Velocity;

    public override void SetPosition(Vector3 position)
    {
        if (Body is not { } body) return;
        body.Position = position;
        Matrix4x4.Invert(body.World, out body.Inverse);
    }

    public override void SetVelocity(Vector3 velocity)
    {
        if (Body is not { } body) return;
        body.Velocity = velocity;
        body.Flags &= ~RigidBody.MomentumDirty;
        body.Momentum = velocity * body.Mass;
        body.UpdateDerived();
    }

    private void Init_152B68()
    {
        var node = Owner.GameMatrix;
        State = 0;
        Heading = new Vector3(node.M31, node.M32, node.M33);
        Radius = 0.8f;
        WobbleAxis = Vector3.Zero;
        Squash = Matrix4x4.Identity;
        SquashOffset = Vector3.Zero;
        Wobble = 0f;
        WobblePhase = 0f;
        Timer = 0f;
        ShrinkTimer = 0f;
        Marks1.Clear_161C68(); // Amedo
        Marks2.Clear_161C68();
        Init_14EF90();
        var body = Body!;
        Size = 0f;
        Radius = 0.8f;
        CloudScale = 0.6f;
        body.SetBoxInertia(1f, 1.1f, 1.1f, 1.1f);
        body.IsEllipsoid = false;
        body.Radius = Radius;
        body.Bounce = 0.15f;
        body.BounceK = 12f * 12f;
        body.Grip = 1.5f;
        body.Twist = 0.5f;
        body.Roll = 0.07f;
        body.Contact = Contact_160FF0;
        body.Flags2 &= ~RigidBody.SyncTransform;
        body.Substeps = 3;
        body.Terrain = new TriangleCache(0x10);
        body.Decel = Size;
        body.Damping = Size;
        Other?.SetCollidable?.Invoke(false);
        LastVelocity = body.Velocity;
        Size = 0f;
        Radius = 0.8f;
        CloudScale = 0.6f;
        body.SetBoxInertia(1f, 1.1f, 1.1f, 1.1f);
        body.IsEllipsoid = false;
        body.Radius = Radius;
        OnSnow = false;
    }

    private void Init_14EF90()
    {
        Frame = Matrix4x4.Identity;
        CrashMatrix = Matrix4x4.Identity;
        PartnerMatrix = Matrix4x4.Identity;
        Owner.Body5 = null;
        var body = new RigidBody { Radius = 0.5f };
        body.SetBoxInertia(1f, 1.3f, 1.3f, 1.3f);
        body.InitFromTransform(Owner.GameMatrix);
        body.CollisionMask = BodyMask;
        body.Flags2 &= ~RigidBody.SyncTransform;
        Owner.Body5 = body;
        UpdateFrames();
    }

    // Amedo
    public override void SplashSphere_160278(out Vector3 position, out float radius)
    {
        if (Body is not { } body)
        {
            base.SplashSphere_160278(out position, out radius);
            return;
        }
        position = body.Position;
        radius = Radius;
    }

    public override bool UpdateFrames()
    {
        if (Body is not { } body) return true;
        var m = body.World;
        var deformed = Squash * m;
        deformed.M41 = m.M41 + SquashOffset.X;
        deformed.M42 = m.M42 + SquashOffset.Y;
        deformed.M43 = m.M43 + SquashOffset.Z;
        PartnerMatrix = deformed;
        CrashMatrix = deformed;
        var p = new Vector3(m.M41, m.M42 + (0.05f - Radius), m.M43);
        var fwd = new Vector3(Heading.X, 0f, Heading.Z);
        fwd *= GameMath.InverseLength(fwd);
        var up = Vector3.UnitY;
        var right = new Vector3(up.Y * fwd.Z - up.Z * fwd.Y, up.Z * fwd.X - up.X * fwd.Z, up.X * fwd.Y - up.Y * fwd.X);
        right *= GameMath.InverseLength(right);
        Frame = new Matrix4x4(right.X, right.Y, right.Z, 0f, up.X, up.Y, up.Z, 0f, fwd.X, fwd.Y, fwd.Z, 0f, p.X, p.Y, p.Z, 1f);
        return true;
    }

    public override void Step(float dt)
    {
        if (Body is not { } body) return;
        bool touching = (body.Flags & (RigidBody.TouchB | RigidBody.TouchA)) != 0;
        switch (State)
        {
            case 0:
                Rolling_151758(dt);
                if (touching) Accumulate(dt);
                break;
            case 1:
                Stopped_151A20(dt);
                if (touching) Accumulate(dt);
                break;
            case 2:
                Squashed_151D28(dt);
                goto done;
            default:
                goto done;
        }
        if (0f < ShrinkTimer) Shrink_160D78(dt * 0.5f);
        OnSnow = false;
        done:
        SnowCloud_150AD0(dt);
        Trails_152468((body.Flags & RigidBody.TouchA) != 0);
        Touches_1526F0();
    }

    private void Accumulate(float dt)
    {
        var v = Body!.Velocity;
        float travelled = MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z) * dt;
        if (OnSnow) Grow_160C78(travelled * 0.01f);
        else Shrink_160D78(travelled * 0.02f);
    }

    private void PlaceNodes()
    {
        if (!UpdateFrames()) return;
        Owner.SetNode_18AF40(CrashMatrix);
        Other?.SetNode_18AF40(PartnerMatrix);
    }

    private void Rolling_151758(float dt)
    {
        var body = Body!;
        bool touching = (body.Flags & (RigidBody.TouchB | RigidBody.TouchA)) != 0;
        body.Force += new Vector3(0f, body.Mass * -35f, 0f);
        Input_150DA8(touching);
        Drag_151270();
        SpinDrag_151388();
        Impact_151470(dt);
        if (body.Velocity.Y < -120f) Owner.Trigger(0xB);
        float inZ = Owner.MoveZ, inX = Owner.MoveX;
        var v = body.Velocity;
        var w = body.Spin;
        if (v.X * v.X + v.Z * v.Z + (w.X * w.X + w.Y * w.Y + w.Z * w.Z) < 0.4f)
        {
            if (0.010000001f <= inX * inX + inZ * inZ) Timer = 0f;
            else
            {
                Timer += dt;
                if (0.35f < Timer)
                {
                    if (Size == 0f && GroundBelow_152328()) Stop_152000();
                    else Timer = 0f;
                }
            }
        }
        else Timer = 0f;
        PlaceNodes();
    }

    private void Input_150DA8(bool touching)
    {
        var body = Body!;
        float x = Owner.MoveX, z = Owner.MoveZ;
        if (!touching)
        {
            body.Force += new Vector3(x * 8f * body.Mass, 0f * body.Mass, z * 8f * body.Mass);
            return;
        }
        var point = body.Position + new Vector3(x + x, 0f, z + z);
        float len = MathF.Sqrt(x * x + 0f * 0f + z * z);
        len *= len;
        var force = new Vector3(0f, -(len * len * 10f), 0f);
        var flat = new Vector3(body.Velocity.X, 0f, body.Velocity.Z);
        flat *= GameMath.InverseLength(flat);
        float along = (1f - (x * flat.X + 0f * flat.Y + z * flat.Z)) * 0.5f;
        if (along < 0f) along = 0f;
        if (1f < along) along = 1f;
        force *= along * 0.5f + 1f;
        float dot = x * flat.X + 0f * flat.Y + z * flat.Z;
        float stick = MathF.Sqrt(x * x + 0f * 0f + z * z);
        var v = body.Velocity;
        float speed = MathF.Sqrt(v.X * v.X + v.Z * v.Z);
        if (1f < dot) dot = 1f;
        else if (dot < -1f) dot = -1f;
        float roll;
        if (4f < speed)
        {
            if (0f < dot || stick < 0.1f) roll = 8f < speed ? 0f : 0.5f;
            else
            {
                float d2 = dot * dot, d4 = d2 * d2;
                roll = d4 * d4 * 18f + 1f;
            }
        }
        else if (0.001f < speed)
        {
            if (0.01f < dot) roll = 1f;
            else if (stick < 0.1f) roll = 5f;
            else roll = dot * (dot * 10f) + 1f;
        }
        else roll = 1.2f / (stick + 0.2f);
        body.Twist = 0.5f;
        body.Roll = roll * 0.07f;
        body.AddForceAtPoint_289F08(force, point);
    }

    private void Drag_151270()
    {
        var body = Body!;
        var v = body.Velocity;
        float speed = MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        float k = 0.09f <= speed ? speed - 0.09f : 0f;
        k *= -0.005f;
        float m = body.Mass;
        body.Force += new Vector3(v.X * k * m, v.Y * k * m, v.Z * k * m);
    }

    private void SpinDrag_151388()
    {
        var body = Body!;
        var w = body.Spin;
        float speed = MathF.Sqrt(w.X * w.X + w.Y * w.Y + w.Z * w.Z);
        float k = 0.09f <= speed ? speed - 0.09f : 0f;
        k *= -0.0025f;
        body.Torque += w * k;
    }

    private void Impact_151470(float dt)
    {
        var body = Body!;
        Squash = Matrix4x4.Identity;
        var dv = body.Velocity - LastVelocity;
        LastVelocity = body.Velocity;
        float len = MathF.Sqrt(dv.X * dv.X + dv.Y * dv.Y + dv.Z * dv.Z);
        if (15f < len && Wobble < len)
        {
            float amp = len * 0.016666668f;
            Wobble = amp;
            if (0.5f < amp) Wobble = 0.5f;
            else if (amp < -0.5f) Wobble = -0.5f;
            var d = dv * GameMath.InverseLength(dv);
            WobbleAxis = Vector3.TransformNormal(d, body.Inverse);
            float a = MathF.Abs(Wobble);
            Owner.CameraImpulse?.Invoke(a + a, 0.01f, body.Position);
            WobblePhase = 0f;
        }
        if (Wobble == 0f) return;
        float c = MathF.Cos(GameMath.FromValue_11BFA0(WobblePhase, 0) * GameMath.UnitToRad);
        float w = Wobble;
        WobblePhase += dt * 21f;
        Wobble = w * (1f - dt * 1.5f);
        float s = w * c;
        var n = WobbleAxis;
        Squash.M11 -= s * n.X * n.X; Squash.M12 -= s * n.X * n.Y; Squash.M13 -= s * n.X * n.Z;
        Squash.M21 -= s * n.Y * n.X; Squash.M22 -= s * n.Y * n.Y; Squash.M23 -= s * n.Y * n.Z;
        Squash.M31 -= s * n.Z * n.X; Squash.M32 -= s * n.Z * n.Y; Squash.M33 -= s * n.Z * n.Z;
        SquashOffset = Vector3.Zero;
    }

    private bool GroundBelow_152328()
    {
        var body = Body!;
        var from = new Vector3(body.Position.X, body.Position.Y - Radius + 0.05f, body.Position.Z);
        return Owner.CollisionWorld.SegmentCast(from, from + new Vector3(0f, -0.1f, 0f), 0x10, out _);
    }

    private void Stop_152000()
    {
        var node = Owner.GameMatrix;
        StopPosition = node.Translation;
        StopFrom = GameMath.MatrixToQuat_186900(node);
        var b = new Vector3(node.M21, node.M22, node.M23);
        b *= GameMath.InverseLength(b);
        var axis = new Vector3(b.Y * 0f - b.Z * 1f, b.Z * 0f - b.X * 0f, b.X * 1f - b.Y * 0f);
        if (5e-05f < axis.X * axis.X + axis.Y * axis.Y + axis.Z * axis.Z)
        {
            axis *= GameMath.InverseLength(axis);
            int angle = GameMath.Acos_18B738(b.X * 0f + b.Y * 1f + b.Z * 0f);
            var q = GameMath.AxisAngle_18D798(axis, angle);
            q = GameMath.Mul_18D558(q, StopFrom);
            float yaw = Owner.Rng.RandomBelowFloat_18C180(360f); // Amedo
            float h = GameMath.FromValue_11BFA0(yaw, 0) * GameMath.UnitToRad * 0.5f;
            var spinY = h == 0f ? Quaternion.Identity : new Quaternion(0f, MathF.Sin(h), 0f, MathF.Cos(h));
            StopTo = GameMath.Mul_18D558(q, spinY);
        }
        else StopTo = StopFrom;
        byte bump = (byte)Owner.Rng.GetRandom_18C018(0xFF); // Amedo
        Owner.SetBump154?.Invoke(bump);
        Other?.SetBump154?.Invoke(bump);
        Owner.Trigger(0x4C);
        Other?.Trigger(0x4C);
        Wobble = 0f;
        State = 1;
        Timer = 0f;
        ShrinkTimer = 0f;
    }

    private void Stopped_151A20(float dt)
    {
        var body = Body!;
        float inZ = Owner.MoveZ, inX = Owner.MoveX;
        body.Position = StopPosition;
        Matrix4x4.Invert(body.World, out body.Inverse);
        body.Force += new Vector3(body.Mass * 0f, body.Mass * -35f, body.Mass * 0f);
        Drag_151270();
        SpinDrag_151388();
        Impact_151470(dt);
        float t = Timer + Timer;
        var v = body.Velocity;
        var w = body.Spin;
        float energy = v.X * v.X + v.Y * v.Y + v.Z * v.Z + w.X * w.X + w.Y * w.Y + w.Z * w.Z;
        if (1f < t) t = 1f;
        var q = GameMath.Slerp_187100(t * t * 3f - (t + t) * t * t, StopFrom, StopTo);
        var m = GameMath.QuatToMatrix_18E620(q);
        m.Translation = body.Position;
        body.InitFromTransform(m);
        UpdateFrames();
        PlaceNodes();
        Timer += dt;
        if (8f < Timer || 0.040000003f < inX * inX + 0f + inZ * inZ || 0.8f < energy) Resume_160C10();
    }

    private void Resume_160C10()
    {
        Owner.Trigger(0x4D);
        Other?.Trigger(0x4D);
        Timer = 0f;
        State = 0;
    }

    private void Squashed_151D28(float dt)
    {
        var body = Body!;
        body.Force += new Vector3(0f, body.Mass * -35f, 0f);
        WobbleAxis = Vector3.UnitY;
        float f = 1f - Timer * 0.33333334f * 20f;
        float flat = 0.1f <= f ? f : 0.1f;
        float wide = (flat - 0.1f) * -1.1111112f + 2f;
        Squash = Matrix4x4.Identity;
        var n = WobbleAxis;
        float k = flat / wide - 1f;
        Squash.M11 = (Squash.M11 + k * n.X * n.X) * wide; Squash.M12 = (Squash.M12 + k * n.X * n.Y) * wide; Squash.M13 = (Squash.M13 + k * n.X * n.Z) * wide;
        Squash.M21 = (Squash.M21 + k * n.Y * n.X) * wide; Squash.M22 = (Squash.M22 + k * n.Y * n.Y) * wide; Squash.M23 = (Squash.M23 + k * n.Y * n.Z) * wide;
        Squash.M31 = (Squash.M31 + k * n.Z * n.X) * wide; Squash.M32 = (Squash.M32 + k * n.Z * n.Y) * wide; Squash.M33 = (Squash.M33 + k * n.Z * n.Z) * wide;
        SquashOffset = Vector3.Zero;
        UpdateFrames();
        PlaceNodes();
        Timer += dt;
        if (3f < Timer) Owner.Trigger(0xB);
    }

    private void Grow_160C78(float amount)
    {
        Size += amount;
        Resize(Size);
    }

    private void Shrink_160D78(float amount)
    {
        Size -= amount;
        if (Size < 0f) Size = 0f;
        Resize(Size);
    }

    private void Resize(float size)
    {
        if (3f < Size) Size = 3f;
        Radius = MathF.Max(Size - 0.4f, 0f) + 0.8f;
        CloudScale = Size - 0.2f + 0.8f;
        var body = Body!;
        body.SetBoxInertia(size * 0.55f + 1f, 1.1f, 1.1f, 1.1f);
        body.IsEllipsoid = false;
        body.Radius = Radius;
    }

    private void Contact_160FF0(CollisionWorld.Triangle tri)
    {
        var surface = Owner.CollisionWorld.SurfaceAt(tri.Surface);
        if (((surface.Mask >> 10) & 1) != 0) OnSnow = true;
        if ((surface.HitKind & 8) == 0) Owner.SurfaceMessage_140958(surface);
        else if (Size <= 0f) Owner.SurfaceMessage_140958(surface);
        else if (ShrinkTimer < 0.6f) ShrinkTimer = 0.6f;
        if (0f < ShrinkTimer) OnSnow = false;
    }

    private void SnowCloud_150AD0(float dt)
    {
        if (Body is { } body && 0f < Size)
        {
            var m = body.World;
            m.M11 *= CloudScale; m.M12 *= CloudScale; m.M13 *= CloudScale;
            m.M21 *= CloudScale; m.M22 *= CloudScale; m.M23 *= CloudScale;
            m.M31 *= CloudScale; m.M32 *= CloudScale; m.M33 *= CloudScale;
            Owner.SnowCloud?.Invoke(m);
        }
        else Owner.SnowCloud?.Invoke(null);
        float left = ShrinkTimer;
        if (0f < left)
        {
            ShrinkTimer = left - dt;
            if (ShrinkTimer < 0f) ShrinkTimer = 0f;
        }
    }

    private float Signed(float x) => Owner.Rng.GetRandFloat__18C130(x); // Amedo

    private void Trails_152468(bool touching)
    {
        if (touching && Body is { } body)
        {
            var p = new Vector4(body.Position, 1f);
            p.Y -= Radius * 0.866f;
            var v = body.Velocity;
            if (0.1f < MathF.Sqrt(v.X * v.X + v.Z * v.Z))
            {
                const float eps = 2.4999998e-09f;
                var flat = new Vector4(v.X, 0f, v.Z, 0f);
                float k = GameMath.InvLength_11C030(eps, flat);
                var dir = new Vector4(flat.X * k, flat.Y * k, flat.Z * k, 0f);
                var side = new Vector4(dir.Y * 0f - dir.Z * 1f, dir.Z * 0f - dir.X * 0f, dir.X * 1f - dir.Y * 0f, 1f);
                float ks = GameMath.InvLength_11C030(eps, side);
                side = new Vector4(side.X * ks, side.Y * ks, side.Z * ks, 1f);
                float offset = Radius * 0.5f;
                IReadOnlyList<CollisionWorld.Triangle> tris = (body.Terrain ??= new TriangleCache()).Triangles;
                var world = Owner.CollisionWorld;
                Marks1.Lay_15BDB0(p, side, tris, world, offset, 0.16f, Signed);
                Marks2.Lay_15BDB0(p, new Vector4(-side.X, -side.Y, -side.Z, side.W), tris, world, offset, 0.16f, Signed);
                return;
            }
        }
        Marks1.Idle_161C40();
        Marks2.Idle_161C40();
    }

    private void Touches_1526F0()
    {
        if (Owner.BallBodies_1ECFC0 is not { } query || Body is not { } body) return;
        var p = body.Position;
        var reach = new Vector3(Radius);
        var bodies = query(p - reach, p + reach, Other?.Self);
        for (int i = 0; i < bodies.Count; i++)
        {
            var target = bodies[i];
            if (((Owner.InstanceFlagsOf?.Invoke(target.Owner) ?? 0u) & 0x10) != 0) continue;
            for (int part = 0; part < target.Parts.Count; part++) // Amedo
            {
                var (hull, m) = target.Parts[part];
                var local = Vector3.Transform(p, GameMath.RigidInverse_18E808(m));
                if (!HullQueries.SphereHull_1FC818(Radius, hull, local, out _)) continue;
                Owner.SurfaceMessage_140958(Owner.CollisionWorld.SurfaceAt(Owner.HullSurfaceOf?.Invoke(target.Owner, part) ?? 0)); // Amedo
                Owner.TouchFrom_137858(target.Owner, body.Velocity, -body.Velocity);
                if (Owner.Self is { } self && ((Owner.InstanceFlagsOf?.Invoke(self) ?? 0u) & 0x8000) != 0) // Amedo
                    Owner.BodyHit_23D1E0?.Invoke(self, target.Owner, body.Position, -body.Velocity);
            }
        }
    }

    public override void Transfer(Matrix4x4 obj) => LastVelocity = Vector3.TransformNormal(LastVelocity, obj);

    public override void Destroy()
    {
        Marks2.Destroy_161B98(); // Amedo
        Marks1.Destroy_161B98();
        Owner.SkidMarks?.Invoke(Marks2.Ring, false);
        Owner.SkidMarks?.Invoke(Marks1.Ring, false);
        if (Body is { } body) body.Contact = null;
        Other?.SetCollidable?.Invoke(true);
        Owner.Body5 = null;
    }
}
