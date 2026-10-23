using System.Numerics;
using CrashEngine.Runtime.Camera;
using CrashEngine.Runtime.Physics;

namespace CrashEngine.Runtime.Character.Vehicles;

// Amedo
public sealed class Hoverboard : Vehicle
{
    public const uint BodyMask = 0x1A030;

    public object? Board { get; }
    public float Knock;
    public object? Towed;
    public bool TowLatch;
    public bool Armed;
    public float HoverHeight;
    public float CameraValue;
    public bool AutoForward, CanTow, CanRise;
    public Vector3 Size;
    public float HoverError;
    public Matrix4x4 CrashMatrix = Matrix4x4.Identity;
    public Matrix4x4 PartnerMatrix = Matrix4x4.Identity;

    public Hoverboard(CharacterController owner, object? board, bool armed) : base(owner, null, true)
    {
        Board = board;
        Towed = null;
        Armed = armed;
        HoverHeight = 3.5f;
        Start_15B3C0();
        CameraValue = 5f;
    }

    public override int Kind => Vehicle.Hoverboard;
    public override bool HasBody => true;
    public override bool AllowsLook => true;
    public override bool KeepsGun => Armed;
    public override (Vector3 Min, Vector3 Max) Box => (new Vector3(-2f), new Vector3(2f));
    public RigidBody? Body => Owner.Body5;
    public override Vector3? Velocity => Body?.Velocity;

    private bool TowedAlive => Towed is { } t && Owner.AliveOf?.Invoke(t) == true;

    public override void SetPosition(Vector3 position)
    {
        if (Body is { } body) body.SetPosition_2914B0(position);
    }

    private void SetArmedHandling_161AD8()
    {
        CanRise = true;
        Size = new Vector3(1f, 0.7f, 1.6f);
        AutoForward = true;
        CanTow = false;
    }

    private void SetUnarmedHandling_161B18()
    {
        Size = new Vector3(1.4f, 0.5f, 1.4f);
        CanTow = true;
        AutoForward = false;
        CanRise = false;
    }

    private void Start_15B3C0()
    {
        if (Armed) SetArmedHandling_161AD8();
        else SetUnarmedHandling_161B18();
        Knock = 0f;
        if (TowedAlive) Towed = null;
        TowLatch = false;
        Start_14EF90();
        Owner.SetAttachedTo_28?.Invoke(Board);
        var body = Body!;
        if (Armed)
        {
            body.SetBoxInertia(5f, Size.X + Size.X, Size.Y, Size.Z);
            body.SetEllipsoid_292348(Size.X, Size.Y, Size.Z);
            body.BounceK = 40f * 40f;
        }
        else
        {
            body.SetBoxInertia(5f, Size.X, Size.Y, Size.Z);
            body.SetEllipsoid_292348(Size.X, Size.Y, Size.Z);
            body.BounceK = 0.5f * 0.5f;
        }
        body.Grip = 0f;
        body.Twist = 0.002f;
        body.Roll = 0.002f;
        body.Flags2 &= ~RigidBody.SyncTransform;
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

    public override bool UpdateFrames() => Place_15B830();

    private bool Place_15B830()
    {
        if (Body is not { } body) return true;
        PartnerMatrix = body.World;
        var m = PartnerMatrix;
        var t = m.Translation + new Vector3(m.M21, m.M22, m.M23) * -0.76f;
        t += new Vector3(m.M31, m.M32, m.M33) * -0.15f;
        m.M41 = t.X; m.M42 = t.Y; m.M43 = t.Z;
        CrashMatrix = m;
        var f = new Vector3(m.M31, m.M32, m.M33);
        var u = Vector3.UnitY;
        var r = new Vector3(-f.Z, f.Z * u.X - f.X * u.Z, f.X * u.Y - f.Y * u.X);
        var fwd = new Vector3(r.Y * u.Z - r.Z * u.Y, r.Z * u.X - r.X * u.Z, r.X * u.Y - r.Y * u.X);
        Frame = new Matrix4x4(r.X, r.Y, r.Z, 0f, u.X, u.Y, u.Z, 0f, fwd.X, fwd.Y, fwd.Z, 0f, t.X, t.Y, t.Z, 1f);
        return true;
    }

    public override void Step(float dt) => Frame_15B558(dt);

    private void Frame_15B558(float dt)
    {
        var body = Body!;
        float impulse = body.ImpulseSum;
        Drive_159DB8();
        if (CanTow)
        {
            if (Owner.Dead)
            {
                if (TowedAlive) Towed = null;
            }
            else Tow_15AE88();
        }
        Knock -= dt * 0.1f;
        if (Knock < 0f) Knock = 0f;
        if (150f < impulse)
        {
            if (Knock < 1f) Knock = 1f;
            if (!Armed)
            {
                var w = body.Spin;
                body.SetSpin_289528(new Vector3(w.X * 0.5f, w.Y * 5f, w.Z * 0.5f));
            }
        }
        if (Armed)
        {
            if (0.4f < Knock) Knock = 0.4f;
            if (0f < Knock) CameraValue = 1e-4f;
            else CameraValue = CameraValue * 0.999f + 0.0100000007f;
            body.Bounce = 0.3f;
        }
        else body.Bounce = 0.6f;
        if (!UpdateFrames()) return;
        Owner.SetNode_18AF40(CrashMatrix);
        if (Board is { } board) Owner.SetNodeOf?.Invoke(board, PartnerMatrix);
    }

    private void Drive_159DB8()
    {
        var body = Body!;
        float mx = Owner.MoveX, mz = Owner.MoveZ;
        float x = MathF.Sqrt(MathF.Abs(mx)) * 0.7f;
        if (mx <= 0f) x = -x;
        float z = MathF.Sqrt(MathF.Abs(mz)) * 0.7f;
        if (mz <= 0f) z = -z;
        var s = new Vector3(x, 0f, z);
        if (AutoForward)
        {
            var n = new Vector3(body.World.M31, 0f, body.World.M33);
            n *= RigidBody.InverseLength(n);
            float d = n.X * s.X + n.Y * s.Y + n.Z * s.Z;
            var g = n * (1f - Knock);
            g *= 1f - d;
            s += g;
            s *= RigidBody.InverseLength(s);
        }
        float push = 32f;
        if (Armed) push = (1f - (HoverHeight - 3.5f) * 0.048780486f * 0.5f) * 32f;
        var force = s * push;
        body.Force += force * body.Mass;
        Hover_15A338();
        Tilt_15A718(HoverError, s, body.World);
        Turn_15AA80(s, body.World);
        Drag_15A0C0();
        SpinDrag_15A208();
    }

    private void Hover_15A338()
    {
        var body = Body!;
        if (TowedAlive) Owner.ClearInstanceFlags?.Invoke(Towed!, 0x10);
        for (int i = 0; i < 4; i++)
        {
            var corner = new Vector3((i & 1) != 0 ? 0.5f : -0.5f, 0f, (i & 2) != 0 ? 0.5f : -0.5f);
            var from = Vector3.Transform(corner, body.World);
            var to = new Vector3(from.X, from.Y - 26f, from.Z);
            float lift;
            if (Owner.HoverRay_281408?.Invoke(from, to, Board) is { } hit)
            {
                float gap = from.Y - hit.Y;
                if (gap < 0f) gap = 0f;
                HoverHeight = 3.5f;
                if (CanRise && (0.1f < Owner.RightMinusLeft || Owner.MoveZ < -0.8f)) HoverHeight = 24f;
                float err = HoverHeight - gap;
                HoverError = err;
                if (Armed)
                {
                    lift = err * 8.5f;
                    if (lift < 0f) lift *= 0.1f;
                }
                else lift = err * 20f;
                if (0f < lift) lift *= 1f - Knock;
            }
            else
            {
                HoverError = -10f;
                lift = -200f;
            }
            var point = corner * 0.3f;
            lift *= 0.25f;
            body.AddForce_2918B0(new Vector3(0f, lift * body.Mass, 0f), point);
        }
        if (TowedAlive) Owner.SetInstanceFlags?.Invoke(Towed!, 0x10);
    }

    private void Tilt_15A718(float h, Vector3 s, Matrix4x4 w)
    {
        var body = Body!;
        var up = new Vector3(0f, 1f, 0f) + s * 0.2f;
        if (3f < h) h = 3f;
        if (h < -3f) h = -3f;
        up += new Vector3(w.M31, w.M32, w.M33) * (h * -0.07f);
        up *= RigidBody.InverseLength(up);
        var r = new Vector3(w.M21, w.M22, w.M23);
        var axis = new Vector3(r.Y * up.Z - r.Z * up.Y, r.Z * up.X - r.X * up.Z, r.X * up.Y - r.Y * up.X);
        float d = r.X * up.X + r.Y * up.Y + r.Z * up.Z;
        if (d < 0f) axis = axis * RigidBody.InverseLength(axis) * (1f - d);
        float gain = Armed ? -45f : -40f;
        body.Torque += axis * gain * (1f - Knock) * 5f;
    }

    private void Turn_15AA80(Vector3 s, Matrix4x4 w)
    {
        if (!(0.001f < MathF.Sqrt(s.X * s.X + s.Y * s.Y + s.Z * s.Z))) return;
        var body = Body!;
        var f = new Vector3(w.M31, w.M32, w.M33);
        f *= RigidBody.InverseLength(f);
        var t = s * RigidBody.InverseLength(s);
        var axis = new Vector3(t.Y * f.Z - t.Z * f.Y, t.Z * f.X - t.X * f.Z, t.X * f.Y - t.Y * f.X);
        float gain = Armed ? 20f : 14f;
        body.Torque += axis * gain * (1f - Knock) * 5f;
    }

    private void Drag_15A0C0()
    {
        var body = Body!;
        float k = 1f - Knock;
        var v = Vector3.TransformNormal(body.Velocity, body.Inverse);
        var d = new Vector3(v.X * MathF.Abs(v.X) * -1.4f * k, v.Y * MathF.Abs(v.Y) * -1.5f * k, v.Z * MathF.Abs(v.Z) * -0.015f * k);
        body.Force += Vector3.TransformNormal(d, body.World) * 5f;
    }

    private void SpinDrag_15A208()
    {
        var body = Body!;
        float k = 1f - Knock;
        var w = Vector3.TransformNormal(body.Spin, body.Inverse);
        var d = new Vector3(w.X * -2.6f, w.Y * -4.2f * k, w.Z * -2.6f);
        body.Torque += Vector3.TransformNormal(d, body.World) * 5f;
    }

    private void Tow_15AE88()
    {
        bool pressed = 0.1f <= Owner.Circle;
        var towedBody = TowedAlive ? Owner.BodyOf?.Invoke(Towed!) : null;
        if (towedBody is null)
        {
            if (!pressed)
            {
                TowLatch = true;
                return;
            }
            if (!TowLatch) return;
            var p = Body!.Position;
            var min = new Vector3(p.X - 3f, p.Y - 15f, p.Z - 3f);
            var max = new Vector3(p.X + 3f, p.Y, p.Z + 3f);
            object? best = null;
            float bestD = 1e30f;
            if (Owner.TowCandidates_1ECFC0?.Invoke(min, max) is { } found)
                foreach (var o in found)
                {
                    if (Owner.PositionOf?.Invoke(o) is not { } q) continue;
                    float dx = q.X - p.X, dz = q.Z - p.Z;
                    float d2 = dx * dx + dz * dz;
                    float dy = p.Y - q.Y;
                    if (0f < dy && dy < 12f && d2 < bestD)
                    {
                        bestD = d2;
                        best = o;
                    }
                }
            if (best is null) return;
            Owner.WakeBody?.Invoke(best);
            if (TowedAlive && ReferenceEquals(Towed, best))
            {
                TowLatch = false;
                return;
            }
            Towed = best;
            TowLatch = false;
            return;
        }
        var a = Body!.Position;
        var b = towedBody.Position;
        var ab = a - b;
        if (2.6f < MathF.Sqrt(ab.X * ab.X + ab.Y * ab.Y + ab.Z * ab.Z))
        {
            Body!.Spring_28A198(b, Vector3.Zero, false, 2.6f, 50f, 5f);
            towedBody.Spring_28A198(a, Vector3.Zero, false, 2.6f, 50f, 5f);
        }
        if (!pressed)
        {
            TowLatch = true;
            return;
        }
        if (!TowLatch) return;
        if (TowedAlive) Towed = null;
        TowLatch = false;
    }

    public void Knock_15ACB0(float strength, object? source)
    {
        if (!(0.1f < strength) || source is null) return;
        Knock = 1f;
        var body = Body!;
        var m = body.World;
        var p = m.Translation;
        if (Owner.PositionOf?.Invoke(source) is not { } q) return;
        var d = p - q;
        float k = strength * 4f / (d.X * d.X + d.Y * d.Y + d.Z * d.Z + 4f);
        var j = d * RigidBody.InverseLength(d) * k;
        body.ApplyImpulse_289CC0(j, Vector3.Transform(q, CamMath.RigidInverse_18E808(m)));
    }

    public override void Destroy()
    {
        Towed = null;
        Owner.Body5 = null;
    }
}
