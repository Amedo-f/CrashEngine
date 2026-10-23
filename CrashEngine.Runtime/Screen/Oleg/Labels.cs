using System.Numerics;

namespace CrashEngine.Runtime.Screen.Oleg;

// Amedo
public class StringLabel : AnimatedWidget
{
    public GameFont? Font_88;
    public uint Flags_8C;
    public string Text_90 = "";
    public string? StyleKey;

    // Amedo
    public StringLabel(GameFont? font, uint flags, float anchorX) : base(anchorX)
    {
        Font_88 = font;
        Flags_8C = flags;
    }

    protected virtual string DrawnText => Text_90;

    // Amedo
    public override void Draw_25A038(OverlayQueue overlay)
    {
        if ((State_00 & Off) == 0 && Phase >= 2)
        {
            var p = Position_48;
            var s = Vector_68;
            if ((State_00 & AspectCorrected) != 0)
            {
                p = OverlayQueue.FitPlaceToScreen_19FFA0(Anchor_04, p);
                s = OverlayQueue.FitSizeToScreen_1A0030(s);
            }
            if (Scale_24 is { } scale) s *= scale.Scale_10;
            overlay.Font_28 = Font_88;
            overlay.Scale_2C = s;
            overlay.Flags_34 = Flags_8C;
            overlay.Colour_10 = Colour_38;
            overlay.TextOwner = this;
            overlay.TextStyle = StyleKey;
            overlay.QueueText_1A03A0(DrawnText, p);
            overlay.TextOwner = null;
        }
        Next_1C?.Draw_25A038(overlay);
    }
}

// Amedo
public sealed class Label : StringLabel
{
    public int TextIndex_90;
    public GameTexts? Texts_309770;

    // Amedo
    public Label(GameFont? font, GameTexts? texts, int textIndex, uint flags, float anchorX) : base(font, flags, anchorX)
    {
        TextIndex_90 = textIndex;
        Texts_309770 = texts;
    }

    protected override string DrawnText => Texts_309770?[TextIndex_90] ?? "";
}

// Amedo
public sealed class TextLine
{
    public GameFont? Font_00;
    public uint Flags_04 = 0x22;
    public string Text_08 = "";
    public uint Colour_14 = Colour.GetColor_11BF10(15);
    public Vector2 Position_18 = new(0.5f, 0.5f), Scale_20 = Vector2.One;
    public int Start_28, Duration_2C;
    public StringLabel? Widget_30;
    public string? StyleKey;

    // Amedo
    public TextLine(GameFont? font) => Font_00 = font;

    // Amedo
    public void Update_2567C0(IOlegClock clock)
    {
        if (Text_08.Length != 0)
        {
            if (Start_28 == 0) Start_28 = clock.Now_04;
            else if (Duration_2C <= clock.Now_04 - Start_28)
            {
                Duration_2C = 0;
                Start_28 = 0;
            }
        }
        if (Widget_30 is { } widget)
        {
            widget.Text_90 = Text_08;
            widget.StyleKey = StyleKey;
        }
    }

    // Amedo
    public void Draw_25ADA0(OverlayQueue overlay)
    {
        if (Text_08.Length == 0 || Widget_30 is not null) return;
        overlay.Colour_10 = Colour_14;
        overlay.Scale_2C = Scale_20;
        overlay.Flags_34 = Flags_04;
        overlay.Font_28 = Font_00;
        overlay.TextOwner = this;
        overlay.TextStyle = StyleKey;
        overlay.QueueText_1A03A0(Text_08, Position_18);
        overlay.TextOwner = null;
    }
}
