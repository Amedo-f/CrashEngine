using CrashEngine.Runtime.Character;

namespace CrashEngine.Runtime.Screen.Oleg;

// Amedo
public sealed class MenuWidget : AnimatedWidget
{
    public const uint AllowStart = 1, RememberPage = 2;

    public uint Flags_88;
    public MenuPage? Home_8C, Start_90;
    public CharacterPad? Pad_94;
    public readonly MenuInput Input_98;
    public readonly MenuDrawer Drawer_9C;
    public readonly MenuSounds Sounds_A0;
    public MenuPage? Current_A4, Drawn_A8;

    // Amedo
    public MenuWidget(MenuInput input, MenuDrawer drawer, MenuSounds sounds, float anchorX) : base(anchorX)
    {
        Input_98 = input;
        Drawer_9C = drawer;
        Sounds_A0 = sounds;
        State_00 = (State_00 & 0xFFFFFF0Fu) | 0x110;
        Fade_10 = 0;
        Start_0C = 0;
        Hold_14 = 0;
        Progress_18 = 0f;
    }

    // Amedo
    public override void BeginFrame_25A000()
    {
        Current_A4?.BeginFrame(0);
        Next_1C?.BeginFrame_25A000();
    }

    // Amedo
    public override void EndFrame_25A070()
    {
        Current_A4?.EndFrame(0);
        Next_1C?.EndFrame_25A070();
    }

    // Amedo
    public override void Update_255188(IOlegClock clock)
    {
        base.Update_255188(clock);
        if ((State_00 & Off) == 0 && Phase >= 2)
        {
            if (Phase == 3 && Pad_94 is { } pad) Input_98.Poll_257CC0(pad, (Flags_88 & AllowStart) != 0);
            else Input_98.Clear_25BB98();
            if (Current_A4 is null)
            {
                Current_A4 = Start_90;
                Current_A4?.Enter_257570(null);
            }
            if (Current_A4?.Step_257778(Input_98, Sounds_A0, 0) is { } next)
            {
                var previous = Current_A4;
                previous.Leave_25BA18(next);
                if ((Flags_88 & RememberPage) != 0 && !ReferenceEquals(Current_A4, MenuPage.Exit_3B6B50)) Start_90 = Current_A4;
                Current_A4 = next;
                next.Enter_257570(previous);
            }
        }
        else if (Current_A4 is { } current)
        {
            current.Leave_25BA18(null);
            if ((Flags_88 & RememberPage) != 0 && !ReferenceEquals(current, MenuPage.Exit_3B6B50)) Start_90 = current;
            Current_A4 = null;
        }
        int phase = Phase;
        if (phase is 2 or 3)
        {
            bool hidden = (Flags_88 & AllowStart) != 0 && ReferenceEquals(Current_A4, MenuPage.Exit_3B6B50);
            if (!hidden) Drawn_A8 = Current_A4;
        }
        else if (phase < 2) Drawn_A8 = null;
    }

    // Amedo
    public override void Draw_25A038(OverlayQueue overlay)
    {
        if ((State_00 & Off) == 0 && Drawn_A8 is { } page)
        {
            var p = Position_48;
            var s = Vector_68;
            if ((State_00 & AspectCorrected) != 0)
            {
                p = OverlayQueue.FitPlaceToScreen_19FFA0(Anchor_04, p);
                s = OverlayQueue.FitSizeToScreen_1A0030(s);
            }
            if (Scale_24 is { } scale) s *= scale.Scale_10;
            Drawer_9C.DrawMenuPage_2581A0(page, 0, Colour_38, p, s, overlay);
        }
        Next_1C?.Draw_25A038(overlay);
    }
}

// Amedo
public class ItemWidget : AnimatedWidget
{
    public MenuPage Page_88;
    public MenuItem Item_8C;

    // Amedo
    public ItemWidget(MenuPage page, MenuItem item, float anchorX) : base(anchorX)
    {
        Page_88 = page;
        Item_8C = item;
    }

    // Amedo
    public bool IsSelected_25A4A0(int col) => ReferenceEquals(Page_88.ItemAgain_25A268(Page_88.Selected_0C[col]), Item_8C);
}
