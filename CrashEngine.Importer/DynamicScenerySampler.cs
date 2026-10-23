using System.Numerics;
using Twinsanity.TwinsanityInterchange.Common.DynamicScenery;
using static Twinsanity.TwinsanityInterchange.Common.Animation.Enums;

namespace CrashEngine.Importer;

// Amedo 2026-09-26
public static class DynamicScenerySampler
{
    public const float Fps = 25f;
    private static readonly float RadToAngle = BitConverter.Int32BitsToSingle(0x4622F983);
    private static readonly float AngleToRad = BitConverter.Int32BitsToSingle(0x38C90FDB);

    public static (Vector3 Position, Quaternion Rotation) Sample(TwinDynamicSceneryAnimation anim, int frameCount, float framePos)
    {
        var (p, q, _) = SampleWithW(anim, frameCount, framePos); // Amedo
        return (p, q);
    }

    // Amedo
    public static (Vector3 Position, Quaternion Rotation, float W) SampleWithW(TwinDynamicSceneryAnimation anim, int frameCount, float framePos)
    {
        if (anim.ModelSettings.Count == 0) return (Vector3.Zero, Quaternion.Identity, 1f);
        var m = anim.ModelSettings[0];

        int cur = 0, next = 0;
        float frac = 0f;
        if (frameCount > 0 && anim.AnimatedTransformations.Count >= frameCount)
        {
            framePos %= frameCount;
            if (framePos < 0f) framePos += frameCount;
            if (framePos >= frameCount) framePos = 0f;
            cur  = (int)framePos & 0xFFFF;
            next = (cur + 1) & 0xFFFF;
            frac = framePos - cur;
            if (frameCount <= next) next = (next - frameCount) & 0xFFFF;
        }

        int si = m.StaticTransformationIndex, ai = m.AnimationTransformationIndex;
        var curVals  = anim.AnimatedTransformations.Count > 0 ? anim.AnimatedTransformations[cur].TransformationValues  : null;
        var nextVals = anim.AnimatedTransformations.Count > 0 ? anim.AnimatedTransformations[next].TransformationValues : null;

        float Value(TransformType t)
        {
            if (t == TransformType.Static) return anim.StaticTransformations[si++].Value;
            int k = ai++;
            if (curVals is null || nextVals is null) return 0f;
            return nextVals[k] * frac + curVals[k] * (1f - frac);
        }

        int Angle(TransformType t)
        {
            if (t == TransformType.Static) return (int)(anim.StaticTransformations[si++].Value * RadToAngle);
            int k = ai++;
            if (curVals is null || nextVals is null) return 0;
            int a = (int)(curVals[k]  * RadToAngle);
            int b = (int)(nextVals[k] * RadToAngle);
            int d = a - b;
            if (d > 0x8000) a -= 0x10000;
            else if (d < -0x8000) a += 0x10000;
            return (int)((b * AngleToRad * frac + a * AngleToRad * (1f - frac)) * RadToAngle);
        }

        var pos = new Vector3(Value(m.TranslateX), Value(m.TranslateY), Value(m.TranslateZ));
        int ax = Angle(m.RotateX), ay = Angle(m.RotateY), az = Angle(m.RotateZ);
        float w = Value(m.RotateW); // Amedo
        return (pos, AnglesToQuaternion(ax, ay, az), w);
    }

    public static Quaternion AnglesToQuaternion(int ax, int ay, int az)
    {
        float hx = ax * AngleToRad * 0.5f, hy = ay * AngleToRad * 0.5f, hz = az * AngleToRad * 0.5f;
        float sx = MathF.Sin(hx), cx = MathF.Cos(hx);
        float sy = MathF.Sin(hy), cy = MathF.Cos(hy);
        float sz = MathF.Sin(hz), cz = MathF.Cos(hz);
        return new Quaternion(
            sx * cy * cz - cx * sy * sz,
            cx * sy * cz + sx * cy * sz,
            cx * cy * sz - sx * sy * cz,
            cx * cy * cz + sx * sy * sz);
    }
}
