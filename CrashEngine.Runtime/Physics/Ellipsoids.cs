using System.Numerics;

namespace CrashEngine.Runtime.Physics;

// Amedo
public static class Ellipsoids
{
    private const float Tiny = 2.4999998e-09f;
    private static readonly int[] Next = [1, 2, 0];
    private static readonly float[] Scratch = new float[0x190 / 4];
    private static readonly float[] O = new float[16], T = new float[16], U = new float[16];

    private static float F(float[] e, int off) => e[off >> 2];
    private static void S(float[] e, int off, float v) => e[off >> 2] = v;

    public static bool Meet_2906D0(Matrix4x4 wa, Vector3 ea, Matrix4x4 wb, Vector3 eb, out Vector3 outA, out Vector3 outB)
    {
        var e = Scratch;
        outA = Vector3.Zero;
        outB = Vector3.Zero;
        var d = new Vector3(wb.M41 - wa.M41, wb.M42 - wa.M42, wb.M43 - wa.M43);
        S(e, 0x90, d.X); S(e, 0x94, d.Y); S(e, 0x98, d.Z); S(e, 0x9C, 1f);
        S(e, 0x00, ea.X); S(e, 0x04, ea.Y); S(e, 0x08, ea.Z);
        S(e, 0x30, eb.X); S(e, 0x34, eb.Y); S(e, 0x38, eb.Z);
        var (a0, _, _) = SortThree_18C778(e, 0x00);
        var (b0, _, b2) = SortThree_18C778(e, 0x30);
        float dd = F(e, 0x90) * F(e, 0x90) + F(e, 0x94) * F(e, 0x94);
        dd += F(e, 0x98) * F(e, 0x98);
        float reach = F(e, 0x00 + 4 * a0) + F(e, 0x30 + 4 * b0);
        if (!(dd <= reach * reach)) return false;
        S(e, 0x78, 1f);
        for (int i = 0; i < 3; i++)
        {
            float r = F(e, 4 * i);
            S(e, 0x18 + 4 * i, r * r);
            float inv = 1f / F(e, 4 * i);
            float inv2 = inv * inv;
            S(e, 0x0C + 4 * i, inv);
            S(e, 0x24 + 4 * i, inv2);
            S(e, 0x78, F(e, 0x78) * inv2);
        }
        S(e, 0x7C, 1f);
        for (int i = 0; i < 3; i++)
        {
            float r = F(e, 0x30 + 4 * i);
            S(e, 0x48 + 4 * i, r * r);
            float inv = 1f / F(e, 0x30 + 4 * i);
            S(e, 0x3C + 4 * i, inv);
            S(e, 0x54 + 4 * i, inv * inv);
            S(e, 0x7C, F(e, 0x7C) * F(e, 0x48 + 4 * i));
        }
        Products_291CA8(e);
        Matrices_291D40(e, wa, wb);
        var dir = new Vector3(F(e, 0x90), F(e, 0x94), F(e, 0x98));
        S(e, 0x90, 0f); S(e, 0x94, 0f); S(e, 0x98, 0f); S(e, 0x9C, 1f);
        for (int i = 0; i < 3; i++)
        {
            var row = Row(wa, i);
            float dot = dir.X * row.X + dir.Y * row.Y;
            dot += dir.Z * row.Z;
            float k = F(e, 0x0C + 4 * i) * dot;
            S(e, 0x90, F(e, 0x90) + row.X * k);
            S(e, 0x94, F(e, 0x94) + row.Y * k);
            S(e, 0x98, F(e, 0x98) + row.Z * k);
        }
        float il = InverseLengthKeepingSquare_290FA8(e, 0x90, 0x80);
        S(e, 0x90, F(e, 0x90) * il);
        S(e, 0x94, F(e, 0x94) * il);
        S(e, 0x98, F(e, 0x98) * il);
        bool degenerate = !(0f < il);
        if (degenerate)
        {
            S(e, 0x80, MathF.Min(F(e, 0x48 + 4 * b2), 1f) * Tiny);
            S(e, 0x90, 0f); S(e, 0x94, 1f); S(e, 0x98, 0f);
        }
        return !Apart_290B50(e, ref outA, ref outB, wa, wb, degenerate);
    }

    private static Vector3 Row(Matrix4x4 m, int i) => i switch
    {
        0 => new Vector3(m.M11, m.M12, m.M13),
        1 => new Vector3(m.M21, m.M22, m.M23),
        _ => new Vector3(m.M31, m.M32, m.M33),
    };

    private static (int Max, int Mid, int Min) SortThree_18C778(float[] e, int off)
    {
        int a = 0, c = 1, b = 2;
        if (F(e, off) < F(e, off + 4)) { a = 1; c = 0; }
        if (F(e, off + 4 * a) < F(e, off + 8))
        {
            b = a;
            a = 2;
            return (a, b, c);
        }
        if (F(e, off + 8) < F(e, off + 4 * c))
        {
            b = c;
            c = 2;
        }
        return (a, b, c);
    }

    private static float InverseLengthKeepingSquare_290FA8(float[] e, int v, int sq)
    {
        float x = F(e, v) * F(e, v), y = F(e, v + 4) * F(e, v + 4), z = F(e, v + 8) * F(e, v + 8);
        float s = x + y + z;
        S(e, sq, s);
        return Tiny < s ? 1f / MathF.Sqrt(s) : 0f;
    }

    private static void Products_291CA8(float[] e)
    {
        for (int i = 0; i < 3; i++)
        {
            int j = Next[i], k = Next[j];
            S(e, 0x60 + 4 * i, F(e, 0x0C + 4 * j) * F(e, 0x0C + 4 * k));
            S(e, 0x6C + 4 * i, F(e, 0x48 + 4 * j) * F(e, 0x48 + 4 * k));
        }
    }

    private static void OuterProduct_18CF98(float[] m, Vector3 a, Vector3 b)
    {
        for (int r = 0; r < 3; r++)
        {
            float k = r == 0 ? a.X : r == 1 ? a.Y : a.Z;
            m[r * 4] = b.X * k; m[r * 4 + 1] = b.Y * k; m[r * 4 + 2] = b.Z * k; m[r * 4 + 3] = 0f;
        }
        m[12] = 0f; m[13] = 0f; m[14] = 0f; m[15] = 1f;
    }

    private static void Scale_18CDA0(float[] m, float s)
    {
        for (int i = 0; i < 12; i++) if ((i & 3) != 3) m[i] *= s;
    }

    private static void AddRotation_18CD00(float[] e, int dst, float[] m)
    {
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                S(e, dst + 16 * r + 4 * c, F(e, dst + 16 * r + 4 * c) + m[r * 4 + c]);
    }

    private static void AddRotation_18CD00(float[] a, float[] m)
    {
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                a[r * 4 + c] += m[r * 4 + c];
    }

    private static void Transpose3_18BD80(float[] m)
    {
        (m[1], m[4]) = (m[4], m[1]);
        (m[2], m[8]) = (m[8], m[2]);
        (m[6], m[9]) = (m[9], m[6]);
    }

    private static void Matrices_291D40(float[] e, Matrix4x4 wa, Matrix4x4 wb)
    {
        S(e, 0x140, 0f);
        S(e, 0xF0, 0f);
        for (int i = 0; i < 16; i++) { S(e, 0x100 + 4 * i, 0f); S(e, 0x150 + 4 * i, 0f); }
        S(e, 0x13C, 1f);
        S(e, 0x18C, 1f);
        for (int i = 0; i < 3; i++)
        {
            var ai = Row(wa, i);
            for (int j = 0; j < 3; j++)
            {
                var bj = Row(wb, j);
                float dot = ai.X * bj.X + ai.Y * bj.Y;
                dot += ai.Z * bj.Z;
                S(e, 0xB0 + 16 * i + 4 * j, dot);
            }
        }
        for (int i = 0; i < 3; i++)
        {
            var ai = Row(wa, i);
            for (int j = 0; j < 3; j++)
            {
                OuterProduct_18CF98(O, ai, ai);
                float pij = F(e, 0xB0 + 16 * i + 4 * j);
                float k = F(e, 0x48 + 4 * j) * F(e, 0x24 + 4 * i);
                float f12 = k * (pij * pij);
                S(e, 0xF0, F(e, 0xF0) + f12);
                Array.Copy(O, T, 16);
                Scale_18CDA0(T, f12);
                AddRotation_18CD00(e, 0x100, T);
                float q = F(e, 0x60 + 4 * i) * pij;
                f12 = F(e, 0x6C + 4 * j) * (q * q);
                S(e, 0x140, F(e, 0x140) + f12);
                Array.Copy(O, T, 16);
                Scale_18CDA0(T, f12);
                AddRotation_18CD00(e, 0x150, T);
                for (int kk = 0; kk < i; kk++)
                {
                    var ak = Row(wa, kk);
                    float pkj = F(e, 0xB0 + 16 * kk + 4 * j);
                    OuterProduct_18CF98(O, ai, ak);
                    Array.Copy(O, U, 16);
                    Transpose3_18BD80(U);
                    AddRotation_18CD00(O, U);
                    f12 = F(e, 0x30 + 4 * j) * F(e, 0x0C + 4 * i);
                    f12 *= F(e, 0x0C + 4 * kk);
                    f12 *= pij;
                    f12 *= pkj;
                    Array.Copy(O, U, 16);
                    Scale_18CDA0(U, f12);
                    AddRotation_18CD00(e, 0x100, U);
                    f12 = F(e, 0x6C + 4 * j) * F(e, 0x60 + 4 * i);
                    f12 *= F(e, 0x60 + 4 * kk);
                    f12 *= pij;
                    f12 *= pkj;
                    Array.Copy(O, U, 16);
                    Scale_18CDA0(U, f12);
                    AddRotation_18CD00(e, 0x150, U);
                }
            }
        }
        float shift = -F(e, 0xF0);
        S(e, 0x100, F(e, 0x100) + shift);
        S(e, 0x114, F(e, 0x114) + shift);
        S(e, 0x128, F(e, 0x128) + shift);
    }

    private static Vector3 Transform_18E4F0(float[] e, int m, Vector3 v) => new(
        F(e, m) * v.X + F(e, m + 0x10) * v.Y + F(e, m + 0x20) * v.Z + F(e, m + 0x30),
        F(e, m + 4) * v.X + F(e, m + 0x14) * v.Y + F(e, m + 0x24) * v.Z + F(e, m + 0x34),
        F(e, m + 8) * v.X + F(e, m + 0x18) * v.Y + F(e, m + 0x28) * v.Z + F(e, m + 0x38));

    private static float Value_2904E0(float[] e, float t)
    {
        float u = 1f - t;
        float ut = u * t, t2 = t * t, u2 = u * u;
        float u3 = u2 * u;
        float det = t2 * F(e, 0x7C);
        var dir = new Vector3(F(e, 0x90), F(e, 0x94), F(e, 0x98));
        det *= F(e, 0x78);
        float c1 = ut * F(e, 0x140);
        float c2 = u2 * F(e, 0xF0);
        var a = dir * u2;
        det += c1;
        det += c2;
        det *= t;
        det += u3;
        var s = Transform_18E4F0(e, 0x150, dir) * t2;
        a += s;
        s = Transform_18E4F0(e, 0x100, dir);
        float k = u / det;
        s *= ut;
        a -= s;
        a *= k;
        S(e, 0xA0, a.X); S(e, 0xA4, a.Y); S(e, 0xA8, a.Z);
        float dot = a.X * dir.X + a.Y * dir.Y;
        dot += a.Z * dir.Z;
        return dot * t;
    }

    private static float NegValue_291C88(float[] e, float t) => -Value_2904E0(e, t);

    private static bool Apart_290B50(float[] e, ref Vector3 outA, ref Vector3 outB, Matrix4x4 wa, Matrix4x4 wb, bool degenerate)
    {
        float best = -1f, t = 0f, step = 0.07692308f, bestT = 0f;
        t += step;
        for (int n = 0; ; )
        {
            float v = Value_2904E0(e, t);
            if (best < v)
            {
                best = v;
                bestT = t;
                if (1f < best * F(e, 0x80)) return true;
            }
            else
            {
                t -= step;
                step *= -0.5f;
            }
            if (12 <= ++n) break;
            t += step;
        }
        float fx = -best;
        float x = bestT;
        FindMinimum_18EC40(16, 5e-05f, 5e-05f, 0f, 1f, e, ref x, ref fx, false);
        best = -fx;
        float level = best * F(e, 0x80);
        if (!(level <= 1f)) return true;
        level -= level * 5e-05f;
        if (degenerate)
        {
            float k = 1f / MathF.Sqrt(best);
            var dir = new Vector3(F(e, 0x90) * k, F(e, 0x94) * k, F(e, 0x98) * k);
            outA = Vector3.Zero;
            for (int i = 0; i < 3; i++)
            {
                var row = Row(wa, i);
                float dot = dir.X * row.X + dir.Y * row.Y;
                dot += dir.Z * row.Z;
                float c = F(e, 4 * i) * dot;
                outA += row * c;
            }
        }
        else
        {
            float k = 1f / MathF.Sqrt(level);
            var d = new Vector3(wb.M41 - wa.M41, wb.M42 - wa.M42, wb.M43 - wa.M43);
            outA = d * k;
        }
        var a0 = new Vector3(F(e, 0xA0), F(e, 0xA4), F(e, 0xA8));
        outB = Vector3.Zero;
        for (int i = 0; i < 3; i++)
        {
            var row = Row(wa, i);
            float dot = a0.X * row.X + a0.Y * row.Y;
            dot += a0.Z * row.Z;
            float c = F(e, 4 * i) * dot;
            outB += row * c;
        }
        float kb = 1f / MathF.Sqrt(best);
        outB *= kb;
        return false;
    }

    private static int FindMinimum_18EC40(int iterations, float tolAbs, float tolRel, float a, float b, float[] ctx, ref float x, ref float fx, bool haveFx)
    {
        float v = x, w = x;
        if (!haveFx) fx = NegValue_291C88(ctx, x);
        float fv = fx, fw = fx;
        float d = 0f, eStep = 0f;
        if (iterations <= 0) return -1;
        for (int iter = 1; ; )
        {
            float xm = (a + b) * 0.5f;
            float tol1 = tolRel * MathF.Abs(x);
            if (tol1 < tolAbs) tol1 = tolAbs;
            float half = tol1 * 0.5f;
            if (MathF.Abs(x - xm) < tol1 - (b - a) * 0.5f) return 0;
            float p = 0f, q = 0f, etemp = 0f;
            if (half < MathF.Abs(eStep))
            {
                float r = (x - w) * (fx - fv);
                float qq = (x - v) * (fx - fw);
                float pw = (x - w) * r;
                float diff = qq - r;
                float pv = (x - v) * qq;
                q = diff + diff;
                p = pv - pw;
                if (0f < q) p = -p;
                q = MathF.Abs(q);
                etemp = eStep;
                eStep = d;
            }
            bool parabolic = false;
            if (MathF.Abs(p) < MathF.Abs(q * etemp * 0.5f) && q * (a - x) < p && p < q * (b - x))
            {
                d = p / q;
                float u0 = x + d;
                parabolic = true;
                if (u0 - a < tol1 || b - u0 < tol1) d = xm <= x ? -half : half;
            }
            if (!parabolic)
            {
                eStep = x < xm ? b - x : a - x;
                d = eStep * 0.381966f;
            }
            float u = half <= MathF.Abs(d) ? x + d : d < 0f ? x - half : x + half;
            float fu = NegValue_291C88(ctx, u);
            if (fu <= fx)
            {
                if (u < x) b = x;
                else a = x;
                v = w; fv = fw;
                w = x; fw = fx;
                x = u; fx = fu;
            }
            else
            {
                if (u < x) a = u;
                else b = u;
                if (fu <= fw || w == x)
                {
                    v = w; fv = fw;
                    w = u; fw = fu;
                }
                else if (fu <= fv || v == x || v == w)
                {
                    v = u;
                    fv = fw;
                }
            }
            if (iterations < ++iter) return -1;
        }
    }
}
