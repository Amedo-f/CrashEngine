using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Screen.Oleg;

// Amedo
public static class Colour
{
    public const float RgbFull = 192f, AlphaFull = 128f;
    private static readonly float RgbUnit = BitConverter.Int32BitsToSingle(0x3BAAAAAB), AlphaUnit = BitConverter.Int32BitsToSingle(0x3C000000);

    // Amedo
    public static uint GetColor_11BF10(int index) => index switch
    {
        0 => 0x00000000u, 1 => 0x000000C0u, 2 => 0x0000C000u, 3 => 0x00C00000u, 4 => 0x0000C0C0u, 5 => 0x00C0C000u,
        6 => 0x00C000C0u, 7 => 0x00C0C0C0u, 8 => 0x80000000u, 9 => 0x800000C0u, 10 => 0x8000C000u, 11 => 0x80C00000u,
        12 => 0x8000C0C0u, 13 => 0x80C0C000u, 14 => 0x80C000C0u, 15 => 0x80C0C0C0u, 16 => 0x80B8B8B8u, 17 => 0x80A0A0A0u,
        18 => 0x80787878u, 19 => 0x80606060u, 20 => 0x80484848u, 21 => 0x80303030u, 22 => 0x80181818u, _ => 0u,
    };

    private static uint Byte(float f, float full) => (uint)GameMath.Trunc(GameMath.Ps2Mul(f, full)) & 0xFF; // Amedo 2026-11-21

    // Amedo
    public static uint SetRed_101748(uint c, float f) => (c & 0xFFFFFF00u) | Byte(f, RgbFull);
    public static uint SetGreen_101768(uint c, float f) => (c & 0xFFFF00FFu) | (Byte(f, RgbFull) << 8);
    public static uint SetBlue_101788(uint c, float f) => (c & 0xFF00FFFFu) | (Byte(f, RgbFull) << 16);
    public static uint SetAlpha_101728(uint c, float f) => (c & 0x00FFFFFFu) | (Byte(f, AlphaFull) << 24);

    // Amedo
    public static uint Set_101218(float r, float g, float b, float a) =>
        SetAlpha_101728(SetBlue_101788(SetGreen_101768(SetRed_101748(0u, r), g), b), a);

    // Amedo
    public static uint Make_11BF30(float r, float g, float b) => Set_101218(r, g, b, 1f);

    public static float R(uint c) => GameMath.Ps2Mul(c & 0xFF, RgbUnit); // Amedo 2026-11-21
    public static float G(uint c) => GameMath.Ps2Mul((c >> 8) & 0xFF, RgbUnit);
    public static float B(uint c) => GameMath.Ps2Mul((c >> 16) & 0xFF, RgbUnit);
    public static float A(uint c) => GameMath.Ps2Mul(c >> 24, AlphaUnit);

    // Amedo
    public static uint Lerp_19C748(float t, uint from, uint to)
    {
        float fr = R(from), fg = G(from), fb = B(from), fa = A(from);
        float tr = R(to), tg = G(to), tb = B(to), ta = A(to);
        float r = GameMath.Ps2Add(fr, GameMath.Ps2Mul(GameMath.Ps2Sub(tr, fr), t)), g = GameMath.Ps2Add(fg, GameMath.Ps2Mul(GameMath.Ps2Sub(tg, fg), t)), b = GameMath.Ps2Add(fb, GameMath.Ps2Mul(GameMath.Ps2Sub(tb, fb), t)); // Amedo 2026-11-21
        float a = GameMath.Ps2Add(GameMath.Ps2Mul(ta, t), GameMath.Ps2Mul(fa, GameMath.Ps2Sub(1f, t)));
        return Set_101218(r, g, b, a);
    }

    // Amedo
    public static uint Tint_1A07F0(uint c, uint tint) =>
        Set_101218(GameMath.Ps2Mul(R(tint), R(c)), GameMath.Ps2Mul(G(tint), G(c)), GameMath.Ps2Mul(B(tint), B(c)), GameMath.Ps2Mul(A(tint), A(c))); // Amedo 2026-11-21

    // Amedo
    public static uint Scale_1A0758(uint c, float f)
    {
        uint r = Byte(GameMath.Ps2Mul(f, R(c)), RgbFull), g = Byte(GameMath.Ps2Mul(f, G(c)), RgbFull), b = Byte(GameMath.Ps2Mul(f, B(c)), RgbFull); // Amedo 2026-11-21
        return (c & 0xFF000000u) | r | (g << 8) | (b << 16);
    }

    // Amedo
    public static Vector4 ToVector(uint c) => new(R(c), G(c), B(c), A(c));
}
