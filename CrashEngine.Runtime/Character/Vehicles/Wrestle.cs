using System.Numerics;
using CrashEngine.Runtime.Camera;
using CrashEngine.Runtime.Physics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Character.Vehicles;

// Amedo
public sealed class Wrestle : Vehicle
{
    public const uint BodyMask = 0x1A030;

    public object? Creature { get; }
    public int State;
    public int Tactic;
    public float Radius;
    public float Timer, Timer2;
    public Vector3 Heading;
    public Vector3 LastVelocity;
    public Vector3 WobbleAxis;
    public float Wobble;
    public Vector3 Home;
    public Vector3 UpProbe, DownProbe;
    public Matrix4x4 Squash = Matrix4x4.Identity;
    public Vector3 SquashOffset;
    public float WobblePhase;
    public Vector3 PinPosition;
    public Quaternion PinFrom = Quaternion.Identity, PinTo = Quaternion.Identity;
    public Vector3 CrashPush, CreaturePushVector;
    public float StruggleX, StruggleZ;
    public bool Struggling;
    public int Level;
    public float StruggleTime;
    public Matrix4x4 CrashMatrix = Matrix4x4.Identity;
    public Matrix4x4 PartnerMatrix = Matrix4x4.Identity;

    public Wrestle(CharacterController owner, object? creature) : base(owner, null, true)
    {
        Creature = creature;
        Start_154AB8();
    }

    public override int Kind => Vehicle.Wrestle;
    public override bool HasBody => true;
    public RigidBody? Body => Owner.Body5;
    public override Vector3? Velocity => Body?.Velocity;

    public override void SetPosition(Vector3 position)
    {
        if (Body is { } body) body.SetPosition_2914B0(position);
    }

    public override void SetVelocity(Vector3 velocity)
    {
        if (Body is not { } body) return;
        var v = velocity * GameMath.InverseLength(velocity) * 32f;
        body.SetVelocity_2915D0(Vector3.Zero);
        body.ApplyImpulse_289CC0(Vector3.Zero, v);
    }

    // Amedo
    public float Gauge_161228()
    {
        switch (State)
        {
            case 1:
            {
                float t = Timer * BitConverter.Int32BitsToSingle(0x3F169696);
                if (t < 0f) t = 0f;
                else if (1f < t) t = 1f;
                return t * 0.5f + 0.5f;
            }
            case 2:
            {
                float t = Timer * BitConverter.Int32BitsToSingle(0x3F2AAAAB);
                if (t < 0f) t = 0f;
                else if (1f < t) t = 1f;
                return 0.5f - t * 0.5f;
            }
            case 3: return 1f;
            case 4: return 0f;
            default: return 0.5f;
        }
    }

    private void Start_154AB8()
    {
        var node = Owner.GameMatrix;
        State = 0;
        Tactic = 0;
        Heading = new Vector3(node.M31, node.M32, node.M33);
        Radius = 0.8f;
        UpProbe = new Vector3(-1.1f, 1f, 0.4f);
        UpProbe *= GameMath.InverseLength(UpProbe);
        DownProbe = new Vector3(1.1f, -1f, -0.4f);
        DownProbe *= GameMath.InverseLength(DownProbe);
        Start_14EF90();
        var body = Body!;
        Radius = 0.8f;
        body.SetBoxInertia(1f, 2.5f, 2.5f, 2.5f);
        body.IsEllipsoid = false;
        body.Radius = Radius;
        body.Bounce = 0.35f;
        body.BounceK = 6f * 6f;
        body.Grip = 1.5f;
        body.Twist = 0.5f;
        body.Roll = 0.07f;
        body.Flags2 &= ~RigidBody.SyncTransform;
        body.Contact = Contact_161520;
        body.Substeps = 3;
        body.Terrain = new TriangleCache(0x10);
        body.Decel = 0f;
        body.Damping = 0f;
        if (Creature is { } creature) Owner.ClearInstanceFlags?.Invoke(creature, 0x10);
        LastVelocity = body.Velocity;
        WobbleAxis = Vector3.Zero;
        Wobble = 0f;
        Squash = Matrix4x4.Identity;
        SquashOffset = Vector3.Zero;
        WobblePhase = 0f;
        Home = node.Translation;
        Timer = 0f;
        Timer2 = 0f;
        CrashPush = Vector3.Zero;
        CreaturePushVector = Vector3.Zero;
        StruggleTime = 0f;
        Struggling = false;
        StruggleX = 0f;
        StruggleZ = 0f;
        Level = 0;
    }

    private void Start_14EF90()
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

    private void Contact_161520(CollisionWorld.Triangle tri) => Owner.SurfaceMessage_140958(Owner.CollisionWorld.SurfaceAt(tri.Surface));

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
        var right = new Vector3(fwd.Z, up.Z * fwd.X - up.X * fwd.Z, up.X * fwd.Y - up.Y * fwd.X);
        right *= GameMath.InverseLength(right);
        Frame = new Matrix4x4(right.X, right.Y, right.Z, 0f, up.X, up.Y, up.Z, 0f, fwd.X, fwd.Y, fwd.Z, 0f, p.X, p.Y, p.Z, 1f);
        return true;
    }

    public override void Step(float dt) => Frame_154FF0(dt);

    private void Frame_154FF0(float dt)
    {
        CrashPush = Vector3.Zero;
        CreaturePushVector = Vector3.Zero;
        if (State is 3 or 4) PinnedFrame_153C90();
        else RollFrame_153B58(dt);
        StepTactic_154890();
        StepState_153D60();
        Timer += dt;
        Timer2 += dt;
        float la = CrashPush.X * CrashPush.X + CrashPush.Y * CrashPush.Y + CrashPush.Z * CrashPush.Z;
        float lb = CreaturePushVector.X * CreaturePushVector.X + CreaturePushVector.Y * CreaturePushVector.Y + CreaturePushVector.Z * CreaturePushVector.Z;
        float struggle = 0f;
        if (1e-4f < la && 1e-4f < lb)
        {
            if (1f < la) CrashPush *= GameMath.InverseLength(CrashPush);
            if (1f < lb) CreaturePushVector *= GameMath.InverseLength(CreaturePushVector);
            var a = CrashPush;
            var b = CreaturePushVector;
            float d = a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            if (d <= 0f) struggle = -d;
        }
        Owner.TrailLevel?.Invoke(struggle);
        if (!UpdateFrames()) return;
        Owner.SetNode_18AF40(CrashMatrix);
        if (Creature is { } creature) Owner.SetNodeOf?.Invoke(creature, PartnerMatrix);
    }

    private void RollFrame_153B58(float dt)
    {
        var body = Body!;
        bool touching = (body.Flags & (RigidBody.TouchB | RigidBody.TouchA)) != 0;
        body.Force += new Vector3(0f, body.Mass * -35f, 0f);
        StickPush_153268(touching);
        Drag_153600();
        SpinDrag_153718();
        Wobble_153800(dt);
        if (body.Velocity.Y < -40f) Owner.Trigger(0xB);
    }

    private void PinnedFrame_153C90()
    {
        float t = Timer * 1.4285715f;
        if (1f < t) t = 1f;
        float s = t * (t * 3f) - (t + t) * t * t;
        var m = GameMath.QuatToMatrix_18E620(GameMath.Slerp_187100(s, PinFrom, PinTo));
        m.Translation = PinPosition;
        Body!.InitFromTransform(m);
        UpdateFrames();
    }

    private void StickPush_153268(bool touching)
    {
        var body = Body!;
        float x = Owner.MoveX, z = Owner.MoveZ;
        var s = new Vector3(x, 0f, z);
        if (!touching)
        {
            body.Force += new Vector3(x * 8f, 0f, z * 8f) * body.Mass;
            return;
        }
        var point = body.Position + new Vector3(x + x, 0f, z + z);
        var force = new Vector3(0f, -(MathF.Sqrt(s.X * s.X + s.Y * s.Y + s.Z * s.Z) * 10f), 0f);
        CrashPush = s;
        var flat = new Vector3(body.Velocity.X, 0f, body.Velocity.Z);
        flat *= GameMath.InverseLength(flat);
        float along = (1f - (s.X * flat.X + s.Y * flat.Y + s.Z * flat.Z)) * 0.5f;
        if (along < 0f) along = 0f;
        if (1f < along) along = 1f;
        force *= along * 0.5f + 1f;
        body.AddForceAtPoint_289F08(force, point);
    }

    private void Drag_153600()
    {
        var body = Body!;
        var v = body.Velocity;
        float speed = MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        float k = 0.09f <= speed ? speed - 0.09f : 0f;
        k *= -0.005f;
        float m = body.Mass;
        body.Force += new Vector3(v.X * k * m, v.Y * k * m, v.Z * k * m);
    }

    private void SpinDrag_153718()
    {
        var body = Body!;
        var w = body.Spin;
        float speed = MathF.Sqrt(w.X * w.X + w.Y * w.Y + w.Z * w.Z);
        float k = 0.09f <= speed ? speed - 0.09f : 0f;
        k *= -0.0025f;
        body.Torque += w * k;
    }

    private void CreaturePush_1534F8(Vector3 d)
    {
        var body = Body!;
        CreaturePushVector = d;
        var point = body.Position + new Vector3(d.X + d.X, d.Y + d.Y, d.Z + d.Z);
        var force = new Vector3(0f, -(MathF.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z) * 10f), 0f);
        body.AddForceAtPoint_289F08(force, point);
    }

    private void Wobble_153800(float dt)
    {
        var body = Body!;
        Squash = Matrix4x4.Identity;
        var dv = body.Velocity - LastVelocity;
        LastVelocity = body.Velocity;
        float len = MathF.Sqrt(dv.X * dv.X + dv.Y * dv.Y + dv.Z * dv.Z);
        if (43f < len)
        {
            var d = dv * GameMath.InverseLength(dv);
            WobbleAxis = Vector3.TransformNormal(d, body.Inverse);
        }
        else if (15f < len && Wobble < len)
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
        float k = w * c;
        var n = WobbleAxis;
        Squash.M11 -= k * n.X * n.X; Squash.M12 -= k * n.X * n.Y; Squash.M13 -= k * n.X * n.Z;
        Squash.M21 -= k * n.Y * n.X; Squash.M22 -= k * n.Y * n.Y; Squash.M23 -= k * n.Y * n.Z;
        Squash.M31 -= k * n.Z * n.X; Squash.M32 -= k * n.Z * n.Y; Squash.M33 -= k * n.Z * n.Z;
        SquashOffset = Vector3.Zero;
    }

    private Vector3 WorldOf(Vector3 local) => Vector3.TransformNormal(local, Body!.World);

    private void StepState_153D60()
    {
        float y = WorldOf(UpProbe).Y;
        switch (State)
        {
            case 0:
                if (0.68f < y) { Timer = 0f; State = 1; }
                else if (y < -0.68f) { Timer = 0f; State = 2; }
                return;
            case 1:
                if (y < 0.58f) { Timer = 0f; State = 0; return; }
                if (!(1.7f < Timer)) return;
                Owner.Trigger(0x67);
                Timer = 0f;
                State = 3;
                StartPin_154DE0(new Vector3(0f, 1f, 0f));
                return;
            case 2:
                if (-0.58f < y) { Timer = 0f; State = 0; return; }
                if (!(1.5f < Timer)) return;
                Owner.Trigger(0x69);
                Timer = 0f;
                State = 4;
                StartPin_154DE0(new Vector3(0f, -1f, 0f));
                return;
            case 3:
            case 4:
                if (4f < Timer) { Timer = 0f; State = 0; }
                return;
        }
    }

    private void StartPin_154DE0(Vector3 dir)
    {
        var node = Owner.GameMatrix;
        PinPosition = node.Translation;
        PinFrom = GameMath.MatrixToQuat_186900(node);
        var u = Vector3.TransformNormal(Vector3.UnitY, node);
        var axis = new Vector3(u.Y * dir.Z - u.Z * dir.Y, u.Z * dir.X - u.X * dir.Z, u.X * dir.Y - u.Y * dir.X);
        if (!(5e-5f < axis.X * axis.X + axis.Y * axis.Y + axis.Z * axis.Z))
        {
            PinTo = PinFrom;
            return;
        }
        axis *= GameMath.InverseLength(axis);
        int angle = GameMath.Acos_18B738(u.X * dir.X + u.Y * dir.Y + u.Z * dir.Z);
        PinTo = GameMath.Mul_18D558(GameMath.AxisAngle_18D798(axis, angle), PinFrom);
    }

    private void StepTactic_154890()
    {
        if (State is 3 or 4) return;
        if (Level == 5) Tactic = 4;
        switch (Tactic)
        {
            case 0:
                Tactic = 3;
                return;
            case 1:
                HeadHome_153F60();
                return;
            case 2:
            {
                var d = Home - Body!.Velocity;
                if (2f < MathF.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z))
                {
                    d *= GameMath.InverseLength(d);
                    d += d;
                }
                CreaturePush_1534F8(d);
                if (1.5f < Timer2) Tactic = 0;
                return;
            }
            case 3:
                Struggle_1541D8();
                return;
            case 4:
                PressDown_1544E8();
                return;
        }
    }

    private void HeadHome_153F60()
    {
        var body = Body!;
        var w = body.Spin;
        if (30f < MathF.Sqrt(w.X * w.X + w.Y * w.Y + w.Z * w.Z))
        {
            Tactic = 0;
            return;
        }
        var d = Home - body.Position;
        var flat = new Vector3(d.X, 0f, d.Z);
        var n = flat * GameMath.InverseLength(flat);
        var v = n * 6f - body.Velocity;
        if (2f < MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z))
        {
            v *= GameMath.InverseLength(v);
            v += v;
        }
        CreaturePush_1534F8(v);
        if (MathF.Sqrt(flat.X * flat.X + flat.Y * flat.Y + flat.Z * flat.Z) < 6f) Tactic = 0;
    }

    private static readonly float[] RestTimes = [3.5f, 3.5f, 3.5f, 3.5f, 3.5f, 4f];
    private static readonly float[] StruggleTimes = [2.2f, 3.4f, 5.5f, 3.5f, 2f, 1f];

    private int GetRandom_18C018(int n) => Owner.Rng.GetRandom_18C018(n); // Amedo

    private float GetRandFloat_18C130(float scale) => Owner.Rng.GetRandFloat__18C130(scale); // Amedo

    private void Struggle_1541D8()
    {
        float uy = WorldOf(UpProbe).Y;
        int odds = 60 - (int)(uy * uy * 40f);
        if (!Struggling)
        {
            if (GetRandom_18C018(odds) == 0)
            {
                float lo, hi;
                if (Level < 5) { lo = 0.5f; hi = 0.8f; }
                else { lo = 0.75f; hi = 1.05f; }
                float r;
                do
                {
                    StruggleX = GetRandFloat_18C130(1f);
                    StruggleZ = GetRandFloat_18C130(1f);
                    r = StruggleX * StruggleX + StruggleZ * StruggleZ;
                }
                while (r < lo || hi < r);
            }
            if (RestTimes[Level] < Timer2)
            {
                Struggling = true;
                Timer2 = 0f;
                StruggleTime = StruggleTimes[Level];
                Level++;
                if (6 <= Level) Level = 5;
                StruggleX = 0f;
                StruggleZ = 0f;
            }
        }
        else if (StruggleTime < Timer2)
        {
            Struggling = false;
            Timer2 = 0f;
        }
        CreaturePush_1534F8(new Vector3(StruggleX, 0f, StruggleZ));
        var d = Home - Body!.Position;
        if (9f < MathF.Sqrt(d.X * d.X + d.Z * d.Z))
        {
            Tactic = 1;
            return;
        }
        if (0.707f < WorldOf(DownProbe).Y) Tactic = 4;
    }

    private void PressDown_1544E8()
    {
        var body = Body!;
        var s = WorldOf(UpProbe);
        float len = MathF.Sqrt(s.X * s.X + s.Y * s.Y + s.Z * s.Z);
        s.Y = 0f;
        s *= GameMath.InverseLength(s);
        float reach = len * 5f + 2f;
        float cap = Timer2 * 0.3f + 0.4f;
        if (1.3f < cap) cap = 1.3f;
        var v = (s * reach - body.Velocity) * 0.25f;
        if (cap < MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z)) v = v * GameMath.InverseLength(v) * cap;
        CreaturePush_1534F8(v);
        var d = Home - body.Position;
        if (9f < MathF.Sqrt(d.X * d.X + d.Z * d.Z))
        {
            Tactic = 1;
            return;
        }
        float dy = WorldOf(DownProbe).Y;
        if (2.4f < Timer2 || dy < -0.707f) Tactic = 0;
    }

    public override void Transfer(Matrix4x4 obj) => LastVelocity = Vector3.TransformNormal(LastVelocity, obj);

    public override void Destroy()
    {
        if (Creature is { } creature) Owner.SetInstanceFlags?.Invoke(creature, 0x10);
        if (Body is { } body) body.Contact = null;
        Owner.Body5 = null;
    }
}
