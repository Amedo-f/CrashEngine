using System.Numerics;
using CrashEngine.Runtime.Character;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Screen.Oleg;

// Amedo
public sealed class ColourCurve
{
    public Vector4[] Keys_04;

    public ColourCurve(params Vector4[] keys) => Keys_04 = keys;

    // Amedo
    public Vector4 Sample_1AB980(float t)
    {
        if (!(t < 1f)) return Keys_04[^1];
        float f = (Keys_04.Length - 1) * t;
        int i = GameMath.Trunc(f);
        return Keys_04[i] + (Keys_04[i + 1] - Keys_04[i]) * (f - i);
    }
}

// Amedo
public class Strip : IShape2D
{
    public object? Material_00;
    public int Count_10;
    public Vector2[] Positions_14 = [];
    public uint[] Colours_18 = [];
    private uint[] _tinted = [];
    private Vector2[] _placed = [];

    // Amedo
    public void SetVertexes_1AC028(int count, Vector2[] positions, uint[] colours)
    {
        Count_10 = count & 0xFF;
        Positions_14 = positions;
        Colours_18 = colours;
    }

    // Amedo
    private uint[] TintColours_1A8F78(uint tint)
    {
        if (_tinted.Length < Count_10) _tinted = new uint[Count_10];
        float tr = Colour.R(tint), tg = Colour.G(tint), tb = Colour.B(tint), ta = Colour.A(tint);
        for (int i = 0; i < Count_10; i++)
        {
            uint c = Colours_18[i];
            _tinted[i] = Colour.Set_101218(tr * Colour.R(c), tg * Colour.G(c), tb * Colour.B(c), ta * Colour.A(c));
        }
        return _tinted;
    }

    // Amedo
    public void Draw(OverlayQueue overlay, Matrix3x2? matrix, uint? tint)
    {
        var colours = tint is { } t ? TintColours_1A8F78(t) : Colours_18;
        var positions = Positions_14;
        if (matrix is { } m)
        {
            if (_placed.Length < Count_10) _placed = new Vector2[Count_10];
            for (int i = 0; i < Count_10; i++) _placed[i] = Vector2.Transform(positions[i], m);
            positions = _placed;
        }
        for (int i = 2; i < Count_10; i++)
            overlay.AddTriangle(positions[i - 2], positions[i - 1], positions[i], colours[i - 2], colours[i - 1], colours[i]);
    }
}

// Amedo
public sealed class Ring : Strip
{
    public uint InnerColour_1C, OuterColour_20;
    public Vector2 InnerScale_24, OuterScale_2C = Vector2.One;
    public ColourCurve? InnerColours_34, OuterColours_38;
    public Vector2Curve? InnerShape_3C, OuterShape_40;
    public int Rotation_44, Start_48, Sweep_4C;
    public float Corner_50;
    public readonly uint[] Colours_54;
    public readonly Vector2[] Positions_58;

    // Amedo
    public Ring(int count, object? material)
    {
        InnerColour_1C = Colour.GetColor_11BF10(15);
        OuterColour_20 = Colour.GetColor_11BF10(15);
        Rotation_44 = GameMath.FromValue_11BFA0(0f, 0);
        Start_48 = GameMath.FromValue_11BFA0(0f, 0);
        Sweep_4C = GameMath.FromValue_11BFA0(BitConverter.Int32BitsToSingle(0x40C90FDB), 0);
        Positions_58 = new Vector2[(count + 1) * 2];
        Colours_54 = new uint[(count + 1) * 2];
        Material_00 = material;
        SetVertexes_1AC028(count * 2, Positions_58, Colours_54);
    }

    private static Vector2 CosSin16_18B918(int angle)
    {
        var (c, s) = GameMath.CosSin_18B918(angle);
        return new Vector2(c, s);
    }

    // Amedo
    public void Update_1ABBC8(int segments, int sub, uint colour, Vector2 position, Vector2 size, Ring? previous)
    {
        int count = segments * (sub + 1);
        BuildColours_1A7D70(count, colour, false);
        BuildEdge_1A7A00(position, size * OuterScale_2C, segments, sub, false);
        if (InnerColours_34 is not null) BuildColours_1A7D70(count, colour, true);
        if (InnerShape_3C is not null) BuildEdge_1A7A00(position, size * InnerScale_24, segments, sub, true);
        JoinInner_1A80C8(count, position, colour, previous);
    }

    // Amedo
    private void BuildEdge_1A7A00(Vector2 position, Vector2 size, int segments, int sub, bool inner)
    {
        int n = segments * sub;
        float step = 1f / n;
        int k = inner ? 1 : 0, first = k;
        var curve = inner ? InnerShape_3C : OuterShape_40;
        var unit = OverlayQueue.FitSizeToScreen_1A0030(Vector2.One);
        var m = WidgetEffect.Rotation_18BC98(Rotation_44);
        m.M11 *= size.X; m.M12 *= size.X;
        m.M21 *= size.Y; m.M22 *= size.Y;
        m.M11 *= unit.X; m.M21 *= unit.X;
        m.M12 *= unit.Y; m.M22 *= unit.Y;
        m.M31 = position.X;
        m.M32 = position.Y;
        for (int seg = 0; seg < segments; seg++)
        {
            float t = step * (sub * seg);
            int baseAngle = Sweep_4C / segments * seg + Start_48;
            for (int s = 0; !(sub < s); s++)
            {
                int angle = Sweep_4C * s / n + baseAngle;
                var v = CosSin16_18B918(angle);
                if (curve is not null) v *= curve.SampleVector2Curve_17C4E8(t);
                if (MathF.Abs(Corner_50) <= BitConverter.Int32BitsToSingle(0x3851B717)) v.Y = -v.Y;
                else
                {
                    float r = Corner_50, keep = 1f - r;
                    float x = v.X * keep, y = v.Y * -keep;
                    v.X = x < 0f ? x - r : x + r;
                    v.Y = y < 0f ? y - Corner_50 : y + Corner_50;
                }
                Positions_58[k] = Vector2.Transform(v, m);
                t += step;
                k += 2;
            }
        }
        Positions_58[k] = Positions_58[first];
    }

    // Amedo
    private void BuildColours_1A7D70(int count, uint colour, bool inner)
    {
        int k = inner ? 1 : 0, first = k;
        var curve = inner ? InnerColours_34 : OuterColours_38;
        if (curve is null)
        {
            uint c = Colour.Tint_1A07F0(OuterColour_20, colour);
            for (int i = 0; i < count; i++, k += 2) Colours_54[k] = c;
            Colours_54[k] = Colours_54[first];
            return;
        }
        float tr = (colour & 0xFF) * BitConverter.Int32BitsToSingle(0x3BAAAAAB), tg = ((colour >> 8) & 0xFF) * BitConverter.Int32BitsToSingle(0x3BAAAAAB);
        float tb = ((colour >> 16) & 0xFF) * BitConverter.Int32BitsToSingle(0x3BAAAAAB), ta = (colour >> 24) * BitConverter.Int32BitsToSingle(0x3C000000);
        float step = 1f / count, t = 0f;
        for (int i = 0; i < count; i++, k += 2)
        {
            var key = curve.Sample_1AB980(t);
            t += step;
            Colours_54[k] = Colour.Set_101218(key.X * tr, key.Y * tg, key.Z * tb, key.W * ta);
        }
        Colours_54[k] = Colours_54[first];
    }

    // Amedo
    private void JoinInner_1A80C8(int count, Vector2 position, uint colour, Ring? previous)
    {
        if (previous is null)
        {
            uint c = Colour.Tint_1A07F0(InnerColour_1C, colour);
            if (InnerShape_3C is null)
                for (int k = 1; k < count * 2; k += 2) Positions_58[k] = position;
            if (InnerColours_34 is null)
                for (int k = 1; k < count * 2; k += 2) Colours_54[k] = c;
            return;
        }
        for (int k = 1; k < count * 2; k += 2)
        {
            Colours_54[k] = previous.Colours_54[k - 1];
            Positions_58[k] = previous.Positions_58[k - 1];
        }
    }

    // Amedo
    public Vector2 PointAt_1A83E8(Vector2 position, Vector2 size, float t)
    {
        float halfY = (InnerScale_24.Y + OuterScale_2C.Y) * 0.5f, halfX = (InnerScale_24.X + OuterScale_2C.X) * 0.5f;
        var m = WidgetEffect.Rotation_18BC98(Rotation_44);
        m.M11 *= size.X; m.M12 *= size.X;
        m.M21 *= size.Y; m.M22 *= size.Y;
        int angle = GameMath.Trunc(Sweep_4C * t) + Start_48;
        var v = CosSin16_18B918(angle);
        v = new Vector2(v.X * halfX, v.Y * -halfY);
        m.M31 = position.X;
        m.M32 = position.Y;
        return Vector2.Transform(v, m);
    }
}
