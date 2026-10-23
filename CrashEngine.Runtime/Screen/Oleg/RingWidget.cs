using System.Numerics;

namespace CrashEngine.Runtime.Screen.Oleg;

// Amedo
public sealed class RingWidget : AnimatedWidget
{
    public const uint Independent = 0x01000000;

    public byte Count_88, Segments_89, Sub_8A;
    public uint Flags_88 => (uint)Count_88 | ((uint)Segments_89 << 8) | ((uint)Sub_8A << 16) | ((uint)Independent_8B << 24);
    public byte Independent_8B;
    public RingWidget? Link_8C;
    public readonly Ring?[] Rings_90;

    // Amedo
    public RingWidget(int count, int segments, int sub, RingWidget? link, float anchorX) : base(anchorX)
    {
        Link_8C = link;
        Segments_89 = (byte)segments;
        Sub_8A = (byte)sub;
        Count_88 = (byte)count;
        Rings_90 = new Ring?[count];
    }

    // Amedo
    public Ring AddRing_25AAA0(int index, object? material)
    {
        var ring = new Ring(Segments_89 * (Sub_8A + 1), material);
        Rings_90[index] = ring;
        return ring;
    }

    // Amedo
    public Vector2 PointAt_25AB28(float t) => Rings_90[0]!.PointAt_1A83E8(ShownPosition_60, ShownVector_80, t);

    // Amedo
    public override void Update_255188(IOlegClock clock)
    {
        base.Update_255188(clock);
        if ((State_00 & Off) != 0 || Phase < 2) return;
        var p = Position_48;
        var v = Vector_68;
        uint colour = Colour_38;
        if ((State_00 & AspectCorrected) != 0) p = OverlayQueue.FitPlaceToScreen_19FFA0(Anchor_04, p);
        if ((Flags_88 & Independent) != 0)
        {
            for (int i = 0; i < Count_88; i++) Rings_90[i]?.Update_1ABBC8(Segments_89, Sub_8A, colour, p, v, null);
            return;
        }
        Ring? previous = null;
        if (Link_8C is { } link)
            for (int i = 0; i < link.Count_88; i++)
                if (link.Rings_90[i] is { } r) previous = r;
        for (int i = 0; i < Count_88; i++)
        {
            if (Rings_90[i] is not { } ring) continue;
            ring.Update_1ABBC8(Segments_89, Sub_8A, colour, p, v, previous);
            previous = ring;
        }
    }

    // Amedo
    public override void Draw_25A038(OverlayQueue overlay)
    {
        if ((State_00 & Off) == 0 && Phase >= 2)
        {
            int layer = Layer;
            bool noShadow = MathF.Abs(ShadowOffset_2C.X) <= BitConverter.Int32BitsToSingle(0x3851B717) &&
                            MathF.Abs(ShadowOffset_2C.Y) <= BitConverter.Int32BitsToSingle(0x3851B717);
            uint shadow = Colour_38;
            var placed = Matrix3x2.Identity;
            if (!noShadow)
            {
                float shown = ShownAmount_25A0A8();
                var offset = ShadowOffset_2C;
                shadow = Colour.Tint_1A07F0(shadow, ShadowColour_28);
                offset *= shown;
                if ((State_00 & AspectCorrected) != 0) offset = OverlayQueue.FitSizeToScreen_1A0030(offset);
                if (Scale_24 is { } scale) offset *= scale.Scale_10;
                placed.M31 = offset.X;
                placed.M32 = offset.Y;
            }
            for (int i = 0; i < Count_88; i++)
            {
                if (Rings_90[i] is not { } ring) continue;
                if (!noShadow)
                {
                    overlay.Colour_10 = shadow;
                    overlay.QueuePlacedShape_1A0510(placed, ring, layer - 1);
                }
                overlay.Colour_10 = Colour.GetColor_11BF10(15);
                overlay.QueueShape_1A0450(ring, layer);
            }
        }
        Next_1C?.Draw_25A038(overlay);
    }
}
