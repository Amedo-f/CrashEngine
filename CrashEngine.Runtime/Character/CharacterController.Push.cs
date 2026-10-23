using System.Numerics;
using CrashEngine.Runtime.Physics;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-09-30
public sealed partial class CharacterController
{
    public object? Pushed;
    public float PushDistance;
    public float SlideKickCooldown;
    public float SpinKickCooldown;
    public object? LimbTouched;
    public Vector3 LimbTouchPush;
    public Func<object, RigidBody?>? BodyOf;
    public Action<object>? WakeBody;
    private readonly List<object> _pushTouched = new();

    private Vector3 Center_13F8C8() => Position + new Vector3(0f, CenterOffset, 0f);

    private static bool IsSphere(RigidBody body) => !body.IsBox;

    private void Push_134810(float dt)
    {
        if (BodyOf is not { } bodyOf) return;
        SlideKickCooldown -= dt;
        if (SlideKickCooldown < 0f) SlideKickCooldown = 0f;
        SpinKickCooldown -= dt;
        if (SpinKickCooldown < 0f) SpinKickCooldown = 0f;
        _pushTouched.Clear();
        foreach (var r in Collision.Solids)
        {
            if ((r.Flags & CharacterCollision.Dynamic) == 0 || r.Owner is not { } owner || bodyOf(owner) is null) continue;
            if ((r.Flags & 0x400) != 0) _pushTouched.Add(owner);
        }
        if (!Carrying)
        {
            if (MotionState is 6 or 10)
            {
                if (Pushed is not null && SpinKickCooldown == 0f) SpinKick_133B18();
                Pushed = null;
            }
            else
            {
                if (Pushed is not null) Keep_133CE8();
                if (Pushed is null) Start_133FF0();
            }
            if (Pushed is { } pushed)
            {
                if (bodyOf(pushed) is not { } body) Pushed = null;
                else if (_pushTouched.Contains(pushed)) PushDistance = (body.Position - Center_13F8C8()).Length() + 0.1f;
                else if (0.1f < MathF.Sqrt(MoveX * MoveX + MoveZ * MoveZ)) PushDistance -= dt;
            }
            Force_134318();
        }
        foreach (var r in Collision.Solids)
        {
            if ((r.Flags & CharacterCollision.Dynamic) == 0 || r.Owner is not { } owner || bodyOf(owner) is not { } body) continue;
            if ((r.Flags & 0x200) != 0 && Velocity.Y < 0f) Stand_133990(body);
            if ((r.Flags & 0x400) == 0) continue;
            var push = r.Push;
            if (MotionState is 8 or 12 && SlideKickCooldown == 0f && IsGrounded &&
                MathF.Abs(push.Y) < 2f * MathF.Sqrt(push.X * push.X + push.Z * push.Z))
            {
                SlideKick_133140(owner, body);
                SlideKickCooldown = 1f;
            }
            else Contact_133360(owner, body, push);
        }
        if (LimbTouched is { } limb && bodyOf(limb) is { } touched && !ReferenceEquals(limb, Pushed))
            Contact_133360(limb, touched, LimbTouchPush);
    }

    private void Start_133FF0()
    {
        foreach (var owner in _pushTouched)
        {
            if (ReferenceEquals(owner, StandingOn) || BodyOf!(owner) is not { } body) continue;
            if ((body.Flags & 0x60) == 0 || !IsGrounded) continue;
            var d = body.Position - Center_13F8C8();
            var flat = new Vector3(d.X, 0f, d.Z);
            flat *= InverseLength(flat);
            if (!(0.1f < MathF.Sqrt(MoveX * MoveX + MoveZ * MoveZ))) continue;
            if (!(0.5f < Vector3.Dot(ForwardAxis, flat))) continue;
            Pushed = owner;
            PushDistance = d.Length();
            WakeBody?.Invoke(owner);
        }
    }

    private void Keep_133CE8()
    {
        if (BodyOf!(Pushed!) is { } body && !(body.Velocity.Length() < 0.2f && Velocity.Length() < 0.2f) && IsGrounded)
        {
            var d = body.Position - Center_13F8C8();
            d *= InverseLength(d);
            if (0f < Vector3.Dot(d, ForwardAxis) && d.Length() < 4f)
            {
                var input = new Vector3(MoveX, 0f, MoveZ);
                var flat = new Vector3(d.X, 0f, d.Z);
                flat *= InverseLength(flat);
                if (input.Length() < 0.2f || -0.35f < Vector3.Dot(input, flat)) return;
            }
        }
        Pushed = null;
    }

    private void Force_134318()
    {
        bool look = Bones.Kind != 4;
        if (Pushed is not { } pushed)
        {
            if (look) Bones.Look.Push_15EE38(0f, 0f, 0f, false);
            return;
        }
        if (BodyOf!(pushed) is not { } body)
        {
            Pushed = null;
            return;
        }
        float r = IsSphere(body) ? body.Radius : 0.5f;
        var m = _getMatrix();
        var fwd = new Vector3(m.M31, m.M32, m.M33);
        var target = Center_13F8C8() + fwd * ((MotionFlagsHigh & 1u) != 0 ? r + 1.15f : PushDistance);
        var dv = target - body.Position;
        if (1.3f < dv.Length())
        {
            Pushed = null;
            return;
        }
        var f = dv * 350f - (body.Velocity - Velocity) * 90f;
        float lateral = Vector3.Dot(dv, new Vector3(m.M11, m.M12, m.M13));
        float along = Vector3.Dot(dv, fwd);
        f.Y = 0f;
        if (80f < MathF.Sqrt(f.X * f.X + f.Z * f.Z)) f = f * InverseLength(f) * 80f;
        if (r < 0.6f) f *= r * 1.6666666f;
        body.AddForce_2918B0(f, Vector3.Zero);
        if (look) Bones.Look.Push_15EE38(lateral, along, r, true);
    }

    private void SpinKick_133B18()
    {
        if (BodyOf!(Pushed!) is not { } body) return;
        body.AngularMomentum += new Vector3(0f, -1f, 0f);
        body.Flags |= RigidBody.MomentumDirty;
        var fwd = ForwardAxis;
        float k = Vector3.Dot(fwd, Velocity) * 0.6f + 15f;
        var j = new Vector3(fwd.X * k, (fwd.Y + MathF.Sqrt(fwd.X * fwd.X + fwd.Z * fwd.Z) * 0.1f) * k, fwd.Z * k);
        var p = Center_13F8C8() + new Vector3(0f, 0.8f, 0f);
        body.ApplyImpulse_289CC0(j, Vector3.Transform(p, body.Inverse));
        SpinKickCooldown = 0.8f;
    }

    private void SlideKick_133140(object owner, RigidBody body)
    {
        var c = Center_13F8C8();
        var local = Vector3.Transform(c, body.Inverse);
        var d = body.Position - c;
        d.Y = 0f;
        d *= InverseLength(d);
        float k = Crouch.SlideScale;
        if (k < 0f) k = 0f;
        if (1f < k) k = 1f;
        d *= 4f;
        body.ApplyImpulse_289CC0(d * 1.2f, local);
        body.ApplyImpulse_289CC0(new Vector3(0f, (k * 5.8f + 3.2f) * 1.2f, 0f), Vector3.Zero);
        Crouch.Friction = 0f;
        if (ReferenceEquals(owner, Pushed)) Pushed = null;
    }

    private void Contact_133360(object owner, RigidBody body, Vector3 push)
    {
        if (MathF.Sqrt(push.X * push.X + push.Z * push.Z) < MathF.Abs(push.Y)) return;
        var p = Center_13F8C8() + new Vector3(0f, 0.8f, 0f);
        var local = Vector3.Transform(p, body.Inverse);
        var dv = Velocity - body.PointVelocityAt_289408(p);
        var f = dv * 3.5f;
        if (Vector3.Dot(dv, ForwardAxis) < 0f) return;
        if (MotionState is 6 or 10)
        {
            if (SpinKickCooldown == 0f && !ReferenceEquals(owner, Pushed))
            {
                body.AngularMomentum += new Vector3(0f, -1f, 0f);
                body.Flags |= RigidBody.MomentumDirty;
                body.ApplyImpulse_289CC0(new Vector3(f.X * 0.5f, 0f, f.Z * 0.5f), local);
                SpinKickCooldown = 0.8f;
            }
            f *= 1.4f;
        }
        body.AddForce_2918B0(f, local);
    }

    private void Stand_133990(RigidBody body)
    {
        bool sphere = IsSphere(body);
        var c = Center_13F8C8();
        var local = Vector3.Transform(c, body.Inverse);
        if (!sphere) body.AddForce_2918B0(new Vector3(0f, -30f, 0f), local);
        else if (Velocity.Y - body.PointVelocityAt_289408(c).Y < 2f) body.AddForceAtPoint_289F08(new Vector3(0f, -30f, 0f), c);
        if (sphere || (Jump.Flags & 0x800000) == 0) return;
        body.ApplyImpulse_289CC0(new Vector3(Velocity.X * 0.7f, Velocity.Y, Velocity.Z * 0.7f), local);
        Jump.Flags &= ~0x800000u;
    }
}
