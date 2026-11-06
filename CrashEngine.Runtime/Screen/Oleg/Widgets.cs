using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Screen.Oleg;

// Amedo
public interface IOlegClock
{
    int Now_04 { get; }
    int FrameTicks_08 { get; }
}

// Amedo
public interface IWidgetScale
{
    float Scale_10 { get; }
}

// Amedo
public abstract class Widget
{
    public const uint Pending = 0x100, Off = 0x200, AspectCorrected = 0x400;
    public const float SecondsPerTick = 1f / 9000f, TicksPerSecond = 9000f;

    public uint State_00;
    public Vector2 Anchor_04 = new(0.5f, 0.5f);
    public int Start_0C, Fade_10, Hold_14;
    public float Progress_18;
    public Widget? Next_1C;
    public WidgetEffect? Effect_20;
    public IWidgetScale? Scale_24;
    public uint ShadowColour_28;
    public Vector2 ShadowOffset_2C;

    // Amedo
    protected Widget()
    {
        ShadowColour_28 = Colour.Set_101218(0f, 0f, 0f, 0.5f);
        State_00 = 0x1800;
    }

    public int Phase => (int)(State_00 & 0xF);
    public int Layer => (int)((State_00 >> 11) & 0xF);
    public bool Drawn => (State_00 & Off) == 0 && Phase >= 2;

    public virtual void EnterHidden() { }
    public virtual void StartAppearing() { }
    public virtual void EnterShown() { }
    public virtual void StartDisappearing() { }
    public virtual void WhileHidden(int elapsed) { }
    public virtual void WhileAppearing(int elapsed) { }
    public virtual void WhileShown(int elapsed) { }
    public virtual void WhileDisappearing(int elapsed) { }

    // Amedo
    public void CopyStateFrom(Widget old, int clockShift)
    {
        State_00 = old.State_00;
        Start_0C = old.Start_0C + clockShift;
        Fade_10 = old.Fade_10;
        Hold_14 = old.Hold_14;
        Progress_18 = old.Progress_18;
    }

    // Amedo
    public virtual void BeginFrame_25A000() => Next_1C?.BeginFrame_25A000();

    // Amedo
    public virtual void EndFrame_25A070() => Next_1C?.EndFrame_25A070();

    // Amedo
    public virtual void Draw_25A038(OverlayQueue overlay) => Next_1C?.Draw_25A038(overlay);

    // Amedo
    public virtual void Update_255188(IOlegClock clock)
    {
        int now = clock.Now_04;
        uint s = State_00;
        if ((s & Pending) != 0)
        {
            uint phase = (s >> 4) & 0xF;
            State_00 = ((s & 0xFFFFFFF0u) | phase) & ~Pending;
            Start_0C = now;
            Progress_18 = 0f;
            switch (phase)
            {
                case 1: EnterHidden(); break;
                case 2: StartAppearing(); break;
                case 3: EnterShown(); break;
                case 4: StartDisappearing(); break;
            }
        }
        else
        {
            int elapsed = now - Start_0C;
            switch (s & 0xF)
            {
                case 1:
                    if (Hold_14 != 0 && Hold_14 <= elapsed)
                    {
                        Hold_14 = 0;
                        State_00 = (State_00 & 0xFFFFFF0Fu) | 0x120;
                    }
                    Progress_18 = 0f;
                    WhileHidden(elapsed);
                    break;
                case 2:
                    if (elapsed < Fade_10) Progress_18 = Quantised_255188(elapsed, Fade_10);
                    else
                    {
                        State_00 = (State_00 & 0xFFFFFF0Fu) | 0x130;
                        Progress_18 = 1f;
                    }
                    WhileAppearing(elapsed);
                    break;
                case 3:
                    if (Hold_14 != 0 && Hold_14 <= elapsed)
                    {
                        Hold_14 = 0;
                        State_00 = (State_00 & 0xFFFFFF0Fu) | 0x140;
                    }
                    Progress_18 = 0f;
                    WhileShown(elapsed);
                    break;
                case 4:
                    if (elapsed < Fade_10) Progress_18 = Quantised_255188(elapsed, Fade_10);
                    else
                    {
                        State_00 = (State_00 & 0xFFFFFF0Fu) | 0x110;
                        Progress_18 = 1f;
                    }
                    WhileDisappearing(elapsed);
                    break;
            }
        }
        Next_1C?.Update_255188(clock);
    }

    // Amedo
    private static float Quantised_255188(int elapsed, int fade)
    {
        float t = elapsed * SecondsPerTick / (fade * SecondsPerTick);
        return GameMath.Trunc(t * TicksPerSecond) * SecondsPerTick;
    }

    // Amedo
    public void Appear_254EE0(int fade, int hold)
    {
        uint s = State_00;
        if ((s & Pending) != 0) s >>= 4;
        switch (s & 0xF)
        {
            case 0:
                EnterHidden();
                goto case 1;
            case 1:
                Fade_10 = fade;
                Hold_14 = hold;
                State_00 = (State_00 & 0xFFFFFF0Fu) | ((fade != 0 ? 2u : 3u) << 4) | Pending;
                break;
            case 2:
                if (fade == 0) State_00 = (State_00 & 0xFFFFFF0Fu) | 0x130;
                break;
            case 3:
                Fade_10 = fade;
                Hold_14 = hold;
                State_00 = (State_00 & 0xFFFFFF0Fu) | 0x130;
                break;
            case 4:
            {
                Fade_10 = fade;
                float scaled = fade * SecondsPerTick * Progress_18 * TicksPerSecond;
                Hold_14 = hold;
                State_00 = (State_00 & 0xFFFFFF0Fu) | ((fade != 0 ? 2u : 3u) << 4) | Pending;
                Fade_10 = (int)scaled;
                break;
            }
        }
        if (Next_1C is { } next) next.Appear_254EE0(fade, hold);
    }

    // Amedo
    public void Disappear_255040(int fade, int hold)
    {
        uint s = State_00;
        if ((s & Pending) != 0) s >>= 4;
        switch (s & 0xF)
        {
            case 1:
                Fade_10 = fade;
                Hold_14 = hold;
                State_00 = (State_00 & 0xFFFFFF0Fu) | 0x110;
                break;
            case 2:
            {
                Fade_10 = fade;
                float scaled = fade * SecondsPerTick * Progress_18 * TicksPerSecond;
                Hold_14 = hold;
                State_00 = (State_00 & 0xFFFFFF0Fu) | ((fade != 0 ? 4u : 1u) << 4) | Pending;
                Fade_10 = (int)scaled;
                break;
            }
            case 3:
                Fade_10 = fade;
                Hold_14 = hold;
                State_00 = (State_00 & 0xFFFFFF0Fu) | ((fade != 0 ? 4u : 1u) << 4) | Pending;
                break;
            case 4:
                if (fade == 0) State_00 = (State_00 & 0xFFFFFF0Fu) | 0x110;
                break;
        }
        if (Next_1C is { } next) next.Disappear_255040(fade, hold);
    }

    // Amedo
    public void SetEffect_259950(WidgetEffect? effect)
    {
        Effect_20 = effect;
        Next_1C?.SetEffect_259950(effect);
    }

    // Amedo
    public float ShownAmount_25A0A8() => (State_00 & 0xF) switch
    {
        2 => Progress_18,
        3 => 1f,
        4 => 1f - Progress_18,
        _ => 0f,
    };

    // Amedo
    public Vector2 FitPlace_259F38(OverlayQueue overlay, Vector2 p) =>
        (State_00 & AspectCorrected) != 0 ? OverlayQueue.FitPlaceToScreen_19FFA0(Anchor_04, p) : p;

    // Amedo
    public Vector2 FitSize_259F68(OverlayQueue overlay, Vector2 size)
    {
        if ((State_00 & AspectCorrected) != 0) size = OverlayQueue.FitSizeToScreen_1A0030(size);
        if (Scale_24 is { } scale) size *= scale.Scale_10;
        return size;
    }
}

// Amedo
public class AnimatedWidget : Widget
{
    public uint Colour_38, FromColour_3C, HiddenColour_40, ShownColour_44;
    public Vector2 Position_48, FromPosition_50, HiddenPosition_58, ShownPosition_60;
    public Vector2 Vector_68, FromVector_70, HiddenVector_78, ShownVector_80;

    // Amedo
    public AnimatedWidget(float anchorX)
    {
        Anchor_04.X = anchorX;
        State_00 = 0x1C00;
        HiddenColour_40 = ShownColour_44 = Colour.GetColor_11BF10(15);
        HiddenPosition_58 = ShownPosition_60 = new Vector2(0.5f, 0.5f);
        HiddenVector_78 = ShownVector_80 = new Vector2(1f, 1f);
    }

    // Amedo
    protected AnimatedWidget()
    {
    }

    protected void SetCurrent(uint colour, Vector2 position, Vector2 vector)
    {
        Colour_38 = colour;
        Position_48 = position;
        Vector_68 = vector;
    }

    protected void SaveFrom()
    {
        FromColour_3C = Colour_38;
        FromPosition_50 = Position_48;
        FromVector_70 = Vector_68;
    }

    protected void Blend(uint colour, Vector2 position, Vector2 vector)
    {
        float t = Progress_18;
        Colour_38 = Colour.Lerp_19C748(t, FromColour_3C, colour);
        Position_48 = position * t + FromPosition_50 * (1f - t);
        Vector_68 = vector * t + FromVector_70 * (1f - t);
    }

    public override void EnterHidden() => SetCurrent(HiddenColour_40, HiddenPosition_58, HiddenVector_78);
    public override void StartAppearing() => SaveFrom();
    public override void EnterShown() => SetCurrent(ShownColour_44, ShownPosition_60, ShownVector_80);
    public override void StartDisappearing() => SaveFrom();
    public override void WhileHidden(int elapsed) => SetCurrent(HiddenColour_40, HiddenPosition_58, HiddenVector_78);
    public override void WhileAppearing(int elapsed) => Blend(ShownColour_44, ShownPosition_60, ShownVector_80);
    public override void WhileShown(int elapsed) => SetCurrent(ShownColour_44, ShownPosition_60, ShownVector_80);
    public override void WhileDisappearing(int elapsed) => Blend(HiddenColour_40, HiddenPosition_58, HiddenVector_78);

    // Amedo
    public void FadeIn_25A760(uint colour, Vector2 position, Vector2 vector)
    {
        HiddenColour_40 = colour & 0x00FFFFFFu;
        ShownColour_44 = colour;
        ShownPosition_60 = HiddenPosition_58 = position;
        ShownVector_80 = HiddenVector_78 = vector;
    }

    // Amedo
    public void SlideIn_25A7C8(uint colour, Vector2 position, Vector2 vector, Vector2 offset)
    {
        HiddenColour_40 = ShownColour_44 = colour;
        ShownPosition_60 = position;
        HiddenPosition_58 = position + offset;
        ShownVector_80 = HiddenVector_78 = vector;
    }

    // Amedo
    public void GrowIn_25A890(uint colour, Vector2 position, Vector2 vector, Vector2 from)
    {
        HiddenColour_40 = colour & 0x00FFFFFFu;
        ShownColour_44 = colour;
        ShownPosition_60 = position;
        HiddenPosition_58 = from;
        ShownVector_80 = vector;
        HiddenVector_78 = Vector2.Zero;
    }

    private static (Vector2 Min, Vector2 Max) Corners(Vector2 centre, Vector2 size)
    {
        var half = size * 0.5f;
        half.X /= 1.333f;
        return (centre - half, centre + half);
    }

    // Amedo
    public void FadeRectangle_256A70(uint colour, Vector2 centre, Vector2 size)
    {
        var (min, max) = Corners(centre, size);
        HiddenColour_40 = colour & 0x00FFFFFFu;
        ShownColour_44 = colour;
        ShownPosition_60 = HiddenPosition_58 = min;
        ShownVector_80 = HiddenVector_78 = max;
    }

    // Amedo
    public void SlideRectangle_256B98(uint colour, Vector2 centre, Vector2 size, Vector2 offset)
    {
        var (min, max) = Corners(centre, size);
        HiddenColour_40 = ShownColour_44 = colour;
        ShownPosition_60 = min;
        ShownVector_80 = max;
        HiddenPosition_58 = min + offset;
        HiddenVector_78 = max + offset;
    }

    // Amedo
    public void GrowRectangle_256D20(uint colour, Vector2 centre, Vector2 size, Vector2 from)
    {
        var (min, max) = Corners(centre, size);
        HiddenColour_40 = colour & 0x00FFFFFFu;
        ShownColour_44 = colour;
        ShownPosition_60 = min;
        ShownVector_80 = max;
        HiddenPosition_58 = HiddenVector_78 = from;
    }
}

// Amedo
public class RectangleWidget : AnimatedWidget
{
    // Amedo
    public RectangleWidget(float anchorX) : base(anchorX)
    {
        HiddenPosition_58 = ShownPosition_60 = Vector2.Zero;
        HiddenVector_78 = ShownVector_80 = Vector2.One;
    }

    // Amedo
    public RectangleWidget()
    {
        HiddenColour_40 = ShownColour_44 = Colour.GetColor_11BF10(15);
        HiddenPosition_58 = ShownPosition_60 = Vector2.Zero;
        HiddenVector_78 = ShownVector_80 = Vector2.One;
    }

    // Amedo
    public override void WhileHidden(int elapsed)
    {
        base.WhileHidden(elapsed);
        if (Effect_20 is not null) Effect_20 = null;
    }
}

// Amedo
public class WidgetController
{
    public const int Slots = 64;
    public ulong Disabled_00;
    public readonly ulong[] Groups_08 = new ulong[Slots];
    public readonly Widget?[] Widgets_208 = new Widget?[Slots];

    // Amedo
    public void SetWidget_259CE8(int slot, Widget? widget) => Widgets_208[slot] = widget;

    // Amedo
    public static int IndexOf_259AF0(ulong mask)
    {
        int found = -1;
        for (int i = 0; i < Slots; i++)
            if ((mask & (1ul << i)) != 0) found = i;
        return found;
    }

    // Amedo
    public void SetEffect_259A90(ulong mask, WidgetEffect? effect)
    {
        int i = IndexOf_259AF0(mask);
        if (i < 0 || Widgets_208[i] is not { } widget) return;
        widget.Effect_20 = effect;
        widget.Next_1C?.SetEffect_259950(effect);
    }

    // Amedo
    public void Show_259B20(ulong mask, int fade, int hold)
    {
        ulong m = mask & ~Disabled_00;
        for (int i = 0; i < Slots; i++)
            if ((m & (1ul << i)) != 0 && Widgets_208[i] is { } w) w.Appear_254EE0(fade, hold);
    }

    // Amedo
    public void Hide_259BC0(ulong mask, int fade, int hold)
    {
        ulong m = mask & ~Disabled_00;
        for (int i = 0; i < Slots; i++)
            if ((m & (1ul << i)) != 0 && Widgets_208[i] is { } w) w.Disappear_255040(fade, hold);
    }

    // Amedo
    public void CopyStatesFrom(WidgetController old, int clockShift)
    {
        for (int i = 0; i < Slots; i++)
        {
            int steps = 0;
            for (Widget? a = Widgets_208[i], b = old.Widgets_208[i]; a is not null && b is not null && a.GetType() == b.GetType() && steps < 256; a = a.Next_1C, b = b.Next_1C, steps++)
                a.CopyStateFrom(b, clockShift);
        }
    }

    // Amedo
    public void BeginFrame_259D58()
    {
        foreach (var w in Widgets_208) w?.BeginFrame_25A000();
    }

    // Amedo
    public void Update_259DB8(IOlegClock clock)
    {
        foreach (var w in Widgets_208) w?.Update_255188(clock);
    }

    // Amedo
    public void Draw_259E28(OverlayQueue overlay)
    {
        foreach (var w in Widgets_208) w?.Draw_25A038(overlay);
    }

    // Amedo
    public void EndFrame_259E98()
    {
        foreach (var w in Widgets_208) w?.EndFrame_25A070();
    }
}
