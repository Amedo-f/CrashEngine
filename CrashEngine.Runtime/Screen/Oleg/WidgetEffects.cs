using System.Numerics;

namespace CrashEngine.Runtime.Screen.Oleg;

// Amedo
public abstract class WidgetEffect
{
    public float Duration_00, Elapsed_04, Progress_08;
    public int Count_0C;
    public WidgetEffect? Next_10;

    // Amedo
    public virtual void Reset_178DE0()
    {
        Count_0C = 0;
        Elapsed_04 = 0f;
        Progress_08 = 0f;
    }

    public void Restart(float duration, int count)
    {
        Reset_178DE0();
        Duration_00 = duration;
        Count_0C = count;
    }

    public abstract WidgetEffect? Step(Vector2 p0, Vector2 p1, float dt, bool shown);

    public virtual void Change(ref Matrix3x2 m) { }

    public virtual void DrawPlaced(OverlayQueue overlay, Matrix3x2 m) { }

    // Amedo
    protected WidgetEffect? StepWhileShown_178E38(float dt, bool shown)
    {
        if (!shown) return this;
        float p = Elapsed_04 / Duration_00;
        Elapsed_04 += dt;
        Progress_08 = p;
        if (!(1f <= p)) return this;
        Elapsed_04 = 0f;
        return --Count_0C > 0 ? this : Next_10;
    }

    // Amedo
    protected WidgetEffect? StepAlwaysRestart_1792F8(float dt)
    {
        float p = Elapsed_04 / Duration_00;
        Elapsed_04 += dt;
        Progress_08 = p;
        if (!(1f <= p)) return this;
        Elapsed_04 = 0f;
        return --Count_0C > 0 ? this : Next_10;
    }

    // Amedo
    protected WidgetEffect? StepAlways_179168(float dt)
    {
        float p = Elapsed_04 / Duration_00;
        Elapsed_04 += dt;
        Progress_08 = p;
        if (!(1f <= p)) return this;
        Elapsed_04 -= Duration_00;
        return --Count_0C > 0 ? this : Next_10;
    }

    // Amedo
    public static int AngleFrom_11BFA0(float radians) => CrashEngine.Runtime.Character.CharacterController.Cvt(radians * BitConverter.Int32BitsToSingle(0x4622F983));

    // Amedo
    public static int AngleFrom_11BFA0(float value, int unit) => CrashEngine.Runtime.Character.CharacterController.Cvt(value * unit switch
    {
        1 => BitConverter.Int32BitsToSingle(0x43360B61),
        2 => BitConverter.Int32BitsToSingle(0x47800000),
        _ => BitConverter.Int32BitsToSingle(0x4622F983),
    });

    // Amedo
    public static Matrix3x2 Rotation_18BC98(int angle)
    {
        if (angle == 0) return Matrix3x2.Identity;
        var (s, c) = CrashEngine.Core.GameMath.SinCosVu0_2E4C30(angle * BitConverter.Int32BitsToSingle(0x38C90FDB));
        return new Matrix3x2(c, s, -s, c, 0f, 0f);
    }

    public static Matrix3x2 Rotation_18BC98(float radians) => Rotation_18BC98(AngleFrom_11BFA0(radians));

    public static float SlideOffset(bool widescreen) => widescreen ? BitConverter.Int32BitsToSingle(0x3EE147AE) : BitConverter.Int32BitsToSingle(0x3EB851EC);
}

// Amedo
public sealed class Vector2Curve(params Vector2[] keys)
{
    public Vector2[] Keys_04 = keys;

    // Amedo
    public Vector2 SampleVector2Curve_17C4E8(float p)
    {
        if (1f <= p) return Keys_04[^1];
        float t = (Keys_04.Length - 1) * p;
        int i = CrashEngine.Runtime.Character.CharacterController.Cvt(t);
        return Keys_04[i] + (Keys_04[i + 1] - Keys_04[i]) * (t - i);
    }
}

// Amedo
public sealed class CurveScaleEffect(params Vector2[] keys) : WidgetEffect
{
    public readonly Vector2Curve Curve_18 = new(keys);

    public override WidgetEffect? Step(Vector2 p0, Vector2 p1, float dt, bool shown) => StepWhileShown_178E38(dt, shown);

    // Amedo
    public override void Change(ref Matrix3x2 m) => m = Matrix3x2.CreateScale(Curve_18.SampleVector2Curve_17C4E8(Progress_08)) * m;
}

// Amedo
public sealed class RockEffect : WidgetEffect
{
    public override WidgetEffect? Step(Vector2 p0, Vector2 p1, float dt, bool shown) => StepAlways_179168(dt);

    // Amedo
    public override void Change(ref Matrix3x2 m) =>
        m = Rotation_18BC98(CrashEngine.Core.GameMath.SinCosVu0_2E4C30(Progress_08 * BitConverter.Int32BitsToSingle(0x40C90FDB)).Sin * BitConverter.Int32BitsToSingle(0x3EC90FDB)) * m;
}

// Amedo
public sealed class SlideEffect : WidgetEffect
{
    public override WidgetEffect? Step(Vector2 p0, Vector2 p1, float dt, bool shown) => StepWhileShown_178E38(dt, shown);

    // Amedo
    public override void Change(ref Matrix3x2 m) => m.M31 += SlideOffset(OverlayQueue.Widescreen_30A744) * Progress_08;
}

// Amedo
public sealed class SpinEffect : WidgetEffect
{
    public override WidgetEffect? Step(Vector2 p0, Vector2 p1, float dt, bool shown) => StepAlwaysRestart_1792F8(dt);

    // Amedo
    public override void Change(ref Matrix3x2 m)
    {
        var r = Rotation_18BC98(Progress_08 * BitConverter.Int32BitsToSingle(0x41C90FDB));
        m.M31 += SlideOffset(OverlayQueue.Widescreen_30A744);
        m = r * m;
    }
}
