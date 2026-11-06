using System.Numerics;

namespace CrashEngine.Core;

public static partial class GameMath
{
    // Amedo
    public static Vector4 RotateAxis_18DE00(Vector4 v, Vector4 axis, int angle, bool normalized)
    {
        if (angle == 0) return v;
        var (s, c) = SinCos(angle * UnitToRad);
        return Rodrigues_1881C0(s, v, axis, normalized, c);
    }

    public static Vector4 Rodrigues_1881C0(float s, in Vector4 v, in Vector4 axis, bool normalized, float c)
    {
        float ax = axis.X, ay = axis.Y, az = axis.Z, n = 1f;
        if (!normalized)
        {
            n = ax * ax + ay * ay + az * az;
            n = 2.4999998e-09f < n ? 1f / MathF.Sqrt(n) : 0f;
            ax *= n; ay *= n; az *= n;
        }
        if (!(0f < n)) return v;
        float dot = (v.X * ax + v.Y * ay + v.Z * az) * (1f - c);
        return new Vector4((ay * v.Z - az * v.Y) * s + ax * dot + v.X * c,
                           (az * v.X - ax * v.Z) * s + ay * dot + v.Y * c,
                           (ax * v.Y - ay * v.X) * s + az * dot + v.Z * c, v.W);
    }

    // Amedo 2026-09-28
    public static Matrix4x4 RigidInverse_18E808(Matrix4x4 m)
    {
        float tx = m.M41, ty = m.M42, tz = m.M43;
        return new Matrix4x4(m.M11, m.M21, m.M31, 0f, m.M12, m.M22, m.M32, 0f, m.M13, m.M23, m.M33, 0f,
                             -(m.M11 * tx + m.M12 * ty + m.M13 * tz), -(m.M21 * tx + m.M22 * ty + m.M23 * tz),
                             -(m.M31 * tx + m.M32 * ty + m.M33 * tz), m.M44);
    }

    public static Vector4 Rotate3_18E4B8(Matrix4x4 m, Vector4 v) =>
        new(m.M11 * v.X + m.M21 * v.Y + m.M31 * v.Z, m.M12 * v.X + m.M22 * v.Y + m.M32 * v.Z,
            m.M13 * v.X + m.M23 * v.Y + m.M33 * v.Z, v.W);

    public static Vector4 Transform_18E4F0(Matrix4x4 m, Vector4 v) =>
        new(m.M11 * v.X + m.M21 * v.Y + m.M31 * v.Z + m.M41, m.M12 * v.X + m.M22 * v.Y + m.M32 * v.Z + m.M42,
            m.M13 * v.X + m.M23 * v.Y + m.M33 * v.Z + m.M43, m.M14 * v.X + m.M24 * v.Y + m.M34 * v.Z + m.M44);

    public static Matrix4x4 RotY_18BBC0(int yaw)
    {
        if (yaw == 0) return Matrix4x4.Identity;
        var (s, c) = SinCos(yaw * UnitToRad);
        return new Matrix4x4(c, 0f, -s, 0f, 0f, 1f, 0f, 0f, s, 0f, c, 0f, 0f, 0f, 0f, 1f);
    }

    public static Matrix4x4 RotXY_182AD0(int pitch, int yaw)
    {
        if (pitch == 0)
        {
            if (yaw == 0) return Matrix4x4.Identity;
            var (s, c) = SinCos(yaw * UnitToRad);
            return new Matrix4x4(c, 0f, -s, 0f, 0f, 1f, 0f, 0f, s, 0f, c, 0f, 0f, 0f, 0f, 1f);
        }
        if (yaw == 0)
        {
            var (s, c) = SinCos(pitch * UnitToRad);
            return new Matrix4x4(1f, 0f, 0f, 0f, 0f, c, s, 0f, 0f, -s, c, 0f, 0f, 0f, 0f, 1f);
        }
        var (sy, cy) = SinCos(yaw * UnitToRad);
        var (sp, cp) = SinCos(pitch * UnitToRad);
        return new Matrix4x4(cy, 0f, -sy, 0f, sy * sp, cp, sp * cy, 0f, sy * cp, -sp, cy * cp, 0f, 0f, 0f, 0f, 1f);
    }

    public static Matrix4x4 RotXYZ_183220(int pitch, int yaw, int roll, Action<string>? unimplemented = null)
    {
        if (roll == 0) return RotXY_182AD0(pitch, yaw);
        unimplemented?.Invoke("0x183220 with roll");
        return RotXY_182AD0(pitch, yaw);
    }

    public static Quaternion MatrixToQuat_186900(Matrix4x4 m)
    {
        float tr = m.M11 + m.M22 + m.M33;
        if (0f < tr)
        {
            float s = MathF.Sqrt(tr + 1f), f = 0.5f / s;
            return new Quaternion((m.M23 - m.M32) * f, (m.M31 - m.M13) * f, (m.M12 - m.M21) * f, s * 0.5f);
        }
        Span<float> e = stackalloc float[16];
        e[0] = m.M11; e[1] = m.M12; e[2] = m.M13; e[3] = m.M14;
        e[4] = m.M21; e[5] = m.M22; e[6] = m.M23; e[7] = m.M24;
        e[8] = m.M31; e[9] = m.M32; e[10] = m.M33; e[11] = m.M34;
        Span<int> next = [1, 2, 0];
        Span<float> q = stackalloc float[4];
        int i = m.M11 < m.M22 ? 1 : 0;
        if (e[i * 5] < m.M33) i = 2;
        int j = next[i], k = next[j];
        float r = MathF.Sqrt(e[i * 5] - (e[j * 5] + e[k * 5]) + 1f);
        q[i] = r * 0.5f;
        if (r != 0f) r = 0.5f / r;
        q[3] = (e[j * 4 + k] - e[k * 4 + j]) * r;
        q[j] = (e[i * 4 + j] + e[j * 4 + i]) * r;
        q[k] = (e[i * 4 + k] + e[k * 4 + i]) * r;
        return new Quaternion(q[0], q[1], q[2], q[3]);
    }

    public static Matrix4x4 QuatToMatrix_18E620(Quaternion q) => Matrix4x4.CreateFromQuaternion(q);

    // Amedo
    public static Matrix4x4 MatrixAboutAxis_185058(Vector4 axis, bool normalized, float s, float c)
    {
        float x = axis.X, y = axis.Y, z = axis.Z;
        if (!normalized)
        {
            float lenSq = x * x + y * y + z * z;
            float inv = 2.4999998e-09f < lenSq ? 1f / MathF.Sqrt(lenSq) : 0f;
            z *= inv;
            x *= inv;
            y *= inv;
        }
        float t = 1f - c;
        float tx = t * x, tz = t * z, ty = t * y;
        float txz = tx * z, txy = tx * y, tyz = ty * z, tzz = tz * z, txx = tx * x, tyy = ty * y;
        float sz = s * z, sx = s * x, sy = s * y;
        return new Matrix4x4(txx + c, txy + sz, txz - sy, 0f,
                             txy - sz, tyy + c, tyz + sx, 0f,
                             txz + sy, tyz - sx, tzz + c, 0f,
                             0f, 0f, 0f, 1f);
    }

    // Amedo
    public static Matrix4x4 MatrixBetween_185480(Vector4 a, Vector4 b)
    {
        var axis = new Vector4(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X, a.W);
        float lenSq = axis.X * axis.X + axis.Y * axis.Y + axis.Z * axis.Z;
        float dot = a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        if (0f < lenSq)
        {
            float la = a.X * a.X + a.Y * a.Y + a.Z * a.Z;
            float lb = b.X * b.X + b.Y * b.Y + b.Z * b.Z;
            float k = MathF.Sqrt(1f / (la * lb));
            return MatrixAboutAxis_185058(axis, false, MathF.Sqrt(lenSq) * k, dot * k);
        }
        if (!(dot * MathF.Abs(dot) < 0f)) return Matrix4x4.Identity;
        float laa = a.X * a.X + a.Y * a.Y + a.Z * a.Z;
        float lbb = b.X * b.X + b.Y * b.Y + b.Z * b.Z;
        var from = lbb < laa ? a : b;
        var perp = 1e-20f < from.X * from.X + from.Y * from.Y + from.Z * from.Z
            ? PerpendicularOf_18DA40(from)
            : new Vector4(0f, 1f, 0f, 1f);
        return MatrixAboutAxis_185058(perp, false, 0f, -1f);
    }

    public static Vector4 RotateByQuat_186D98(Quaternion q, Vector4 v, bool normalized)
    {
        if (!normalized)
        {
            float l = q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W;
            float inv = 1.00000005e-20f < l ? 1f / l : 0f;
            q = new Quaternion(q.X * inv, q.Y * inv, q.Z * inv, q.W * inv);
        }
        float x = q.X, y = q.Y, z = q.Z, w = q.W;
        float y2 = y + y, z2 = z + z, wx2 = w * (x + x), xx2 = x * (x + x);
        return new Vector4(
            v.X * ((1f - y * y2) - z * z2) + v.Y * (x * y2 - w * z2) + v.Z * (x * z2 + w * y2),
            v.X * (x * y2 + w * z2) + v.Y * ((1f - xx2) - z * z2) + v.Z * (y * z2 - wx2),
            v.X * (x * z2 - w * y2) + v.Y * (y * z2 + wx2) + v.Z * ((1f - xx2) - y * y2),
            v.W);
    }

    public static Quaternion Mul_18D558(Quaternion a, Quaternion b) => Quaternion.Multiply(a, b);

    // Amedo
    public static Quaternion RotationX_18D590(int units)
    {
        if (units == 0) return Quaternion.Identity;
        var (s, c) = SinCos(units * UnitToRad * 0.5f);
        return new Quaternion(s, 0f, 0f, c);
    }

    // Amedo
    public static Quaternion RotationY_18D620(int units)
    {
        if (units == 0) return Quaternion.Identity;
        var (s, c) = SinCos(units * UnitToRad * 0.5f);
        return new Quaternion(0f, s, 0f, c);
    }

    // Amedo
    public static Quaternion RotationZ_18D6B0(int units)
    {
        if (units == 0) return Quaternion.Identity;
        var (s, c) = SinCos(units * UnitToRad * 0.5f);
        return new Quaternion(0f, 0f, s, c);
    }

    // Amedo
    public static Vector3 Slerp_188300(float t, Vector3 a, Vector3 b)
    {
        float dot = a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        float ax = MathF.Abs(dot), rest = 1f - ax;
        if (rest < 0f) rest = 0f;
        float angle = (((ax * -0.0187293f + 0.074261f) * ax - 0.2121144f) * ax + 1.5707288f) * MathF.Sqrt(rest);
        if (dot < 0f) angle = 3.1415927f - angle;
        float s = MathF.Sin(angle);
        if (s == 0f) return b;
        float inv = 1f / s;
        return a * (inv * MathF.Sin(angle * (1f - t))) + b * (inv * MathF.Sin(angle * t));
    }

    // Amedo
    public static void Frame_1879D0(Vector3 f, out Vector3 right, out Vector3 up)
    {
        float k = f.Y * f.Y - 1f;
        if (k == 0f)
        {
            right = 0f < f.Y ? Vector3.UnitZ : -Vector3.UnitX;
            up = 0f < f.Y ? Vector3.UnitX : -Vector3.UnitZ;
            return;
        }
        up = new Vector3(-(f.X * f.Y), -k, -(f.Y * f.Z));
        right = new Vector3(up.Y * f.Z - up.Z * f.Y, up.Z * f.X - up.X * f.Z, up.X * f.Y - up.Y * f.X);
        up *= InverseLength(up);
        right *= InverseLength(right);
    }

    // Amedo
    public static void EulerAngles_185180(Matrix4x4 m, out int a, out int b, out int c)
    {
        const float e = 0.0005f;
        if (MathF.Abs(m.M12) <= e && MathF.Abs(m.M21) <= e && MathF.Abs(m.M23) <= e && MathF.Abs(m.M32) <= e
            && MathF.Abs(1f - m.M22) <= e && MathF.Abs(m.M11 - m.M33) <= e && MathF.Abs(m.M13 + m.M31) <= e)
        {
            b = Atan2_182918(m.M31, m.M11);
            c = FromValue_11BFA0(0f, 0);
            a = c;
            return;
        }
        float x = -m.M13, ax = MathF.Abs(x);
        float rest = 1f - ax;
        if (rest < 0f) rest = 0f;
        float s = 1.5707964f - MathF.Sqrt(rest) * (((ax * -0.0187293f + 0.074261f) * ax - 0.2121144f) * ax + 1.5707288f);
        if (x < 0f) s = -s;
        b = Trunc(s * RadToUnit);
        if (b < 0x4000)
        {
            if (-0x4000 < b)
            {
                c = Atan2_182918(m.M12, m.M11);
                a = Atan2_182918(m.M23, m.M33);
            }
            else
            {
                c = Atan2_182918(-m.M21, -m.M31);
                a = FromValue_11BFA0(0f, 0);
            }
        }
        else
        {
            c = -Atan2_182918(m.M21, m.M31);
            a = FromValue_11BFA0(0f, 0);
        }
    }

    // Amedo
    public static void AnglesOfRotation_18D740(Quaternion q, out int pitch, out int yaw, out int roll) =>
        EulerAngles_185180(QuatToMatrix_18E620(q), out pitch, out yaw, out roll);

    // Amedo
    public static int VectorAngle_187BC8(Vector3 a, Vector3 b)
    {
        float len = MathF.Sqrt((a.X * a.X + a.Y * a.Y + a.Z * a.Z) * (b.X * b.X + b.Y * b.Y + b.Z * b.Z));
        float k = len == 0f ? len : 1f / len;
        float cos = (a.X * b.X + a.Y * b.Y + a.Z * b.Z) * k;
        float ac = MathF.Abs(cos);
        float rest = 1f - ac;
        if (rest < 0f) rest = 0f;
        float theta = (((ac * -0.0187293f + 0.074261f) * ac - 0.2121144f) * ac + 1.5707288f) * MathF.Sqrt(rest);
        if (cos < 0f) theta = 3.1415927f - theta;
        int angle = Trunc(theta * RadToUnit);
        if (angle == 0 || angle == 0x8000) return angle;
        const float eps = 5e-05f;
        float cx = a.Y * b.Z - a.Z * b.Y, cy = a.Z * b.X - a.X * b.Z, cz = a.X * b.Y - a.Y * b.X;
        if (MathF.Abs(cx) <= eps && MathF.Abs(cy) <= eps && MathF.Abs(cz) <= eps)
            return angle < 0x4000 ? 0 : Trunc(3.1415927f * RadToUnit);
        float s = !(MathF.Abs(cy) <= eps) ? cy : !(MathF.Abs(cx) <= eps) ? cx : cz;
        return Trunc(angle * (s < 0f ? -1f : 1f));
    }

    // Amedo 2026-09-29
    public static Quaternion AxisAngle_18D798(Vector3 axis, int angle)
    {
        float half = Trunc(angle * 0.5f) * UnitToRad;
        var (s, cos) = SinCos(half);
        float len = axis.X * axis.X + axis.Y * axis.Y + axis.Z * axis.Z;
        s = 2.4999998e-09f < len ? s * (1f / MathF.Sqrt(len)) : s * 0f;
        return new Quaternion(s * axis.X, s * axis.Y, s * axis.Z, cos);
    }

    public static Matrix4x4 AxisAngle_18CEA8(Vector3 axis, int angle) // Amedo
    {
        if (angle == 0) return Matrix4x4.Identity;
        var (s, c) = GameMath.SinCos(angle * GameMath.UnitToRad);
        float x = axis.X, y = axis.Y, z = axis.Z;
        float t = 1f - c;
        float tx = t * x, tz = t * z, ty = t * y;
        float txz = tx * z, txy = tx * y, tyz = ty * z, tzz = tz * z, txx = tx * x, tyy = ty * y;
        float sz = s * z, sx = s * x, sy = s * y;
        return new Matrix4x4(txx + c, txy + sz, txz - sy, 0f,
                             txy - sz, tyy + c, tyz + sx, 0f,
                             txz + sy, tyz - sx, tzz + c, 0f,
                             0f, 0f, 0f, 1f);
    }

    public static int AxisAngle_186B50(ref Quaternion q, out Vector3 axis)
    {
        float l = q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W;
        float k = 1.00000005e-20f < l ? 1f / MathF.Sqrt(l) : 0f;
        q = new Quaternion(q.X * k, q.Y * k, q.Z * k, q.W * k);
        float aw = MathF.Abs(q.W);
        if (!(aw < 1f))
        {
            axis = Vector3.UnitY;
            return 0;
        }
        float rest = 1f - aw;
        if (rest < 0f) rest = 0f;
        float a = (((aw * -0.0187293f + 0.074261f) * aw - 0.2121144f) * aw + 1.5707288f) * MathF.Sqrt(rest);
        if (q.W < 0f) a = 3.1415927f - a;
        int half = Trunc(a * RadToUnit);
        float s = 1f / MathF.Sqrt(1f - q.W * q.W);
        axis = new Vector3(q.X * s, q.Y * s, q.Z * s);
        axis *= InverseLength(axis);
        return Trunc((float)half + half);
    }

    // Amedo
    public static Quaternion Slerp_187100(float t, in Quaternion a, in Quaternion b)
    {
        float dot = a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;
        bool neg = dot < 0f;
        if (neg) dot = -dot;
        if (0.05f < 1f - dot)
        {
            float ad = MathF.Abs(dot), rest = 1f - ad;
            if (rest < 0f) rest = 0f;
            float theta = (((ad * -0.0187293f + 0.074261f) * ad - 0.2121144f) * ad + 1.5707288f) * MathF.Sqrt(rest);
            if (dot < 0f) theta = 3.1415927f - theta;
            float inv = 1f / MathF.Sin(theta);
            float tt = theta * t;
            float s1 = MathF.Sin(tt) * inv;
            float s0 = MathF.Sin(tt - theta) * inv;
            if (!neg) s0 = -s0;
            return new Quaternion(a.X * s0 + b.X * s1, a.Y * s0 + b.Y * s1, a.Z * s0 + b.Z * s1, a.W * s0 + b.W * s1);
        }
        var p = neg ? new Quaternion(-a.X, -a.Y, -a.Z, -a.W) : a;
        if (!(0f < t)) return p;
        if (!(t < 1f)) return b;
        var l = new Quaternion((b.X - p.X) * t + p.X, (b.Y - p.Y) * t + p.Y, (b.Z - p.Z) * t + p.Z, (b.W - p.W) * t + p.W);
        float k = QuatInvLength_11C0B8(l, 0f, 1e-10f);
        return new Quaternion(l.X * k, l.Y * k, l.Z * k, l.W * k);
    }

    // Amedo
    public static float QuatInvLength_11C0B8(Quaternion q, float fallback, float eps)
    {
        float l = q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W;
        return eps * eps < l ? 1f / MathF.Sqrt(l) : fallback;
    }

    public static bool QuatNear_1861A0(in Quaternion a, in Quaternion b, float tol)
    {
        bool same = MathF.Abs(a.X - b.X) <= tol && MathF.Abs(a.Y - b.Y) <= tol && MathF.Abs(a.Z - b.Z) <= tol && MathF.Abs(a.W - b.W) <= tol;
        bool flipped = MathF.Abs(a.X + b.X) <= tol && MathF.Abs(a.Y + b.Y) <= tol && MathF.Abs(a.Z + b.Z) <= tol && MathF.Abs(a.W + b.W) <= tol;
        return same || flipped;
    }

    public static Matrix4x4 ScaleColumns_183DE0(Matrix4x4 m, Vector3 s)
    {
        m.M11 *= s.X; m.M12 *= s.Y; m.M13 *= s.Z;
        m.M21 *= s.X; m.M22 *= s.Y; m.M23 *= s.Z;
        m.M31 *= s.X; m.M32 *= s.Y; m.M33 *= s.Z;
        m.M41 *= s.X; m.M42 *= s.Y; m.M43 *= s.Z;
        return m;
    }

    public static Matrix4x4 WithRotation_18E620(Matrix4x4 m, Quaternion q)
    {
        var r = QuatToMatrix_18E620(q);
        r.M41 = m.M41; r.M42 = m.M42; r.M43 = m.M43; r.M44 = m.M44;
        return r;
    }

    public static int AngleBetween_187EE0(Vector4 a, Vector4 b, int axis)
    {
        float len = MathF.Sqrt(LengthSq3(a) * LengthSq3(b));
        if (len != 0f) len = 1f / len;
        float c = (a.X * b.X + a.Y * b.Y + a.Z * b.Z) * len;
        float ac = MathF.Abs(c), rest = 1f - ac;
        if (rest < 0f) rest = 0f;
        float ang = (((ac * -0.0187293f + 0.074261f) * ac - 0.2121144f) * ac + 1.5707288f) * MathF.Sqrt(rest);
        if (c < 0f) ang = 3.1415927f - ang;
        int units = Trunc(ang * RadToUnit);
        if (units == 0 || units == 0x8000) return units;
        float cx = a.Y * b.Z - a.Z * b.Y, cy = a.Z * b.X - a.X * b.Z, cz = a.X * b.Y - a.Y * b.X;
        if (MathF.Abs(cx) <= 5e-5f && MathF.Abs(cy) <= 5e-5f && MathF.Abs(cz) <= 5e-5f)
            return 0x3FFF < units ? FromValue_11BFA0(3.1415927f, 0) : 0;
        float pick = axis switch { 0 => cx, 1 => cy, _ => cz };
        if (5e-5f < MathF.Abs(pick)) return Trunc(units * (pick < 0f ? -1f : 1f));
        return 0;
    }

    public static int Yaw_18C830(Vector4 v) => AngleBetween_187EE0(new Vector4(0f, 0f, 1f, 1f), new Vector4(v.X, 0f, v.Z, 1f), 1);
    public static int Pitch_18C898(Vector4 v) => AngleBetween_187EE0(new Vector4(0f, 0f, 1f, 1f), new Vector4(0f, v.Y, v.Z, 1f), 0);

    public static bool Parallel_18DE78(float eps, in Vector4 a, in Vector4 b, out float dot)
    {
        dot = a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        return MathF.Abs(LengthSq3(a) * LengthSq3(b) - dot * dot) <= eps * eps * dot * dot;
    }

    public static Matrix4x4 LookAt_185668(Vector4 eye, Vector4 target, Vector4? up)
    {
        var f = target - eye;
        float k = InvLength_11C030(2.4999998e-09f, f);
        f = new Vector4(f.X * k, f.Y * k, f.Z * k, f.W);
        var u = up ?? new Vector4(0f, 1f, 0f, 1f);
        var r = new Vector4(u.Y * f.Z - u.Z * f.Y, u.Z * f.X - u.X * f.Z, u.X * f.Y - u.Y * f.X, 0f);
        k = InvLength_11C030(2.4999998e-09f, r);
        r = new Vector4(r.X * k, r.Y * k, r.Z * k, 0f);
        var n = new Vector4(f.Y * r.Z - f.Z * r.Y, f.Z * r.X - f.X * r.Z, f.X * r.Y - f.Y * r.X, 1f);
        k = InvLength_11C030(2.4999998e-09f, n);
        n = new Vector4(n.X * k, n.Y * k, n.Z * k, 0f);
        return new Matrix4x4(r.X, r.Y, r.Z, 0f, n.X, n.Y, n.Z, 0f, f.X, f.Y, f.Z, 0f,
                             -(n.X * eye.X + n.Y * eye.Y + n.Z * eye.Z), -(r.X * eye.X + r.Y * eye.Y + r.Z * eye.Z),
                             f.X * eye.X + f.Y * eye.Y + f.Z * eye.Z, 1f);
    }

    public static Quaternion EulerXY_1862A8(int pitch, int yaw)
    {
        if (pitch == 0)
        {
            if (yaw == 0) return Quaternion.Identity;
            var (s, c) = SinCos(yaw * UnitToRad * 0.5f);
            return new Quaternion(0f, s, 0f, c);
        }
        if (yaw == 0)
        {
            var (s, c) = SinCos(pitch * UnitToRad * 0.5f);
            return new Quaternion(s, 0f, 0f, c);
        }
        var (sp, cp) = SinCos(pitch * UnitToRad * 0.5f);
        var (sy, cy) = SinCos(yaw * UnitToRad * 0.5f);
        return new Quaternion(sp * cy, cp * sy, -sp * sy, cp * cy);
    }

    public static Quaternion Euler_186780(int pitch, int yaw, int roll, Action<string>? unimplemented = null)
    {
        if (roll == 0) return EulerXY_1862A8(pitch, yaw);
        // Amedo 2026-09-28
        var (sx, cx) = SinCos(pitch * UnitToRad * 0.5f);
        var (sy, cy) = SinCos(yaw * UnitToRad * 0.5f);
        var (sz, cz) = SinCos(roll * UnitToRad * 0.5f);
        return new Quaternion(sx * cy * cz - cx * sy * sz, cx * sy * cz + sx * cy * sz,
                              cx * cy * sz - sx * sy * cz, cx * cy * cz + sx * sy * sz);
    }

    public static Quaternion Arc_1875E8(Vector4 a, Vector4 b)
    {
        float cx = a.Y * b.Z - a.Z * b.Y, cy = a.Z * b.X - a.X * b.Z, cz = a.X * b.Y - a.Y * b.X;
        float cross2 = cx * cx + cy * cy + cz * cz;
        float dot = a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        if (!(0f < cross2))
        {
            if (!(dot * MathF.Abs(dot) < 0f)) return Quaternion.Identity;
            var from = LengthSq3(b) < LengthSq3(a) ? a : b;
            var p = 1e-20f < LengthSq3(from) ? PerpendicularOf_18DA40(from) : new Vector4(0f, 1f, 0f, 1f);
            float l = LengthSq3(p);
            float inv = 2.4999998e-09f < l ? 1f / MathF.Sqrt(l) : 0f;
            return new Quaternion(inv * p.X, inv * p.Y, inv * p.Z, 0f);
        }
        float n = LengthSq3(a) * LengthSq3(b);
        float invN = (1e-10f < n || n < -1e-10f) ? 1f / n : 0f;
        float cos = dot * MathF.Sqrt(invN);
        float w = MathF.Sqrt(cross2) * MathF.Sqrt(invN) < 0f ? -MathF.Sqrt((cos + 1f) * 0.5f) : MathF.Sqrt(cos * 0.5f + 0.5f);
        float s = MathF.Sqrt((1f - cos) * 0.5f);
        s *= 2.4999998e-09f < cross2 ? 1f / MathF.Sqrt(cross2) : 0f;
        return new Quaternion(s * cx, s * cy, s * cz, w);
    }

    public static Vector4 PerpendicularOf_18DA40(Vector4 v)
    {
        float xx = v.X * v.X, zz = v.Z * v.Z;
        if (!(1e-20f < xx + v.Y * v.Y + zz)) return v;
        if (zz <= xx) return new Vector4(-v.Y * 3f, v.X * 3f, 0f, 1f);
        return new Vector4(0f, v.Z * 3f, -v.Y * 3f, 1f);
    }

    public static Vector3 PerpendicularOf_18DA40(Vector3 v)
    {
        var p = PerpendicularOf_18DA40(new Vector4(v, 1f));
        return new Vector3(p.X, p.Y, p.Z);
    }

    public static Vector4 PlaneFromTriangle_185DC0(in Vector4 a, in Vector4 b, in Vector4 c)
    {
        var u = new Vector4(b.X - a.X, b.Y - a.Y, b.Z - a.Z, b.W);
        var v = new Vector4(c.X - a.X, c.Y - a.Y, c.Z - a.Z, c.W);
        var n = new Vector4(u.Y * v.Z - u.Z * v.Y, u.Z * v.X - u.X * v.Z, u.X * v.Y - u.Y * v.X, 1f);
        if (Small(n))
        {
            if (!Small(u)) n = PerpendicularOf_18DA40(u);
            else if (!Small(v)) n = PerpendicularOf_18DA40(v);
            else n = new Vector4(0f, 1f, 0f, 1f);
        }
        float k = InvLength_11C030(2.4999998e-09f, n);
        float x = n.X * k, y = n.Y * k, z = n.Z * k;
        return new Vector4(x, y, z, -(x * a.X + y * a.Y + z * a.Z));
    }

    public static Vector4 PlaneFromTriangle_185DC0(in Vector3 a, in Vector3 b, in Vector3 c) =>
        PlaneFromTriangle_185DC0(new Vector4(a, 1f), new Vector4(b, 1f), new Vector4(c, 1f));

    public static int PlaneSideOf_18D1D8(in Vector4 plane, in Vector3 point)
    {
        float d = plane.X * point.X + plane.Y * point.Y + plane.Z * point.Z + plane.W;
        return 0f < d ? 1 : d < 0f ? 3 : 2;
    }

    public static Vector4 FacePlaneTowards_200E98(in Vector4 plane, in Vector3 point) =>
        PlaneSideOf_18D1D8(plane, point) == 3 ? new Vector4(-plane.X, -plane.Y, -plane.Z, -plane.W) : plane;

    private static bool Small(Vector4 v) => MathF.Abs(v.X) <= 5e-05f && MathF.Abs(v.Y) <= 5e-05f && MathF.Abs(v.Z) <= 5e-05f;

    public static bool PlaneThroughEdge_186050(out Vector4 plane, in Vector4 a, in Vector4 b, in Vector4 dir)
    {
        float ex = b.X - a.X, ey = b.Y - a.Y, ez = b.Z - a.Z;
        float nx = ey * dir.Z - ez * dir.Y;
        float ny = ez * dir.X - ex * dir.Z;
        float nz = ex * dir.Y - ey * dir.X;
        float l = nx * nx + ny * ny + nz * nz;
        float inv = 2.4999998e-09f < l ? 1f / MathF.Sqrt(l) : 0f;
        if (inv == 0f)
        {
            plane = new Vector4(nx, ny, nz, b.W);
            return false;
        }
        nx *= inv;
        ny *= inv;
        nz *= inv;
        plane = new Vector4(nx, ny, nz, -(nx * a.X + ny * a.Y + nz * a.Z));
        return true;
    }
}
