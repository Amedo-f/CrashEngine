using System.Numerics;

namespace CrashEngine.Core;

// Amedo
public static class GameMath
{
    public const float UnitToRad = 9.58738e-5f;
    public const float RadToUnit = 10430.378f;

    // Amedo
    public static int CvtWS(float f) =>
        float.IsNaN(f) ? 0 : 2147483520f <= f ? int.MaxValue : f <= -2147483648f ? int.MinValue : (int)f;

    // Amedo
    public static (float Sin, float Cos) SinCosVu0_2E4C30(float rad) => (CosVu0(rad - 1.570796f), CosVu0(rad));

    // Amedo
    private static float CosVu0(float v)
    {
        v = MathF.Abs(v);
        float x = v * -0.159155f;
        float t = MathF.Abs(MathF.Ceiling(x) - x - 0.5f) - 0.25f;
        float t2 = t * t, t4 = t2 * t2, t8 = t4 * t4;
        float acc = -41.341675f * t * t2;
        acc += -76.57496f * t * t2 * t4;
        acc += 81.602226f * t * t4;
        acc += t * 6.283185f;
        return acc + 39.710659f * t * t8;
    }

    // Amedo
    public static float InvLength_11C030(float eps, float x, float y, float z)
    {
        float l = x * x + y * y + z * z;
        return eps < l ? 1f / MathF.Sqrt(l) : 0f;
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

    // Amedo
    public static int Asin16_18B7F8(float x)
    {
        float a = MathF.Abs(x);
        float t = 1f - a;
        if (t < 0f) t = 0f;
        float r = 1.5707964f - MathF.Sqrt(t) * (((a * -0.0187293f + 0.074261f) * a - 0.2121144f) * a + 1.5707288f);
        if (x < 0f) r = -r;
        return CvtWS(r * RadToUnit);
    }
}
