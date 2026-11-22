using System.Numerics;

namespace CrashEngine.Core;

public static partial class GameMath
{
    // Amedo
    public static Vector4 RotateAxis_18DE00(Vector4 v, Vector4 axis, int angle, bool normalized)
    {
        if (angle == 0) return v;
        var (s, c) = SinCos(Ps2Mul(angle, UnitToRad));
        return Rodrigues_1881C0(s, v, axis, normalized, c);
    }

    public static Vector4 Rodrigues_1881C0(float s, in Vector4 v, in Vector4 axis, bool normalized, float c)
    {
        float ax = axis.X, ay = axis.Y, az = axis.Z, n = 1f;
        if (!normalized)
        {
            // Amedo 2026-11-21
            n = Ps2Add(Ps2Add(Ps2Mul(ax, ax), Ps2Mul(ay, ay)), Ps2Mul(az, az));
            n = 2.4999998e-09f < n ? Ps2Div(1f, Ps2Sqrt(n)) : 0f;
            ax = Ps2Mul(ax, n); ay = Ps2Mul(ay, n); az = Ps2Mul(az, n);
        }
        if (!(0f < n)) return v;
        float dot = Ps2Mul(Ps2Add(Ps2Add(Ps2Mul(v.X, ax), Ps2Mul(v.Y, ay)), Ps2Mul(v.Z, az)), Ps2Sub(1f, c));
        return new Vector4(Ps2Add(Ps2Add(Ps2Mul(Ps2Sub(Ps2Mul(ay, v.Z), Ps2Mul(az, v.Y)), s), Ps2Mul(ax, dot)), Ps2Mul(v.X, c)),
                           Ps2Add(Ps2Add(Ps2Mul(Ps2Sub(Ps2Mul(az, v.X), Ps2Mul(ax, v.Z)), s), Ps2Mul(ay, dot)), Ps2Mul(v.Y, c)),
                           Ps2Add(Ps2Add(Ps2Mul(Ps2Sub(Ps2Mul(ax, v.Y), Ps2Mul(ay, v.X)), s), Ps2Mul(az, dot)), Ps2Mul(v.Z, c)), v.W);
    }

    // Amedo 2026-09-28
    public static Matrix4x4 RigidInverse_18E808(Matrix4x4 m)
    {
        // Amedo 2026-11-21
        float tx = -m.M41, ty = -m.M42, tz = -m.M43;
        return new Matrix4x4(m.M11, m.M21, m.M31, 0f, m.M12, m.M22, m.M32, 0f, m.M13, m.M23, m.M33, 0f,
                             Ps2Add(Ps2Add(Ps2Mul(m.M11, tx), Ps2Mul(m.M12, ty)), Ps2Mul(m.M13, tz)),
                             Ps2Add(Ps2Add(Ps2Mul(m.M21, tx), Ps2Mul(m.M22, ty)), Ps2Mul(m.M23, tz)),
                             Ps2Add(Ps2Add(Ps2Mul(m.M31, tx), Ps2Mul(m.M32, ty)), Ps2Mul(m.M33, tz)), 1f);
    }

    // Amedo 2026-11-21
    public static Vector4 Rotate3_18E4B8(Matrix4x4 m, Vector4 v) =>
        new(Ps2Add(Ps2Add(Ps2Mul(m.M11, v.X), Ps2Mul(m.M21, v.Y)), Ps2Mul(m.M31, v.Z)),
            Ps2Add(Ps2Add(Ps2Mul(m.M12, v.X), Ps2Mul(m.M22, v.Y)), Ps2Mul(m.M32, v.Z)),
            Ps2Add(Ps2Add(Ps2Mul(m.M13, v.X), Ps2Mul(m.M23, v.Y)), Ps2Mul(m.M33, v.Z)), v.W);

    // Amedo 2026-11-21
    public static Vector4 Transform_18E4F0(Matrix4x4 m, Vector4 v) =>
        new(Ps2Add(Ps2Add(Ps2Add(Ps2Mul(m.M11, v.X), Ps2Mul(m.M21, v.Y)), Ps2Mul(m.M31, v.Z)), m.M41),
            Ps2Add(Ps2Add(Ps2Add(Ps2Mul(m.M12, v.X), Ps2Mul(m.M22, v.Y)), Ps2Mul(m.M32, v.Z)), m.M42),
            Ps2Add(Ps2Add(Ps2Add(Ps2Mul(m.M13, v.X), Ps2Mul(m.M23, v.Y)), Ps2Mul(m.M33, v.Z)), m.M43),
            Ps2Add(Ps2Add(Ps2Add(Ps2Mul(m.M14, v.X), Ps2Mul(m.M24, v.Y)), Ps2Mul(m.M34, v.Z)), m.M44));

    public static Matrix4x4 RotY_18BBC0(int yaw)
    {
        if (yaw == 0) return Matrix4x4.Identity;
        var (s, c) = SinCos(Ps2Mul(yaw, UnitToRad));
        return new Matrix4x4(c, 0f, -s, 0f, 0f, 1f, 0f, 0f, s, 0f, c, 0f, 0f, 0f, 0f, 1f);
    }

    public static Matrix4x4 RotXY_182AD0(int pitch, int yaw)
    {
        if (pitch == 0)
        {
            if (yaw == 0) return Matrix4x4.Identity;
            var (s, c) = SinCos(Ps2Mul(yaw, UnitToRad));
            return new Matrix4x4(c, 0f, -s, 0f, 0f, 1f, 0f, 0f, s, 0f, c, 0f, 0f, 0f, 0f, 1f);
        }
        if (yaw == 0)
        {
            var (s, c) = SinCos(Ps2Mul(pitch, UnitToRad));
            return new Matrix4x4(1f, 0f, 0f, 0f, 0f, c, s, 0f, 0f, -s, c, 0f, 0f, 0f, 0f, 1f);
        }
        var (sy, cy) = SinCos(Ps2Mul(yaw, UnitToRad));
        var (sp, cp) = SinCos(Ps2Mul(pitch, UnitToRad));
        return new Matrix4x4(cy, 0f, -sy, 0f, Ps2Mul(sy, sp), cp, Ps2Mul(sp, cy), 0f, Ps2Mul(sy, cp), -sp, Ps2Mul(cp, cy), 0f, 0f, 0f, 0f, 1f); // Amedo 2026-11-21
    }

    public static Matrix4x4 RotXYZ_183220(int pitch, int yaw, int roll, Action<string>? unimplemented = null)
    {
        if (roll == 0) return RotXY_182AD0(pitch, yaw);
        unimplemented?.Invoke("0x183220 with roll");
        return RotXY_182AD0(pitch, yaw);
    }

    public static Quaternion MatrixToQuat_186900(Matrix4x4 m)
    {
        float tr = Ps2Add(Ps2Add(m.M11, m.M22), m.M33); // Amedo 2026-11-21
        if (0f < tr)
        {
            float s = Ps2Sqrt(Ps2Add(tr, 1f)), f = Ps2Div(0.5f, s);
            return new Quaternion(Ps2Mul(Ps2Sub(m.M23, m.M32), f), Ps2Mul(Ps2Sub(m.M31, m.M13), f), Ps2Mul(Ps2Sub(m.M12, m.M21), f), Ps2Mul(s, 0.5f));
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
        float r = Ps2Sqrt(Ps2Add(Ps2Sub(e[i * 5], Ps2Add(e[j * 5], e[k * 5])), 1f));
        q[i] = Ps2Mul(r, 0.5f);
        if (r != 0f) r = Ps2Div(0.5f, r);
        q[3] = Ps2Mul(Ps2Sub(e[j * 4 + k], e[k * 4 + j]), r);
        q[j] = Ps2Mul(Ps2Add(e[i * 4 + j], e[j * 4 + i]), r);
        q[k] = Ps2Mul(Ps2Add(e[i * 4 + k], e[k * 4 + i]), r);
        return new Quaternion(q[0], q[1], q[2], q[3]);
    }

    // Amedo 2026-11-21
    public static Matrix4x4 QuatToMatrix_18E620(Quaternion q)
    {
        float k = BitConverter.Int32BitsToSingle(0x3FB504F3);
        float xx = Ps2Mul(q.X, q.X), yy = Ps2Mul(q.Y, q.Y), zz = Ps2Mul(q.Z, q.Z);
        float x = Ps2Mul(q.X, k), y = Ps2Mul(q.Y, k), z = Ps2Mul(q.Z, k), w = Ps2Mul(q.W, k);
        float x2 = Ps2Add(xx, xx), y2 = Ps2Add(yy, yy), z2 = Ps2Add(zz, zz);
        float yz = Ps2Mul(y, z), zx = Ps2Mul(z, x), xy = Ps2Mul(x, y);
        float yzm = Ps2Sub(yz, Ps2Mul(x, w)), zxm = Ps2Sub(zx, Ps2Mul(y, w)), xym = Ps2Sub(xy, Ps2Mul(z, w));
        float yzp = Ps2Add(yz, Ps2Mul(x, w)), zxp = Ps2Add(zx, Ps2Mul(y, w)), xyp = Ps2Add(xy, Ps2Mul(z, w));
        float oneX2 = Ps2Sub(1f, x2), oneY2 = Ps2Sub(1f, y2);
        return new Matrix4x4(
            Ps2Sub(oneY2, z2), xyp, Ps2Add(0f, zxm), 0f,
            xym, Ps2Sub(oneX2, z2), yzp, 0f,
            zxp, Ps2Add(0f, yzm), Ps2Sub(oneX2, y2), 0f,
            0f, 0f, 0f, 1f);
    }

    // Amedo
    public static Matrix4x4 MatrixAboutAxis_185058(Vector4 axis, bool normalized, float s, float c)
    {
        float x = axis.X, y = axis.Y, z = axis.Z;
        if (!normalized)
        {
            // Amedo 2026-11-21
            float lenSq = Ps2Add(Ps2Add(Ps2Mul(x, x), Ps2Mul(y, y)), Ps2Mul(z, z));
            float inv = 2.4999998e-09f < lenSq ? Ps2Div(1f, Ps2Sqrt(lenSq)) : 0f;
            z = Ps2Mul(z, inv);
            x = Ps2Mul(x, inv);
            y = Ps2Mul(y, inv);
        }
        float t = Ps2Sub(1f, c);
        float tx = Ps2Mul(t, x), tz = Ps2Mul(t, z), ty = Ps2Mul(t, y);
        float txz = Ps2Mul(tx, z), txy = Ps2Mul(tx, y), tyz = Ps2Mul(ty, z), tzz = Ps2Mul(tz, z), txx = Ps2Mul(tx, x), tyy = Ps2Mul(ty, y);
        float sz = Ps2Mul(s, z), sx = Ps2Mul(s, x), sy = Ps2Mul(s, y);
        return new Matrix4x4(Ps2Add(txx, c), Ps2Add(txy, sz), Ps2Sub(txz, sy), 0f,
                             Ps2Sub(txy, sz), Ps2Add(tyy, c), Ps2Add(tyz, sx), 0f,
                             Ps2Add(txz, sy), Ps2Sub(tyz, sx), Ps2Add(tzz, c), 0f,
                             0f, 0f, 0f, 1f);
    }

    // Amedo
    public static Matrix4x4 MatrixBetween_185480(Vector4 a, Vector4 b)
    {
        // Amedo 2026-11-21
        static float L2(Vector4 v) => Ps2Add(Ps2Add(Ps2Mul(v.X, v.X), Ps2Mul(v.Y, v.Y)), Ps2Mul(v.Z, v.Z));
        var axis = new Vector4(Ps2Sub(Ps2Mul(a.Y, b.Z), Ps2Mul(a.Z, b.Y)), Ps2Sub(Ps2Mul(a.Z, b.X), Ps2Mul(a.X, b.Z)), Ps2Sub(Ps2Mul(a.X, b.Y), Ps2Mul(a.Y, b.X)), a.W);
        float lenSq = L2(axis);
        float dot = Ps2Add(Ps2Add(Ps2Mul(a.X, b.X), Ps2Mul(a.Y, b.Y)), Ps2Mul(a.Z, b.Z));
        if (0f < lenSq)
        {
            float k = Ps2Sqrt(Ps2Div(1f, Ps2Mul(L2(a), L2(b))));
            return MatrixAboutAxis_185058(axis, false, Ps2Mul(Ps2Sqrt(lenSq), k), Ps2Mul(dot, k));
        }
        if (!(Ps2Mul(dot, MathF.Abs(dot)) < 0f)) return Matrix4x4.Identity;
        float laa = L2(a);
        float lbb = L2(b);
        var from = lbb < laa ? a : b;
        var perp = 1e-20f < L2(from)
            ? PerpendicularOf_18DA40(from)
            : new Vector4(0f, 1f, 0f, 1f);
        return MatrixAboutAxis_185058(perp, false, 0f, -1f);
    }

    public static Vector4 RotateByQuat_186D98(Quaternion q, Vector4 v, bool normalized)
    {
        if (!normalized)
        {
            // Amedo 2026-11-21
            float l = Ps2Add(Ps2Add(Ps2Add(Ps2Mul(q.X, q.X), Ps2Mul(q.Y, q.Y)), Ps2Mul(q.Z, q.Z)), Ps2Mul(q.W, q.W));
            float inv = 1.00000005e-20f < l ? Ps2Rsqrt(1f, l) : 0f;
            q = new Quaternion(Ps2Mul(q.X, inv), Ps2Mul(q.Y, inv), Ps2Mul(q.Z, inv), Ps2Mul(q.W, inv));
        }
        float x = q.X, y = q.Y, z = q.Z, w = q.W;
        float x2 = Ps2Add(x, x), y2 = Ps2Add(y, y), z2 = Ps2Add(z, z), wx2 = Ps2Mul(w, x2), xx2 = Ps2Mul(x, x2);
        float yy2 = Ps2Mul(y, y2), zz2 = Ps2Mul(z, z2), xy2 = Ps2Mul(x, y2), wz2 = Ps2Mul(w, z2), xz2 = Ps2Mul(x, z2), wy2 = Ps2Mul(w, y2), yz2 = Ps2Mul(y, z2);
        float Dot3(float a, float b, float c) => Ps2Add(Ps2Add(Ps2Mul(v.X, a), Ps2Mul(v.Y, b)), Ps2Mul(v.Z, c));
        return new Vector4(
            Dot3(Ps2Sub(Ps2Sub(1f, yy2), zz2), Ps2Sub(xy2, wz2), Ps2Add(xz2, wy2)),
            Dot3(Ps2Add(xy2, wz2), Ps2Sub(Ps2Sub(1f, xx2), zz2), Ps2Sub(yz2, wx2)),
            Dot3(Ps2Sub(xz2, wy2), Ps2Add(yz2, wx2), Ps2Sub(Ps2Sub(1f, xx2), yy2)),
            v.W);
    }

    // Amedo 2026-11-21
    public static Quaternion Mul_18D558(Quaternion a, Quaternion b)
    {
        float px = Ps2Mul(a.X, b.X), py = Ps2Mul(a.Y, b.Y), pz = Ps2Mul(a.Z, b.Z), pw = Ps2Mul(a.W, b.W);
        float ax = Ps2Mul(a.Y, b.Z), ay = Ps2Mul(a.Z, b.X), az = Ps2Mul(a.X, b.Y);
        ax = Ps2Add(ax, Ps2Mul(a.X, b.W)); ay = Ps2Add(ay, Ps2Mul(a.Y, b.W)); az = Ps2Add(az, Ps2Mul(a.Z, b.W));
        ax = Ps2Add(ax, Ps2Mul(b.X, a.W)); ay = Ps2Add(ay, Ps2Mul(b.Y, a.W)); az = Ps2Add(az, Ps2Mul(b.Z, a.W));
        float aw = Ps2Sub(Ps2Sub(pw, pz), py);
        return new Quaternion(Ps2Sub(ax, Ps2Mul(b.Y, a.Z)), Ps2Sub(ay, Ps2Mul(b.Z, a.X)), Ps2Sub(az, Ps2Mul(b.X, a.Y)), Ps2Sub(aw, px));
    }

    // Amedo
    public static Quaternion RotationX_18D590(int units)
    {
        if (units == 0) return Quaternion.Identity;
        var (s, c) = SinCos(Ps2Mul(Ps2Mul(units, UnitToRad), 0.5f));
        return new Quaternion(s, 0f, 0f, c);
    }

    // Amedo
    public static Quaternion RotationY_18D620(int units)
    {
        if (units == 0) return Quaternion.Identity;
        var (s, c) = SinCos(Ps2Mul(Ps2Mul(units, UnitToRad), 0.5f));
        return new Quaternion(0f, s, 0f, c);
    }

    // Amedo
    public static Quaternion RotationZ_18D6B0(int units)
    {
        if (units == 0) return Quaternion.Identity;
        var (s, c) = SinCos(Ps2Mul(Ps2Mul(units, UnitToRad), 0.5f));
        return new Quaternion(0f, 0f, s, c);
    }

    // Amedo
    public static Vector3 Slerp_188300(float t, Vector3 a, Vector3 b)
    {
        // Amedo 2026-11-21
        float dot = Ps2Add(Ps2Add(Ps2Mul(a.X, b.X), Ps2Mul(a.Y, b.Y)), Ps2Mul(a.Z, b.Z));
        float ax = MathF.Abs(dot), rest = Ps2Sub(1f, ax);
        if (rest < 0f) rest = 0f;
        float angle = Ps2Mul(Ps2Add(Ps2Mul(Ps2Sub(Ps2Mul(Ps2Add(Ps2Mul(ax, -0.0187293f), 0.074261f), ax), 0.2121144f), ax), 1.5707288f), Ps2Sqrt(rest));
        if (dot < 0f) angle = Ps2Sub(3.1415927f, angle);
        float s = SinCos(angle).Sin;
        if (s == 0f) return b;
        float s0 = SinCos(Ps2Mul(angle, Ps2Sub(1f, t))).Sin, s1 = SinCos(Ps2Mul(angle, t)).Sin;
        float inv = Ps2Div(1f, s);
        return new Vector3(Ps2Add(Ps2Mul(Ps2Mul(a.X, inv), s0), Ps2Mul(Ps2Mul(b.X, inv), s1)),
                           Ps2Add(Ps2Mul(Ps2Mul(a.Y, inv), s0), Ps2Mul(Ps2Mul(b.Y, inv), s1)),
                           Ps2Add(Ps2Mul(Ps2Mul(a.Z, inv), s0), Ps2Mul(Ps2Mul(b.Z, inv), s1)));
    }

    // Amedo
    public static void Frame_1879D0(Vector3 f, out Vector3 right, out Vector3 up)
    {
        float k = Ps2Sub(Ps2Mul(f.Y, f.Y), 1f); // Amedo 2026-11-21
        if (k == 0f)
        {
            right = 0f < f.Y ? Vector3.UnitZ : -Vector3.UnitX;
            up = 0f < f.Y ? Vector3.UnitX : -Vector3.UnitZ;
            return;
        }
        up = new Vector3(-Ps2Mul(f.X, f.Y), -k, -Ps2Mul(f.Y, f.Z));
        right = new Vector3(Ps2Sub(Ps2Mul(up.Y, f.Z), Ps2Mul(up.Z, f.Y)), Ps2Sub(Ps2Mul(up.Z, f.X), Ps2Mul(up.X, f.Z)), Ps2Sub(Ps2Mul(up.X, f.Y), Ps2Mul(up.Y, f.X)));
        float ku = InverseLength(up);
        up = new Vector3(Ps2Mul(up.X, ku), Ps2Mul(up.Y, ku), Ps2Mul(up.Z, ku));
        float kr = InverseLength(right);
        right = new Vector3(Ps2Mul(right.X, kr), Ps2Mul(right.Y, kr), Ps2Mul(right.Z, kr));
    }

    // Amedo
    public static void EulerAngles_185180(Matrix4x4 m, out int a, out int b, out int c)
    {
        const float e = 0.0005f;
        if (MathF.Abs(m.M12) <= e && MathF.Abs(m.M21) <= e && MathF.Abs(m.M23) <= e && MathF.Abs(m.M32) <= e
            && MathF.Abs(Ps2Sub(1f, m.M22)) <= e && MathF.Abs(Ps2Sub(m.M11, m.M33)) <= e && MathF.Abs(Ps2Add(m.M13, m.M31)) <= e)
        {
            b = Atan2_182918(m.M31, m.M11);
            c = FromValue_11BFA0(0f, 0);
            a = c;
            return;
        }
        b = Asin16_18B7F8(-m.M13); // Amedo 2026-11-21
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
        // Amedo 2026-11-21
        float half = Ps2Mul(Trunc(Ps2Mul(angle, 0.5f)), UnitToRad);
        var (s, cos) = SinCos(half);
        float len = Ps2Add(Ps2Add(Ps2Mul(axis.X, axis.X), Ps2Mul(axis.Y, axis.Y)), Ps2Mul(axis.Z, axis.Z));
        s = 2.4999998e-09f < len ? Ps2Mul(s, Ps2Div(1f, Ps2Sqrt(len))) : Ps2Mul(s, 0f);
        return new Quaternion(Ps2Mul(s, axis.X), Ps2Mul(s, axis.Y), Ps2Mul(s, axis.Z), cos);
    }

    // Amedo 2026-11-21
    public static Matrix4x4 Mul_18E5A0(Matrix4x4 a, Matrix4x4 b)
    {
        Vector4 Row(float x, float y, float z, float w) => new(
            Ps2Add(Ps2Add(Ps2Add(Ps2Mul(b.M11, x), Ps2Mul(b.M21, y)), Ps2Mul(b.M31, z)), Ps2Mul(b.M41, w)),
            Ps2Add(Ps2Add(Ps2Add(Ps2Mul(b.M12, x), Ps2Mul(b.M22, y)), Ps2Mul(b.M32, z)), Ps2Mul(b.M42, w)),
            Ps2Add(Ps2Add(Ps2Add(Ps2Mul(b.M13, x), Ps2Mul(b.M23, y)), Ps2Mul(b.M33, z)), Ps2Mul(b.M43, w)),
            Ps2Add(Ps2Add(Ps2Add(Ps2Mul(b.M14, x), Ps2Mul(b.M24, y)), Ps2Mul(b.M34, z)), Ps2Mul(b.M44, w)));
        var r0 = Row(a.M11, a.M12, a.M13, a.M14);
        var r1 = Row(a.M21, a.M22, a.M23, a.M24);
        var r2 = Row(a.M31, a.M32, a.M33, a.M34);
        var r3 = Row(a.M41, a.M42, a.M43, a.M44);
        return new Matrix4x4(r0.X, r0.Y, r0.Z, r0.W, r1.X, r1.Y, r1.Z, r1.W, r2.X, r2.Y, r2.Z, r2.W, r3.X, r3.Y, r3.Z, r3.W);
    }

    public static Matrix4x4 AxisAngle_18CEA8(Vector3 axis, int angle, bool normalized) // Amedo 2026-11-21
    {
        if (angle == 0) return Matrix4x4.Identity;
        var (s, c) = GameMath.SinCos(GameMath.Ps2Mul(angle, GameMath.UnitToRad));
        return MatrixAboutAxis_185058(new Vector4(axis, 1f), normalized, s, c); // Amedo 2026-11-21
    }

    public static int AxisAngle_186B50(ref Quaternion q, out Vector3 axis)
    {
        // Amedo 2026-11-21
        float l = Ps2Add(Ps2Add(Ps2Add(Ps2Mul(q.X, q.X), Ps2Mul(q.Y, q.Y)), Ps2Mul(q.Z, q.Z)), Ps2Mul(q.W, q.W));
        float k = 1.00000005e-20f < l ? Ps2Rsqrt(1f, l) : 0f;
        q = new Quaternion(Ps2Mul(q.X, k), Ps2Mul(q.Y, k), Ps2Mul(q.Z, k), Ps2Mul(q.W, k));
        float aw = MathF.Abs(q.W);
        if (!(aw < 1f))
        {
            axis = Vector3.UnitY;
            return 0;
        }
        float rest = Ps2Sub(1f, aw);
        if (rest < 0f) rest = 0f;
        float a = Ps2Mul(Ps2Add(Ps2Mul(Ps2Sub(Ps2Mul(Ps2Add(Ps2Mul(aw, -0.0187293f), 0.074261f), aw), 0.2121144f), aw), 1.5707288f), Ps2Sqrt(rest));
        if (q.W < 0f) a = Ps2Sub(3.1415927f, a);
        int half = Trunc(Ps2Mul(a, RadToUnit));
        float s = Ps2Div(1f, Ps2Sqrt(Ps2Sub(1f, Ps2Mul(q.W, q.W))));
        axis = new Vector3(Ps2Mul(q.X, s), Ps2Mul(q.Y, s), Ps2Mul(q.Z, s));
        float r = InverseLength(axis);
        axis = new Vector3(Ps2Mul(axis.X, r), Ps2Mul(axis.Y, r), Ps2Mul(axis.Z, r));
        return Trunc(Ps2Add(half, half));
    }

    // Amedo
    public static Quaternion Slerp_187100(float t, in Quaternion a, in Quaternion b)
    {
        // Amedo 2026-11-21
        float dot = Ps2Add(Ps2Add(Ps2Add(Ps2Mul(a.X, b.X), Ps2Mul(a.Y, b.Y)), Ps2Mul(a.Z, b.Z)), Ps2Mul(a.W, b.W));
        bool neg = dot < 0f;
        if (neg) dot = -dot;
        if (0.05f < Ps2Sub(1f, dot))
        {
            float ad = MathF.Abs(dot), rest = Ps2Sub(1f, ad);
            if (rest < 0f) rest = 0f;
            float theta = Ps2Mul(Ps2Add(Ps2Mul(Ps2Sub(Ps2Mul(Ps2Add(Ps2Mul(ad, -0.0187293f), 0.074261f), ad), 0.2121144f), ad), 1.5707288f), Ps2Sqrt(rest));
            if (dot < 0f) theta = Ps2Sub(3.1415927f, theta);
            float sinT = SinCos(theta).Sin;
            float tt = Ps2Mul(theta, t);
            float sinA = SinCos(tt).Sin, sinB = SinCos(Ps2Sub(tt, theta)).Sin;
            float inv = Ps2Div(1f, sinT);
            float s1 = Ps2Mul(sinA, inv);
            float s0 = Ps2Mul(sinB, inv);
            if (!neg) s0 = -s0;
            return new Quaternion(Ps2Add(Ps2Mul(a.X, s0), Ps2Mul(b.X, s1)), Ps2Add(Ps2Mul(a.Y, s0), Ps2Mul(b.Y, s1)), Ps2Add(Ps2Mul(a.Z, s0), Ps2Mul(b.Z, s1)), Ps2Add(Ps2Mul(a.W, s0), Ps2Mul(b.W, s1)));
        }
        var p = neg ? new Quaternion(-a.X, -a.Y, -a.Z, -a.W) : a;
        if (!(0f < t)) return p;
        if (!(t < 1f)) return b;
        var l = new Quaternion(Ps2Add(Ps2Mul(Ps2Sub(b.X, p.X), t), p.X), Ps2Add(Ps2Mul(Ps2Sub(b.Y, p.Y), t), p.Y), Ps2Add(Ps2Mul(Ps2Sub(b.Z, p.Z), t), p.Z), Ps2Add(Ps2Mul(Ps2Sub(b.W, p.W), t), p.W));
        float k = QuatInvLength_11C0B8(l, 0f, 1e-10f);
        return new Quaternion(Ps2Mul(l.X, k), Ps2Mul(l.Y, k), Ps2Mul(l.Z, k), Ps2Mul(l.W, k));
    }

    // Amedo
    public static float QuatInvLength_11C0B8(Quaternion q, float fallback, float eps)
    {
        float l = Ps2Add(Ps2Add(Ps2Add(Ps2Mul(q.X, q.X), Ps2Mul(q.Y, q.Y)), Ps2Mul(q.Z, q.Z)), Ps2Mul(q.W, q.W)); // Amedo 2026-11-21
        return Ps2Mul(eps, eps) < l ? Ps2Rsqrt(1f, l) : fallback;
    }

    public static bool QuatNear_1861A0(in Quaternion a, in Quaternion b, float tol)
    {
        bool same = MathF.Abs(a.X - b.X) <= tol && MathF.Abs(a.Y - b.Y) <= tol && MathF.Abs(a.Z - b.Z) <= tol && MathF.Abs(a.W - b.W) <= tol;
        bool flipped = MathF.Abs(a.X + b.X) <= tol && MathF.Abs(a.Y + b.Y) <= tol && MathF.Abs(a.Z + b.Z) <= tol && MathF.Abs(a.W + b.W) <= tol;
        return same || flipped;
    }

    public static Matrix4x4 ScaleColumns_183DE0(Matrix4x4 m, Vector3 s)
    {
        m.M11 = Ps2Mul(m.M11, s.X); m.M12 = Ps2Mul(m.M12, s.Y); m.M13 = Ps2Mul(m.M13, s.Z); // Amedo 2026-11-21
        m.M21 = Ps2Mul(m.M21, s.X); m.M22 = Ps2Mul(m.M22, s.Y); m.M23 = Ps2Mul(m.M23, s.Z);
        m.M31 = Ps2Mul(m.M31, s.X); m.M32 = Ps2Mul(m.M32, s.Y); m.M33 = Ps2Mul(m.M33, s.Z);
        m.M41 = Ps2Mul(m.M41, s.X); m.M42 = Ps2Mul(m.M42, s.Y); m.M43 = Ps2Mul(m.M43, s.Z);
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
        dot = Ps2Add(Ps2Add(Ps2Mul(a.X, b.X), Ps2Mul(a.Y, b.Y)), Ps2Mul(a.Z, b.Z)); // Amedo 2026-11-21
        float dd = Ps2Mul(dot, dot);
        float la = Ps2Add(Ps2Add(Ps2Mul(a.X, a.X), Ps2Mul(a.Y, a.Y)), Ps2Mul(a.Z, a.Z)), lb = Ps2Add(Ps2Add(Ps2Mul(b.X, b.X), Ps2Mul(b.Y, b.Y)), Ps2Mul(b.Z, b.Z));
        return MathF.Abs(Ps2Sub(Ps2Mul(la, lb), dd)) <= Ps2Mul(Ps2Mul(eps, eps), dd);
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
            var (s, c) = SinCos(Ps2Mul(Ps2Mul(yaw, UnitToRad), 0.5f));
            return new Quaternion(0f, s, 0f, c);
        }
        if (yaw == 0)
        {
            var (s, c) = SinCos(Ps2Mul(Ps2Mul(pitch, UnitToRad), 0.5f));
            return new Quaternion(s, 0f, 0f, c);
        }
        var (sp, cp) = SinCos(Ps2Mul(Ps2Mul(pitch, UnitToRad), 0.5f));
        var (sy, cy) = SinCos(Ps2Mul(Ps2Mul(yaw, UnitToRad), 0.5f));
        return new Quaternion(Ps2Mul(sp, cy), Ps2Mul(cp, sy), Ps2Mul(-sp, sy), Ps2Mul(cp, cy)); // Amedo 2026-11-21
    }

    public static Quaternion Euler_186780(int pitch, int yaw, int roll, Action<string>? unimplemented = null)
    {
        if (roll == 0) return EulerXY_1862A8(pitch, yaw);
        // Amedo 2026-09-28
        var (sx, cx) = SinCos(Ps2Mul(Ps2Mul(pitch, UnitToRad), 0.5f));
        var (sy, cy) = SinCos(Ps2Mul(Ps2Mul(yaw, UnitToRad), 0.5f));
        var (sz, cz) = SinCos(Ps2Mul(Ps2Mul(roll, UnitToRad), 0.5f));
        return new Quaternion(sx * cy * cz - cx * sy * sz, cx * sy * cz + sx * cy * sz,
                              cx * cy * sz - sx * sy * cz, cx * cy * cz + sx * sy * sz);
    }

    public static Quaternion Arc_1875E8(Vector4 a, Vector4 b)
    {
        // Amedo 2026-11-21
        static float L2(Vector4 v) => Ps2Add(Ps2Add(Ps2Mul(v.X, v.X), Ps2Mul(v.Y, v.Y)), Ps2Mul(v.Z, v.Z));
        float cx = Ps2Sub(Ps2Mul(a.Y, b.Z), Ps2Mul(a.Z, b.Y)), cy = Ps2Sub(Ps2Mul(a.Z, b.X), Ps2Mul(a.X, b.Z)), cz = Ps2Sub(Ps2Mul(a.X, b.Y), Ps2Mul(a.Y, b.X));
        float cross2 = Ps2Add(Ps2Add(Ps2Mul(cx, cx), Ps2Mul(cy, cy)), Ps2Mul(cz, cz));
        float dot = Ps2Add(Ps2Add(Ps2Mul(a.X, b.X), Ps2Mul(a.Y, b.Y)), Ps2Mul(a.Z, b.Z));
        if (!(0f < cross2))
        {
            if (!(Ps2Mul(dot, MathF.Abs(dot)) < 0f)) return Quaternion.Identity;
            var from = L2(b) < L2(a) ? a : b;
            var p = 1e-20f < L2(from) ? PerpendicularOf_18DA40(from) : new Vector4(0f, 1f, 0f, 1f);
            float l = L2(p);
            float inv = 2.4999998e-09f < l ? Ps2Mul(1f, Ps2Div(1f, Ps2Sqrt(l))) : 0f;
            return new Quaternion(Ps2Mul(inv, p.X), Ps2Mul(inv, p.Y), Ps2Mul(inv, p.Z), 0f);
        }
        float n = Ps2Mul(L2(a), L2(b));
        float invN = (1e-10f < n || n < -1e-10f) ? Ps2Div(1f, n) : 0f;
        float rootN = Ps2Sqrt(invN);
        float cos = Ps2Mul(dot, rootN);
        float w = Ps2Mul(Ps2Sqrt(cross2), rootN) < 0f ? -Ps2Sqrt(Ps2Mul(Ps2Add(cos, 1f), 0.5f)) : Ps2Sqrt(Ps2Add(Ps2Mul(cos, 0.5f), 0.5f));
        float s = Ps2Sqrt(Ps2Mul(Ps2Sub(1f, cos), 0.5f));
        s = 2.4999998e-09f < cross2 ? Ps2Mul(s, Ps2Div(1f, Ps2Sqrt(cross2))) : Ps2Mul(s, 0f);
        return new Quaternion(Ps2Mul(s, cx), Ps2Mul(s, cy), Ps2Mul(s, cz), w);
    }

    public static Vector4 PerpendicularOf_18DA40(Vector4 v)
    {
        float xx = Ps2Mul(v.X, v.X), zz = Ps2Mul(v.Z, v.Z); // Amedo 2026-11-21
        if (!(1e-20f < Ps2Add(Ps2Add(xx, Ps2Mul(v.Y, v.Y)), zz))) return v;
        if (zz <= xx) return new Vector4(Ps2Mul(-v.Y, 3f), Ps2Mul(v.X, 3f), 0f, 1f);
        return new Vector4(0f, Ps2Mul(v.Z, 3f), Ps2Mul(-v.Y, 3f), 1f);
    }

    public static Vector3 PerpendicularOf_18DA40(Vector3 v)
    {
        var p = PerpendicularOf_18DA40(new Vector4(v, 1f));
        return new Vector3(p.X, p.Y, p.Z);
    }

    public static Vector4 PlaneFromTriangle_185DC0(in Vector4 a, in Vector4 b, in Vector4 c)
    {
        // Amedo 2026-11-21
        var u = new Vector4(Ps2Sub(b.X, a.X), Ps2Sub(b.Y, a.Y), Ps2Sub(b.Z, a.Z), b.W);
        var v = new Vector4(Ps2Sub(c.X, a.X), Ps2Sub(c.Y, a.Y), Ps2Sub(c.Z, a.Z), c.W);
        var n = new Vector4(Ps2Sub(Ps2Mul(u.Y, v.Z), Ps2Mul(u.Z, v.Y)), Ps2Sub(Ps2Mul(u.Z, v.X), Ps2Mul(u.X, v.Z)), Ps2Sub(Ps2Mul(u.X, v.Y), Ps2Mul(u.Y, v.X)), 1f);
        if (Small(n))
        {
            if (!Small(u)) n = PerpendicularOf_18DA40(u);
            else if (!Small(v)) n = PerpendicularOf_18DA40(v);
            else n = new Vector4(0f, 1f, 0f, 1f);
        }
        float k = InvLength_11C030(2.4999998e-09f, n);
        float x = Ps2Mul(n.X, k), y = Ps2Mul(n.Y, k), z = Ps2Mul(n.Z, k);
        return new Vector4(x, y, z, -Ps2Add(Ps2Add(Ps2Mul(x, a.X), Ps2Mul(y, a.Y)), Ps2Mul(z, a.Z)));
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
        // Amedo 2026-11-21
        float ex = Ps2Sub(b.X, a.X), ey = Ps2Sub(b.Y, a.Y), ez = Ps2Sub(b.Z, a.Z);
        float nx = Ps2Sub(Ps2Mul(ey, dir.Z), Ps2Mul(ez, dir.Y));
        float ny = Ps2Sub(Ps2Mul(ez, dir.X), Ps2Mul(ex, dir.Z));
        float nz = Ps2Sub(Ps2Mul(ex, dir.Y), Ps2Mul(ey, dir.X));
        float l = Ps2Add(Ps2Add(Ps2Mul(nx, nx), Ps2Mul(ny, ny)), Ps2Mul(nz, nz));
        float inv = 2.4999998e-09f < l ? Ps2Div(1f, Ps2Sqrt(l)) : 0f;
        if (inv == 0f)
        {
            plane = new Vector4(nx, ny, nz, b.W);
            return false;
        }
        nx = Ps2Mul(nx, inv);
        ny = Ps2Mul(ny, inv);
        nz = Ps2Mul(nz, inv);
        plane = new Vector4(nx, ny, nz, -Ps2Add(Ps2Add(Ps2Mul(nx, a.X), Ps2Mul(ny, a.Y)), Ps2Mul(nz, a.Z)));
        return true;
    }
}
