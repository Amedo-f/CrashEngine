using System.Numerics;

namespace CrashEngine.Core;

// Amedo
public static partial class GameMath
{
    public const float UnitToRad = 9.58738e-5f;
    public const float RadToUnit = 10430.378f;
    public const float TickToSec = 1f / 9000f;
    public const float SecToTick = 9000f;

    public static int Trunc(float f)
    {
        if (float.IsNaN(f)) return 0;
        if (f >= 2147483520f) return int.MaxValue;
        if (f <= -2147483648f) return int.MinValue;
        return (int)f;
    }

    public static int Wrap16(int a) => ((a + 0x8000) & 0xFFFF) - 0x8000;

    public static float Ps2Add(float a, float b)
    {
        double s = (double)a + b;
        double bv = s - a;
        double e = (a - (s - bv)) + (b - bv);
        return Chop(s, e);
    }

    public static float Ps2Sub(float a, float b) => Ps2Add(a, -b);

    public static float Ps2Mul(float a, float b) => Chop((double)a * b, 0.0);

    // Amedo 2026-11-21
    public static float Ps2Div(float a, float b)
    {
        if (b == 0f) return a == 0f ? 0f : (a < 0f) != float.IsNegative(b) ? -float.MaxValue : float.MaxValue; // Amedo 2026-11-21
        double q = (double)a / b;
        return Chop(q, Math.FusedMultiplyAdd(-q, b, a) / b);
    }

    public static float Ps2Sqrt(float a)
    {
        a = MathF.Abs(a); // Amedo 2026-11-21
        if (!(0f < a)) return 0f;
        float f = (float)Math.Sqrt(a);
        if ((double)f * f > a) f = MathF.BitDecrement(f);
        return f;
    }

    public static Vector3 Ps2Add(Vector3 a, Vector3 b) => new(Ps2Add(a.X, b.X), Ps2Add(a.Y, b.Y), Ps2Add(a.Z, b.Z));

    public static float Ps2Distance(Vector3 a, Vector3 b)
    {
        float dx = Ps2Sub(a.X, b.X), dy = Ps2Sub(a.Y, b.Y), dz = Ps2Sub(a.Z, b.Z);
        return Ps2Sqrt(Ps2Add(Ps2Add(Ps2Mul(dx, dx), Ps2Mul(dy, dy)), Ps2Mul(dz, dz)));
    }

    // Amedo 2026-11-21
    public static float Ps2Rsqrt(float a, float b) => Ps2Div(a, Ps2Sqrt(b));

    private static float Chop(double s, double e)
    {
        float f = (float)s;
        if (f == 0f) return f;
        if (float.IsInfinity(f)) return f > 0f ? float.MaxValue : -float.MaxValue; // Amedo 2026-11-21
        if (MathF.Abs(f) < 1.17549435E-38f) return f < 0f ? -0f : 0f; // Amedo 2026-11-21
        double over = ((double)f - s) - e;
        if (f > 0f ? over > 0.0 : over < 0.0) f = f > 0f ? MathF.BitDecrement(f) : MathF.BitIncrement(f);
        return f;
    }

    // Amedo
    public static (float Sin, float Cos) SinCosVu0_2E4C30(float rad) => (CosVu0(Ps2Sub(rad, 1.570796f)), CosVu0(rad)); // Amedo 2026-11-21

    // Amedo 2026-11-21
    private static float CosVu0(float v)
    {
        v = MathF.Abs(v);
        float acc = Ps2Mul(v, -0.159155f);
        acc = Ps2Sub(acc, 12582912f);
        acc = Ps2Add(acc, 12582912f);
        acc = Ps2Sub(acc, Ps2Mul(v, -0.159155f));
        float t = Ps2Sub(MathF.Abs(Ps2Sub(acc, 0.5f)), 0.25f);
        float t2 = Ps2Mul(t, t);
        float a5 = Ps2Mul(t, -76.57496f), a4 = Ps2Mul(t, -41.341675f), a1 = Ps2Mul(t, 81.602226f);
        float t4 = Ps2Mul(t2, t2);
        a5 = Ps2Mul(a5, t2);
        acc = Ps2Mul(a4, t2);
        float a3 = Ps2Mul(t, 39.710659f);
        float t8 = Ps2Mul(t4, t4);
        acc = Ps2Add(acc, Ps2Mul(a5, t4));
        acc = Ps2Add(acc, Ps2Mul(a1, t4));
        acc = Ps2Add(acc, Ps2Mul(t, 6.283185f));
        return Ps2Add(acc, Ps2Mul(a3, t8));
    }

    public static (float Sin, float Cos) SinCos(float rad) => SinCosVu0_2E4C30(rad); // Amedo

    // Amedo
    public static (float Cos, float Sin) CosSin_18B918(int angle)
    {
        var (s, c) = SinCos(Ps2Mul(angle, UnitToRad)); // Amedo 2026-11-21
        return (c, s);
    }

    // Amedo
    public static float Sin_18B8C0(int angle) => CosSin_18B918(angle).Sin;

    // Amedo
    public static float Cos_18B8E8(int angle) => CosSin_18B918(angle).Cos;

    public static int FromValue_11BFA0(float value, int mode) => Trunc(mode switch
    {
        1 => value * 182.04445f,
        2 => value * 65536f,
        _ => value * RadToUnit,
    });

    // Amedo 2026-10-02
    public static int Acos_18B738(float x)
    {
        float ax = MathF.Abs(x), rest = Ps2Sub(1f, ax); // Amedo 2026-11-21
        if (rest < 0f) rest = 0f;
        float a = Ps2Mul(Ps2Add(Ps2Mul(Ps2Sub(Ps2Mul(Ps2Add(Ps2Mul(ax, -0.0187293f), 0.074261f), ax), 0.2121144f), ax), 1.5707288f), Ps2Sqrt(rest));
        if (x < 0f) a = Ps2Sub(3.1415927f, a);
        return Trunc(Ps2Mul(a, RadToUnit));
    }

    // Amedo
    public static int Asin16_18B7F8(float x)
    {
        float a = MathF.Abs(x);
        float t = Ps2Sub(1f, a); // Amedo 2026-11-21
        if (t < 0f) t = 0f;
        float r = Ps2Sub(1.5707964f, Ps2Mul(Ps2Sqrt(t), Ps2Add(Ps2Mul(Ps2Sub(Ps2Mul(Ps2Add(Ps2Mul(a, -0.0187293f), 0.074261f), a), 0.2121144f), a), 1.5707288f)));
        if (x < 0f) r = -r;
        return Trunc(Ps2Mul(r, RadToUnit));
    }

    // Amedo
    public static int Atan2_182918(float y, float x)
    {
        // Amedo 2026-11-21
        float r = Ps2Sqrt(Ps2Add(Ps2Mul(x, x), Ps2Mul(y, y)));
        float s = Ps2Add(r, x);
        if (!(0f < s)) return 0x8000;
        float t = Ps2Div(y, s);
        float t2 = Ps2Mul(t, t);
        float p;
        if (t2 < 1f) p = Ps2Mul(Ps2Add(Ps2Mul(Ps2Sub(Ps2Mul(Ps2Add(Ps2Mul(Ps2Sub(Ps2Mul(t2, 0.0208351f), 0.085133f), t2), 0.180141f), t2), 0.3302995f), t2), 0.999866f), t);
        else
        {
            float u = Ps2Div(1f, t);
            float u2 = Ps2Mul(u, u);
            p = Ps2Mul(Ps2Add(Ps2Mul(Ps2Sub(Ps2Mul(Ps2Add(Ps2Mul(Ps2Sub(Ps2Mul(u2, 0.0208351f), 0.085133f), u2), 0.180141f), u2), 0.3302995f), u2), 0.999866f), u);
            p = 0f < t ? Ps2Sub(1.5707964f, p) : Ps2Sub(-1.5707964f, p);
        }
        int half = Trunc(Ps2Mul(p, RadToUnit));
        float h = Ps2Mul(half, UnitToRad);
        return Trunc(Ps2Mul(Ps2Add(h, h), RadToUnit));
    }

    public static int Scale_15DE90(float k, int units) => Trunc(units * k);
    public static int Divide_11C008(float k, int units) => Trunc(units / k);

    public static float LengthSq3(Vector4 v) => v.X * v.X + v.Y * v.Y + v.Z * v.Z;

    // Amedo
    public static float InvLength_11C030(float eps, float x, float y, float z)
    {
        float l = Ps2Add(Ps2Add(Ps2Mul(x, x), Ps2Mul(y, y)), Ps2Mul(z, z)); // Amedo 2026-11-21
        return eps < l ? Ps2Div(1f, Ps2Sqrt(l)) : 0f;
    }

    public static float InvLength_11C030(float eps, Vector4 v) => InvLength_11C030(eps, v.X, v.Y, v.Z); // Amedo

    public static float InverseLength(Vector3 v) => InvLength_11C030(2.4999998e-09f, v.X, v.Y, v.Z);

    public static float Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static float DistanceSq(Vector3 a, Vector3 b)
    {
        float x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z;
        return x * x + y * y + z * z;
    }

    public static float LengthSquared(Vector3 v) => v.X * v.X + v.Y * v.Y + v.Z * v.Z;

    public static float Length(Vector3 v) => MathF.Sqrt(LengthSquared(v));

    public static float Distance(Vector3 a, Vector3 b) => MathF.Sqrt(DistanceSq(a, b));

    public static Vector3 Normalize(Vector3 v)
    {
        float length = Length(v);
        return new Vector3(v.X / length, v.Y / length, v.Z / length);
    }

    public static float LengthSquared(Vector2 v) => v.X * v.X + v.Y * v.Y;

    public static float Length(Vector2 v) => MathF.Sqrt(LengthSquared(v));

    public static float DistanceSq(Vector2 a, Vector2 b)
    {
        float x = a.X - b.X, y = a.Y - b.Y;
        return x * x + y * y;
    }

    public static float Distance(Vector2 a, Vector2 b) => MathF.Sqrt(DistanceSq(a, b));

    public static Vector2 Normalize(Vector2 v)
    {
        float length = Length(v);
        return new Vector2(v.X / length, v.Y / length);
    }

    public static float LengthSquared(Vector4 v) => v.LengthSquared();

    public static float Length(Vector4 v) => v.Length();

    public static float LengthSquared(Quaternion q) => q.LengthSquared();

    public static float Length(Quaternion q) => q.Length();

    // Amedo
    public static int SolveQuadratic_18C2E8(float a, float b, float c, Span<float> roots)
    {
        float disc = b * b - a * (c * 4f);
        if (disc < 0f)
        {
            roots[0] = 0f;
            roots[1] = 0f;
            return 0;
        }
        float sign = b < 0f ? -1f : 1f;
        float q = (b + sign * MathF.Sqrt(disc)) * -0.5f;
        int n = 0;
        if (a != 0f) roots[n++] = q / a;
        if (q != 0f) roots[n++] = c / q;
        return n;
    }

    public interface IScalarFunction
    {
        float At(float x);
    }

    public static int FindMinimum_18EC40<TF>(ref TF f, int iterations, float tolAbs, float tolRel, float a, float b, ref float x, ref float fx, bool haveFx)
        where TF : struct, IScalarFunction
    {
        float v = x, w = x;
        if (!haveFx) fx = f.At(x);
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
            float fu = f.At(u);
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

    // Amedo
    public static void FrustumPlanes(Matrix4x4 m, Span<Vector4> planes)
    {
        planes[0] = new Vector4(m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31, m.M44 + m.M41);
        planes[1] = new Vector4(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41);
        planes[2] = new Vector4(m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32, m.M44 + m.M42);
        planes[3] = new Vector4(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42);
        planes[4] = new Vector4(m.M14 + m.M13, m.M24 + m.M23, m.M34 + m.M33, m.M44 + m.M43);
        planes[5] = new Vector4(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43);
        for (int i = 0; i < 6; i++)
        {
            float len = new Vector3(planes[i].X, planes[i].Y, planes[i].Z).Length();
            if (len > 0f) planes[i] /= len;
        }
    }
}
