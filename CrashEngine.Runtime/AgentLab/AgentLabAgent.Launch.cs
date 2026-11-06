using System.Numerics;
using CrashEngine.Runtime.Physics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-30
public sealed partial class AgentLabAgent
{
    private bool _launched;

    private void Launch_24E358(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        if (CachedTemplate() is not { } bomb) return;
        ulong flags = Arg(39) | ((ulong)Arg(40) << 32);
        if ((AgentFlags & 0x80) != 0 && _hasLinkList && UnlinkInstance_196520(bomb, update: true) && _links.Count == 0) AgentFlags &= ~0x80u; // Amedo
        if ((flags & (1UL << 35)) != 0) bomb.AgentFlags &= ~0x100u;
        bomb._attachedTo = null;
        bomb.AgentFlags &= ~0x40u;

        int space = (int)((flags >> 8) & 0xF);
        int participant = (int)((flags >> 12) & 0xFF), key = (int)((flags >> 20) & 0xFF);
        Vector3? offset = (flags & (1UL << 33)) != 0 ? new Vector3(F(1), F(2), F(3)) : null;
        var target = SourcePosition(participant, key, space, offset);
        if ((flags & (1UL << 34)) != 0) target = World.Rng.JitterVector_23C848(target, F(42), 1f, 1f, 1f); // Amedo

        var d = target - bomb.GamePosition;
        float hd = MathF.Sqrt(d.X * d.X + d.Z * d.Z);
        float speed = FloatValueOf(Arg(41));
        float t = hd / speed;
        var v = new Vector3(d.X * (speed / hd), (d.Y + F(8) * t * 0.5f * t) / t, d.Z * (speed / hd));
        if ((flags & (1UL << 39)) != 0) bomb._slot118 = Alive(ref _slot118);
        Trace($"launch {bomb.Entity.Name} at {target} v {v}");
        bomb.Launch_2331F8(a.GetRange(5, Math.Max(0, a.Count - 5)), v, F(44), F(43));
    }

    // Amedo
    private void LaunchTimer_254160(PhysicsController ctrl, float time)
    {
        StorePlace_23D168(this);
        ctrl.Flags |= PhysicsController.LaunchTimed;
        ctrl.AirTime = 0f;
    }

    private void Launch_2331F8(List<uint> block, Vector3 v, float spinY, float spinX)
    {
        ReleaseRigidBody_23DED8(); // Amedo
        _physics = new PhysicsController();
        _launched = true;
        ReleaseMotionBlock_23DDD0(); // Amedo
        ReleaseTrajectory_23DD80(); // Amedo
        _contactResponse = block;
        SetWobble(block);
        AgentFlags |= 0x8000;
        if (_physics?.Body is not { } body) return;

        body.SetVelocity_2915D0(v);
        if (((WArg(block, 27) >> 10) & 1) != 0) World.Unimplemented("command", 96, "projectile faces its velocity (0x2331F8 / 0x185A40)");
        SpinAlongAxes(body, spinY, spinX);
        if (_slot118 is { } owner) body.IgnoredContact = owner;
    }

    private void SpinAlongAxes(RigidBody body, float spinY, float spinX)
    {
        var m = LocalMatrix;
        if (spinX != 0f) body.SetSpin_289528(new Vector3(m.M11, m.M12, m.M13) * spinX);
        else if (spinY != 0f) body.SetSpin_289528(new Vector3(m.M21, m.M22, m.M23) * spinY);
    }

    private void LaunchSelf_244B60(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        uint flags = Arg(5);
        Vector3 v;
        if ((flags & 0x100000) != 0) v = Vector3.TransformNormal(new Vector3(F(1), F(2), F(3)), LocalMatrix);
        else
        {
            var p = GamePosition;
            Vector3? offset = (flags & 0x200000) != 0 ? new Vector3(F(1), F(2), F(3)) : null;
            var target = SourcePosition((int)((flags >> 4) & 0xFF), (int)((flags >> 12) & 0xFF), (int)(flags & 0xF), offset);
            float g = _physics?.Gravity ?? 0f;
            if ((flags & 0x800000) != 0) // Amedo 2026-10-05
            {
                float h = F(6), rise = target.Y - p.Y;
                if ((flags & 0x1000000) != 0 && 0f < rise) h = rise + h;
                v = Apex_22C0E0(g, p, target, h);
            }
            else if ((flags & 0x400000) == 0)
            {
                World.Unimplemented("command", 193, "launch without a velocity rule (flags 0xC00000 clear)");
                return;
            }
            else v = Ballistic_22C210(g, p, target, F(6));
        }
        _prevVelocity = _velocity;
        _velocity = v;
        Trace($"launch self v {v}");
        Launch_2335D8(F(8), F(7), v);
    }

    private void ColliderLaunchNow_244538(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        if (_physics is not { } ctrl) return;
        ctrl.Flags |= PhysicsController.Launched;
        ulong flags = Arg(5) | ((ulong)Arg(6) << 32);
        Vector3 v = _velocity;
        var target = Vector3.Zero; // Amedo 2026-09-30
        if ((flags & 0x100000) != 0) v = Vector3.TransformNormal(new Vector3(F(1), F(2), F(3)), LocalMatrix);
        else
        {
            var p = GamePosition;
            if ((flags & 0xFF000) is 0xFA000 or 0xEF000) World.Unimplemented("command", 72, "launch at a route position (0x20AF58)");
            else
            {
                Vector3? offset = (flags & 0x200000) != 0 ? new Vector3(F(1), F(2), F(3)) : null;
                target = SourcePosition((int)((flags >> 4) & 0xFF), (int)((flags >> 12) & 0xFF), (int)(flags & 0xF), offset);
            }
            if ((flags & 0x20000000) != 0)
            {
                float h = F(14);
                float rise = target.Y - p.Y;
                if ((flags & 0x800000000) != 0 && 0f < rise) h = rise + h;
                v = Apex_22C0E0(ctrl.Gravity, p, target, h);
            }
            else if ((flags & 0x10000000) != 0) v = Ballistic_22C210(ctrl.Gravity, p, target, F(14));
            else World.Unimplemented("command", 72, "launch without a velocity rule (flags 0x30000000 clear)");
        }
        _prevVelocity = _velocity;
        _velocity = v;
        if ((flags & 0x1000000) != 0) { ctrl.SetB4(F(9)); if (ctrl.Body is { } b4) b4.Bounce = F(9); }
        if ((flags & 0x800000) != 0) { ctrl.SetGrip(F(8)); if (ctrl.Body is { } bg) bg.Grip = F(8); }
        if ((flags & 0x400000) != 0) { ctrl.SetDamping(F(7)); if (ctrl.Body is { } bd) bd.Damping = F(7); }
        if ((flags & 0x2000000) != 0) LaunchTimer_254160(ctrl, F(10)); // Amedo
        ctrl.SetState(0x4, (flags & 0x1000000000) != 0);
        if ((flags & 0x8000000) != 0)
        {
            ctrl.Flags |= PhysicsController.Bit57;
            ctrl.State &= ~PhysicsController.StateCounter;
        }
        ResetContacts_2541B8(ctrl); // Amedo
        Trace($"collider launch v {v} from {GamePosition} to {target} (g {ctrl.Gravity}, h {F(14)}, flags 0x{flags:X}, offset ({F(1)}, {F(2)}, {F(3)}), stored forward {(_storedSpace is { } sf ? new Vector3(sf.M31, sf.M32, sf.M33) : default)}, forward now {new Vector3(LocalMatrix.M31, LocalMatrix.M32, LocalMatrix.M33)})"); // Amedo
    }

    private static Vector3 Apex_22C0E0(float g, Vector3 p, Vector3 target, float h)
    {
        float vy = MathF.Sqrt(MathF.Abs((g + g) * h));
        float dy = target.Y - p.Y;
        float dx = target.X - p.X, dz = target.Z - p.Z;
        float speed = MathF.Sqrt(dx * dx + dz * dz) / ((MathF.Sqrt(MathF.Abs((g + g) * (h - dy))) + vy) / g);
        float inv = GameMath.InverseLength(new Vector3(dx, 0f, dz));
        return new Vector3(dx * inv * speed, vy, dz * inv * speed);
    }

    private static Vector3 Ballistic_22C210(float g, Vector3 p, Vector3 target, float t)
    {
        float dx = target.X - p.X, dz = target.Z - p.Z;
        float hd = MathF.Sqrt(dx * dx + dz * dz);
        float vy = (target.Y - p.Y + g * (t * 0.5f) * t) / t;
        if (MathF.Abs(hd) <= 5e-5f) return new Vector3(0f, vy, 0f);
        float k = hd / t / hd;
        return new Vector3(dx * k, vy, dz * k);
    }

    private void Launch_2335D8(float spinY, float spinX, Vector3 v)
    {
        if (_physics?.Body is not { } body) return;
        body.UpdateDerived();
        body.Force = Vector3.Zero;
        body.Torque = Vector3.Zero;
        body.SetVelocity_2915D0(v);
        SpinAlongAxes(body, spinY, spinX);
    }

    private void Projectile_23B3E0(WobbleState w)
    {
        if (_physics?.Body is not { } body) return;
        var r = w.Args;
        if ((body.Flags & RigidBody.TouchB) == 0)
        {
            if ((body.Flags & RigidBody.TouchA) == 0)
            {
                var m = LocalMatrix;
                var z = new Vector3(m.M31, m.M32, m.M33);
                body.SetOrientation_291588(Along_185A40(z * GameMath.InverseLength(z)));
            }
            else
            {
                if (_ref28 is { } partner) partner._ref28 = null; // Amedo
                if (0f < WFloat(r, 15))
                {
                    _launched = false;
                    World.Unimplemented("physics", 0x2403D8, "projectile lands: hold in place (0x2403D8 / 0x1961C8)");
                }
            }
            return;
        }
        var other = body.LastContact;
        _launched = false;
        var owner = Alive(ref _slot118);
        float grip; // Amedo
        if (0f < WFloat(r, 16) && owner is not null && ReferenceEquals(owner, other)) grip = WFloat(r, 16);
        else if (other is not null && 0f < WFloat(r, 15)) grip = WFloat(r, 15);
        else return;
        if (other is not AgentLabAgent touched) return; // Amedo
        GrabTouched_2402E8(w, touched, grip);
        var ctrl = _physics!; // Amedo
        ctrl.SetB8(3f);
        if (ctrl.Body is { } sized) SetMassAndSize_2462F8(ctrl, sized, 3f);
        ctrl.SetDamping(0.03f);
        if (ctrl.Body is { } damped) damped.Damping = 0.03f;
        ctrl.SetBounce(0.01f);
        if (ctrl.Body is { } bounced) bounced.Decel = ctrl.Damping;
    }

    // Amedo
    private void GrabTouched_2402E8(WobbleState w, AgentLabAgent other, float length)
    {
        _launched = false;
        uint bits = WArg(w.Args, 27);
        float stiffness = WFloat(w.Args, 9);
        bool holdsOther = ((bits >> 11) & 1) != 0 || (((bits >> 12) & 1) != 0 && ReferenceEquals(other, Alive(ref _slot118)));
        Trace($"grab {(holdsOther ? $"holds {other.Entity.Name}" : $"held by {other.Entity.Name}")} on a spring (length {length}, stiffness {stiffness}) (0x2402E8)");
        if (holdsOther)
        {
            AttachmentsOf_196E20();
            HoldOnSpring_196130(other, length, stiffness);
            return;
        }
        other.AttachmentsOf_196E20();
        other.HoldOnSpring_196130(this, length, stiffness);
    }

    internal static Matrix4x4 Along_185A40(Vector3 d) // Amedo 2026-10-06
    {
        var m = Matrix4x4.Identity;
        m.M31 = d.X; m.M32 = d.Y; m.M33 = d.Z;
        float k = 1f - d.Y * d.Y;
        Vector3 up, side;
        if (k == 0f)
        {
            float s = 0f < d.Y ? 1f : -1f;
            up = new Vector3(s, 0f, 0f);
            side = new Vector3(0f, 0f, s);
        }
        else
        {
            up = new Vector3(-d.X * d.Y, k, -d.Y * d.Z);
            up *= GameMath.InverseLength(up);
            side = new Vector3(up.Y * d.Z - up.Z * d.Y, up.Z * d.X - up.X * d.Z, up.X * d.Y - up.Y * d.X);
            side *= GameMath.InverseLength(side);
        }
        m.M11 = side.X; m.M12 = side.Y; m.M13 = side.Z;
        m.M21 = up.X; m.M22 = up.Y; m.M23 = up.Z;
        return m;
    }
}
