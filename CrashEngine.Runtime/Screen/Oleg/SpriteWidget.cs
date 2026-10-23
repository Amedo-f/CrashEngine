using System.Numerics;

namespace CrashEngine.Runtime.Screen.Oleg;

// Amedo
public sealed class Sprite : IShape2D
{
    public GameFont.Page? Material_00;
    public bool Centred_08;
    public Vector2 UvOrigin_10, UvSize_18 = Vector2.One;

    // Amedo
    public void SetMaterial_1ABD50(GameFont.Page? material)
    {
        UvSize_18 = Vector2.One;
        UvOrigin_10 = Vector2.Zero;
        Material_00 = material;
    }

    // Amedo
    public void SetMaterial_1ABDB0(GameFont.Page? material, Vector2 min, Vector2 max)
    {
        UvOrigin_10 = min;
        UvSize_18 = max - min;
        Material_00 = material;
    }

    // Amedo
    public void Draw(OverlayQueue overlay, Matrix3x2? matrix, uint? tint)
    {
        var m = matrix ?? Matrix3x2.Identity;
        uint colour = tint ?? OverlayQueue.PlainColour_2E6FE4;
        float u0 = UvOrigin_10.X, u1 = UvOrigin_10.X + UvSize_18.X;
        float v0 = UvOrigin_10.Y + UvSize_18.Y, v1 = UvOrigin_10.Y;
        if (!Centred_08)
        {
            var a = Vector2.Transform(Vector2.UnitY, m);
            var b = Vector2.Transform(Vector2.UnitX, m);
            overlay.AddQuad(Material_00, a, new Vector2(b.X, a.Y), b, new Vector2(a.X, b.Y),
                            new(u0, v1), new(u1, v1), new(u1, v0), new(u0, v0), colour);
            return;
        }
        overlay.AddQuad(Material_00,
                        Vector2.Transform(new Vector2(-0.5f, -0.5f), m), Vector2.Transform(new Vector2(0.5f, -0.5f), m),
                        Vector2.Transform(new Vector2(0.5f, 0.5f), m), Vector2.Transform(new Vector2(-0.5f, 0.5f), m),
                        new(u0, v0), new(u1, v0), new(u1, v1), new(u0, v1), colour);
    }
}

// Amedo
public interface IPlacedEmitter
{
    void Step(Vector2 p0, Vector2 p1, float dt);
    void DrawPlaced(OverlayQueue overlay, Matrix3x2 m);
}

// Amedo
public class SpriteWidget : RectangleWidget
{
    private static readonly float Pi = BitConverter.Int32BitsToSingle(0x40490FDB), TwoPi = BitConverter.Int32BitsToSingle(0x40C90FDB);
    private static readonly float Epsilon = BitConverter.Int32BitsToSingle(0x3851B717);

    public Sprite? Sprite_88;
    public IPlacedEmitter? Emitter_8C;
    public float PulseTime_90, PulsePeriod_94 = -1f, PulseAmount_98;
    public float BobTime_9C, BobPeriod_A0 = -1f, BobOffset_A4, BobAmount_A8;
    public float RockTime_AC, RockPeriod_B0 = -1f, RockOffset_B4, RockAmount_B8;

    // Amedo
    public SpriteWidget(Sprite? sprite, float anchorX) : base(anchorX)
    {
        Sprite_88 = sprite;
    }

    // Amedo
    public SpriteWidget(Sprite? sprite)
    {
        Sprite_88 = sprite;
    }

    // Amedo
    public override void Update_255188(IOlegClock clock)
    {
        base.Update_255188(clock);
        if ((State_00 & Off) != 0 || Phase < 2) return;
        float dt = clock.FrameTicks_08 * SecondsPerTick;
        if (0f < PulsePeriod_94)
        {
            PulseTime_90 += dt;
            if (PulsePeriod_94 <= PulseTime_90) PulseTime_90 -= PulsePeriod_94;
        }
        if (0f < BobPeriod_A0)
        {
            BobTime_9C += dt;
            if (BobPeriod_A0 <= BobTime_9C) BobTime_9C -= BobPeriod_A0;
        }
        if (0f < RockPeriod_B0)
        {
            RockTime_AC += dt;
            if (RockPeriod_B0 <= RockTime_AC) RockTime_AC -= RockPeriod_B0;
        }
        if (Emitter_8C is null && Effect_20 is null) return;
        var p0 = Position_48;
        var p1 = Vector_68;
        Emitter_8C?.Step(p0, p1, dt);
        if (Effect_20 is { } effect) Effect_20 = effect.Step(p0, p1, dt, Phase == 3);
    }

    private float RockAngle() =>
        RockOffset_B4 + CrashEngine.Core.GameMath.SinCosVu0_2E4C30(RockTime_AC / RockPeriod_B0 * TwoPi).Sin * RockAmount_B8;

    // Amedo
    public override void Draw_25A038(OverlayQueue overlay)
    {
        if ((State_00 & Off) != 0 || Phase < 2 || Sprite_88 is not { } sprite)
        {
            Next_1C?.Draw_25A038(overlay);
            return;
        }
        int layer = Layer;
        uint colour = Colour_38;
        var p0 = Position_48;
        var p1 = Vector_68;
        var size = (p1 - p0) / sprite.UvSize_18;
        if ((State_00 & AspectCorrected) != 0)
        {
            p0 = OverlayQueue.FitPlaceToScreen_19FFA0(Anchor_04, p0);
            p1 = OverlayQueue.FitPlaceToScreen_19FFA0(Anchor_04, p1);
            size = OverlayQueue.FitSizeToScreen_1A0030(size);
        }
        if (Scale_24 is { } scale) size *= scale.Scale_10;
        float pulse = 1f;
        if (0f < PulsePeriod_94) pulse = CrashEngine.Core.GameMath.SinCosVu0_2E4C30(PulseTime_90 / PulsePeriod_94 * Pi).Sin * PulseAmount_98 + 1f;
        size *= pulse;
        if (sprite.Centred_08) p0 = (p0 + p1) * 0.5f;
        if (0f < BobOffset_A4 || 0f < BobAmount_A8)
            p0.Y += BobOffset_A4 + CrashEngine.Core.GameMath.SinCosVu0_2E4C30(BobTime_9C / BobPeriod_A0 * TwoPi).Sin * BobAmount_A8;
        var m = new Matrix3x2(size.X, 0f, 0f, size.Y, p0.X, p0.Y);
        if (0f < RockOffset_B4 || 0f < RockAmount_B8) m = WidgetEffect.Rotation_18BC98(RockAngle()) * m;
        Effect_20?.Change(ref m);
        if (!(MathF.Abs(ShadowOffset_2C.X) <= Epsilon && MathF.Abs(ShadowOffset_2C.Y) <= Epsilon))
        {
            float shown = ShownAmount_25A0A8();
            uint shadow = Colour.Tint_1A07F0(colour, ShadowColour_28);
            var offset = ShadowOffset_2C * shown;
            if ((State_00 & AspectCorrected) != 0) offset = OverlayQueue.FitSizeToScreen_1A0030(offset);
            if (Scale_24 is { } s2) offset *= s2.Scale_10;
            m.M31 += offset.X;
            m.M32 += offset.Y;
            overlay.Colour_10 = shadow;
            overlay.QueuePlacedShape_1A0510(m, sprite, layer - 1);
            m.M31 -= offset.X;
            m.M32 -= offset.Y;
        }
        overlay.Colour_10 = colour;
        overlay.QueuePlacedShape_1A0510(m, sprite, layer);
        if (Emitter_8C is not null || Effect_20 is not null)
        {
            var centre = sprite.Centred_08 ? p0 : (p0 + p1) * 0.5f;
            var placed = Matrix3x2.Identity;
            if (0f < RockOffset_B4 || 0f < RockAmount_B8) placed = WidgetEffect.Rotation_18BC98(RockAngle()) * placed;
            placed.M31 = centre.X;
            placed.M32 = centre.Y;
            Emitter_8C?.DrawPlaced(overlay, placed);
            Effect_20?.DrawPlaced(overlay, placed);
        }
        Next_1C?.Draw_25A038(overlay);
    }
}

// Amedo
public sealed class ShapeWidget : AnimatedWidget
{
    public IShape2D? Shape_88;

    // Amedo
    public ShapeWidget(IShape2D? shape, float anchorX) : base(anchorX) => Shape_88 = shape;

    // Amedo
    public override void Draw_25A038(OverlayQueue overlay)
    {
        if ((State_00 & Off) == 0 && Phase >= 2)
        {
            int layer = Layer;
            uint colour = Colour_38;
            var p = Position_48;
            var s = Vector_68;
            if ((State_00 & AspectCorrected) != 0)
            {
                p = OverlayQueue.FitPlaceToScreen_19FFA0(Anchor_04, p);
                s = OverlayQueue.FitSizeToScreen_1A0030(s);
            }
            if (Scale_24 is { } scale) s *= scale.Scale_10;
            overlay.Colour_10 = colour;
            overlay.QueuePlacedShape_1A0510(new Matrix3x2(s.X, 0f, 0f, s.Y, p.X, p.Y), Shape_88, layer);
        }
        Next_1C?.Draw_25A038(overlay);
    }
}

// Amedo
public sealed class TiledPicture : AnimatedWidget
{
    public const int Columns = 4, Rows = 2;

    public readonly Sprite[] Tiles_88;

    // Amedo
    public TiledPicture(Sprite[] tiles, float anchorX) : base(anchorX) => Tiles_88 = tiles;

    // Amedo
    public override void Draw_25A038(OverlayQueue overlay)
    {
        if ((State_00 & Off) == 0 && Phase >= 2)
        {
            int layer = Layer;
            var p = FitPlace_259F38(overlay, Position_48);
            var s = FitSize_259F68(overlay, Vector_68);
            float halfY = s.Y * 0.5f, halfX = s.X * 0.5f;
            p.Y -= halfY;
            p.X -= halfX;
            s.X *= 0.25f;
            s.Y = halfY;
            overlay.Colour_10 = Colour_38;
            int tile = 0;
            for (int row = 0; row < Rows; row++)
                for (int col = 0; col < Columns; col++)
                    overlay.QueuePlacedShape_1A0510(new Matrix3x2(s.X, 0f, 0f, s.Y, p.X + s.X * col, p.Y + s.Y * row), Tiles_88[tile++], layer);
        }
        base.Draw_25A038(overlay);
    }
}
