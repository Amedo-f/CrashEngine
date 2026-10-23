using System.Numerics;
using CrashEngine.Runtime.Camera;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-09-29
public sealed class CharacterLook
{
    public const int Bones = 5;

    public bool Started;
    public Vector3 Target;
    public bool HasTarget;
    public float StickX, StickY;
    public float MinA = -1f, MaxA = 0.5f, MaxAlt = 1f, MinB = -1f, MaxB = 1f;
    public float AngleA, AngleB, SpeedA, SpeedB;
    public float DeathTimer;
    public bool AltMode;
    public float PushLateral, PushAlong, PushRadius; // Amedo 2026-09-30
    public int Kind;
    public readonly int[][] Angles = { new int[3], new int[3], new int[3], new int[3], new int[3] };
    public Vector3 GunAim; // Amedo
    public bool GunAimValid;
    public Quaternion Arm = Quaternion.Identity;
    private Quaternion _armPrev = Quaternion.Identity;
    private int _frameTicks;

    public void Reset_147410()
    {
        AngleA = AngleB = SpeedA = SpeedB = 0f;
        AltMode = false;
        Target = Vector3.Zero;
        HasTarget = false;
        StickX = StickY = 0f;
        foreach (var a in Angles) Array.Clear(a);
        Arm = _armPrev = Quaternion.Identity; // Amedo
        GunAimValid = false;
    }

    // Amedo
    public void Aim_15EE60(Vector3 point, bool valid)
    {
        GunAim = point;
        GunAimValid = valid;
    }

    // Amedo 2026-09-30
    public void Push_15EE38(float lateral, float along, float radius, bool pushing)
    {
        AltMode = pushing;
        PushLateral = lateral;
        PushAlong = along;
        PushRadius = radius;
    }

    public void Input_130958(float stickX, float stickY, int motionState, bool crouched, Vector3? target)
    {
        HasTarget = target is not null;
        if (target is { } t) Target = t;
        bool allowed = motionState != 7 && motionState != 11 && !crouched;
        StickX = allowed ? stickX : 0f;
        StickY = allowed ? stickY : 0f;
    }

    public void Update_1476A8(float dt, bool cutscene, bool crouched, Matrix4x4? head = null, Vector3 forward = default) // Amedo 2026-10-02
    {
        if (0f < DeathTimer)
        {
            Death_147B18(dt);
            DeathTimer -= dt;
            return;
        }
        Started = true;
        _frameTicks = (int)MathF.Round(dt * CamMath.SecToTick); // Amedo
        Dynamics_146E28(dt, cutscene, crouched, head, forward);
        var target = new int[Bones][];
        for (int i = 0; i < Bones; i++) target[i] = new int[3];
        int a = CamMath.Trunc(AngleA * CamMath.RadToUnit);
        int b = CamMath.Trunc(AngleB * CamMath.RadToUnit);
        if (Kind == 5)
        {
            int qa = CamMath.Trunc(a * 0.25f), qb = CamMath.Trunc(b * 0.25f);
            for (int i = 0; i < 4; i++) { target[i][0] = qa; target[i][1] = qb; }
        }
        else if (!AltMode)
        {
            target[0][0] = CamMath.Trunc(a * 0.25f);
            target[0][2] = CamMath.Trunc(CamMath.Trunc(-AngleB * CamMath.RadToUnit) * 0.25f);
            int b1 = CamMath.Trunc(-AngleB * CamMath.RadToUnit);
            target[1][0] = a;
            if (Kind == 0) target[1][1] = b;
            else target[1][2] = b1;
            target[3][0] = CamMath.Trunc(a * 0.2f);
            target[3][1] = CamMath.Trunc(b * 0.2f);
            target[2][0] = CamMath.Trunc(a * 0.3f);
            target[2][1] = CamMath.Trunc(b * 0.3f);
            for (int k = 0; k < 3; k++) target[1][k] = CamMath.Trunc(target[1][k] * 0.4f);
            target[4][0] = target[3][0];
            target[4][1] = target[3][1];
        }
        else
        {
            float fa = a, fb = b;
            target[0][0] = CamMath.Trunc(fa * -0.3f);
            target[0][2] = CamMath.Trunc(CamMath.Trunc(-AngleB * CamMath.RadToUnit) * -0.3f);
            target[1][0] = target[0][0];
            target[1][1] = CamMath.Trunc(fb * -0.3f);
            target[2][0] = CamMath.Trunc(fa * 0.05f);
            target[2][1] = CamMath.Trunc(fb * 0.05f);
            target[3][0] = CamMath.Trunc(fa * 0.35f);
            target[3][1] = CamMath.Trunc(fb * 0.35f);
            target[4][0] = target[3][0];
            target[4][1] = target[3][1];
        }
        for (int i = 0; i < Bones; i++) Follow_1474D8(dt, Angles[i], target[i]);
    }

    private static readonly float Eps = BitConverter.Int32BitsToSingle(0x312BCC76); // Amedo 2026-10-02
    public float TargetFacing { get; private set; } = float.NaN;

    private void Dynamics_146E28(float dt, bool cutscene, bool crouched, Matrix4x4? head, Vector3 forward)
    {
        bool active = false;
        if (Kind == 5 && GunAimValid) // Amedo
        {
            HasTarget = true;
            Target = GunAim;
        }
        if (cutscene)
        {
            HasTarget = false;
            AltMode = false;
        }
        else if (!AltMode)
        {
            if (0.01f < StickX * StickX + StickY * StickY)
            {
                active = true;
                SpeedA = StickY * 5f;
                SpeedB = StickX * -8f;
            }
            else if (HasTarget && !crouched && head is { } m) // Amedo 2026-10-02
            {
                var d = new Vector4(Target.X - m.M41, Target.Y - m.M42, Target.Z - m.M43, 1f);
                float inv = CamMath.InvLengthGuarded_11C030(Eps, d);
                float dx = d.X * inv, dy = d.Y * inv, dz = d.Z * inv;
                TargetFacing = dx * forward.X + dy * forward.Y + dz * forward.Z;
                if (0.7f < TargetFacing)
                {
                    SpeedA += ((m.M21 * dx + m.M22 * dy + m.M23 * dz) * -26f - SpeedA * 5.6f) * dt;
                    SpeedB += ((m.M11 * dx + m.M12 * dy + m.M13 * dz) * 26f - SpeedB * 5.6f) * dt;
                    active = true;
                }
            }
        }
        else // Amedo 2026-09-30
        {
            float k = (1f / (PushRadius + 1f) - 0.56f) * -5f;
            PushAlong += k;
            SpeedB += ((PushLateral * -1.1f - AngleB) * 26f - SpeedB * 5.6f) * dt;
            SpeedA += ((PushAlong * -1.2f + k - AngleA) * 26f - SpeedA * 5.6f) * dt;
            active = true;
        }
        if (!active)
        {
            SpeedB += (-AngleB * 20f - SpeedB * 5f) * dt;
            SpeedA += (-AngleA * 20f - SpeedA * 5f) * dt;
        }
        AngleA += SpeedA * dt;
        AngleB += SpeedB * dt;
        float maxA = AltMode ? MaxAlt : MaxA;
        if (AngleA < MinA) AngleA = MinA;
        else if (maxA < AngleA) AngleA = maxA;
        if (MinB <= AngleB)
        {
            if (MaxB < AngleB) AngleB = MaxB;
        }
        else AngleB = MinB;
    }

    public void Death_147B18(float dt)
    {
        if (!Started) return;
        static float Toward(float v, float step) => 0f < v ? MathF.Max(0f, v - step) : v < 0f ? MathF.Min(0f, v + step) : v;
        AngleA = Toward(AngleA, dt * 32f);
        AngleB = Toward(AngleB, dt * 32f);
        int a = CamMath.Trunc(AngleA * CamMath.RadToUnit);
        int b = CamMath.Trunc(AngleB * CamMath.RadToUnit);
        var target = new int[Bones][];
        for (int i = 0; i < Bones; i++) target[i] = new int[3];
        target[0][0] = CamMath.Trunc(a * 0.25f);
        target[0][2] = CamMath.Trunc(CamMath.Trunc(-AngleB * CamMath.RadToUnit) * 0.25f);
        target[1][0] = a;
        if (Kind == 0) target[1][1] = b;
        else target[1][2] = CamMath.Trunc(-AngleB * CamMath.RadToUnit);
        target[3][0] = CamMath.Trunc(a * 0.2f);
        target[3][1] = CamMath.Trunc(b * 0.2f);
        target[2][0] = CamMath.Trunc(a * 0.3f);
        target[2][1] = CamMath.Trunc(b * 0.3f);
        for (int k = 0; k < 3; k++) target[1][k] = CamMath.Trunc(target[1][k] * 0.4f);
        target[4][0] = target[3][0];
        target[4][1] = target[3][1];
        for (int i = 0; i < Bones; i++) Follow_1474D8(dt, Angles[i], target[i]);
    }

    private static void Follow_1474D8(float dt, int[] current, int[] target)
    {
        float max = dt * 1.6f;
        for (int k = 0; k < 3; k++)
        {
            float d = (target[k] - current[k]) * 9.58738e-05f;
            if (max < d) d = max;
            else if (d < -max) d = -max;
            current[k] += CamMath.Trunc(d * CamMath.RadToUnit);
        }
    }

    public bool Rotate_148148(int reactId, ref Quaternion rot, bool hasRotation)
    {
        if (!Started || reactId < 0) return false;
        int bone = Kind == 5 ? reactId switch { 0 => 0, 2 => 1, 4 => 2, _ => -1 } : reactId < Bones ? reactId : -1; // Amedo
        if (bone < 0) return false;
        var a = Angles[bone];
        var q = CamMath.Euler_186780(a[0], a[1], a[2]);
        rot = hasRotation ? Quaternion.Multiply(q, rot) : q;
        return true;
    }
    // Amedo
    public bool Transform_148148(int reactId, ref Matrix4x4 model, Matrix4x4 node)
    {
        if (!Started || Kind != 5 || reactId != 0xE) return false;
        var arm = Arm;
        if (CrashEngine.Importer.AnimatedObject.InLogicCompose)
        {
            var target = GunAimValid ? GunAim : new Vector3(node.M31 * 1000f + node.M41, node.M32 * 1000f + node.M42, node.M33 * 1000f + node.M43);
            var q = AimRotation_147E50(model, node, target);
            var delta = CamMath.Mul_18D558(q, Conjugate_15DEF0(Arm));
            AxisAngle_186B50(delta, out var axis, out int angle);
            float step = _frameTicks * CamMath.TickToSec * 1.3f;
            if (CamMath.Trunc(step * CamMath.RadToUnit) < angle) angle = CamMath.Trunc(step * CamMath.RadToUnit);
            else if (angle < CamMath.Trunc(-step * CamMath.RadToUnit)) angle = CamMath.Trunc(-step * CamMath.RadToUnit);
            _armPrev = Arm;
            Arm = CamMath.Mul_18D558(CamMath.AxisAngle_18D798(axis, angle), Arm);
            arm = Arm;
        }
        else if (SimulationClock.Active) arm = CamMath.Slerp_187100(SimulationClock.Alpha, _armPrev, Arm);
        var m = model;
        m.M41 = m.M42 = m.M43 = 0f;
        m.M44 = 1f;
        var r = m * CamMath.QuatToMatrix_18E620(arm);
        r.M41 = model.M41;
        r.M42 = model.M42;
        r.M43 = model.M43;
        r.M44 = model.M44;
        model = r;
        return true;
    }

    private static Quaternion AimRotation_147E50(Matrix4x4 joint, Matrix4x4 node, Vector3 target)
    {
        var v = CamMath.Rotate3_18E4B8(node, new Vector4(joint.M11, joint.M12, joint.M13, joint.M14));
        float kv = CamMath.InvLengthGuarded_11C030(Eps, v);
        v = new Vector4(-(v.X * kv), -(v.Y * kv), -(v.Z * kv), v.W);
        var p = CamMath.Transform_18E4F0(node, new Vector4(joint.M41, joint.M42, joint.M43, joint.M44));
        var d = new Vector4(target.X - p.X, target.Y - p.Y, target.Z - p.Z, 1f);
        float ku = CamMath.InvLengthGuarded_11C030(Eps, d);
        var u = new Vector3(d.X * ku, d.Y * ku, d.Z * ku);
        var c = new Vector4(v.Y * u.Z - v.Z * u.Y, v.Z * u.X - v.X * u.Z, v.X * u.Y - v.Y * u.X, 1f);
        if (!(5e-05f < c.X * c.X + c.Y * c.Y + c.Z * c.Z)) return Quaternion.Identity;
        float kc = CamMath.InvLengthGuarded_11C030(Eps, c);
        c = new Vector4(c.X * kc, c.Y * kc, c.Z * kc, c.W);
        int angle = CamMath.Acos_18B738(v.X * u.X + v.Y * u.Y + v.Z * u.Z);
        var axis = CamMath.Rotate3_18E4B8(CamMath.RigidInverse_18E808(node), c);
        return CamMath.AxisAngle_18D798(new Vector3(axis.X, axis.Y, axis.Z), angle);
    }

    private static Quaternion Conjugate_15DEF0(Quaternion q)
    {
        float k = CamMath.QuatInvLength_11C0B8(q, 0f, 1e-10f);
        return new Quaternion(q.X * -k, q.Y * -k, q.Z * -k, q.W * k);
    }

    private static void AxisAngle_186B50(Quaternion q, out Vector3 axis, out int angle)
    {
        float l2 = q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W;
        float k = 1e-20f < l2 ? 1f / MathF.Sqrt(l2) : 0f;
        q = new Quaternion(q.X * k, q.Y * k, q.Z * k, q.W * k);
        float w = q.W, aw = MathF.Abs(w);
        if (!(aw < 1f))
        {
            angle = 0;
            axis = Vector3.UnitY;
            return;
        }
        float rest = 1f - aw;
        if (rest < 0f) rest = 0f;
        float theta = (((aw * -0.0187293f + 0.074261f) * aw - 0.2121144f) * aw + 1.5707288f) * MathF.Sqrt(rest);
        if (w < 0f) theta = 3.1415927f - theta;
        int half = CamMath.Trunc(theta * CamMath.RadToUnit);
        angle = CamMath.Trunc((float)half + half);
        float s = 1f / MathF.Sqrt(1f - w * w);
        var a = new Vector4(q.X * s, q.Y * s, q.Z * s, 1f);
        float ka = CamMath.InvLengthGuarded_11C030(Eps, a);
        axis = new Vector3(a.X * ka, a.Y * ka, a.Z * ka);
    }
}
