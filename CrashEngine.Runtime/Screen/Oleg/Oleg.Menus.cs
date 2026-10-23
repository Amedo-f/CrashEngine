using System.Numerics;
using CrashEngine.Runtime.Character;
using CrashEngine.Runtime.Game;

namespace CrashEngine.Runtime.Screen.Oleg;

// Amedo
public static class OlegGlobals
{
    public static readonly uint ShadowColour_30A298 = Colour.Set_101218(0f, 0f, 0f, 0.5f);
    public static readonly Vector2 ShadowOffset_30A290 = new(0.01f, 0.01f);
    public static readonly Vector2 MenuCentre_30A2A8 = new(0.5f, BitConverter.Int32BitsToSingle(0x3ED3F7CF)), PanelCentre_30A2B0 = new(0.5f, BitConverter.Int32BitsToSingle(0x3F439581));
    public static readonly Vector2 Zero_30A2B8 = Vector2.Zero, Wide_30A2C0 = new(0.7f, 0.7f), Top_30A2C8 = new(0.5f, 0.15f), Big_30A2D0 = new(0.75f, 0.75f);
    public static readonly Vector2 Middle_30A2D8 = new(0.5f, 0.4f), Text_30A2E0 = new(0.625f, 0.625f);
    public static readonly uint Clear_30A380 = Colour.Set_101218(1f, 1f, 1f, 0f), White_30A388 = Colour.Set_101218(1f, 1f, 1f, 1f);
    public static readonly uint White_30A3C8 = Colour.Set_101218(1f, 1f, 1f, 1f), Clear_30A3D0 = Colour.Set_101218(1f, 1f, 1f, 0f);
    public static readonly uint Blue_30A3D8 = Colour.Set_101218(0.1f, 0.588f, 1f, 1f), BlueClear_30A3E0 = Colour.Set_101218(0.1f, 0.588f, 1f, 0f);
    public static readonly uint Mint_30A3E8 = Colour.Set_101218(0.325f, 1f, 0.9f, 1f), White_30A3F0 = Colour.Set_101218(1f, 1f, 1f, 1f), Clear_30A3F8 = Colour.Set_101218(1f, 1f, 1f, 0f);
    public static readonly int Angle15_30A390 = WidgetEffect.AngleFrom_11BFA0(15f, 1), Angle21_30A398 = WidgetEffect.AngleFrom_11BFA0(21f, 1);
    public static readonly int Angle25_30A3A0 = WidgetEffect.AngleFrom_11BFA0(25f, 1), AngleM35_30A3A8 = WidgetEffect.AngleFrom_11BFA0(-35f, 1);
    public static readonly int Angle120_30A3B0 = WidgetEffect.AngleFrom_11BFA0(120f, 1), AngleM36_30A3B8 = WidgetEffect.AngleFrom_11BFA0(-36f, 1);
    public static readonly int Angle122_30A3C0 = WidgetEffect.AngleFrom_11BFA0(122f, 1);
    public static readonly Vector2Curve CurveA_30A400 = new(new Vector2[8]), CurveB_30A408 = new(new Vector2[8]);
    public static readonly ColourCurve ColourC_30A410 = new(new Vector4[16]), ColourD_30A418 = new(new Vector4[8]);
}

// Amedo
public sealed partial class Oleg
{
    public const int Drawers = 9;

    public readonly ButtonBindings Bindings_3A4 = new(0, 7);
    public readonly MenuInput Input_3B0;
    public readonly MenuDrawer[] Drawers_3BC = new MenuDrawer[Drawers];
    public readonly MenuSounds Sounds_6F8 = new();

    public SpriteWidget Dimmer_15CC = null!, Logo_1DC0 = null!, Sprite_2660 = null!, Sprite_367C = null!, Flash_46E4 = null!, Loading_482C = null!;
    public RingWidget TitleRings_1688 = null!, TitlePanel_171C = null!, OptionsPanel_1B40 = null!;
    public RingWidget PauseRing_2908 = null!, PauseRing_299C = null!, PauseRing_2A30 = null!, PauseRing_2AC4 = null!, PauseRing_2B58 = null!;
    public RingWidget LevelPanel_34A8 = null!, Ring_35E8 = null!, Ring_3AF8 = null!;
    public Label Title_17B0 = null!, Title_1844 = null!, Title_18D8 = null!, Title_196C = null!, Title_1A00 = null!;
    public Label Label_1C80 = null!, Label_1FD4 = null!, Label_2114 = null!, Label_21A8 = null!, Label_223C = null!, Label_22D0 = null!;
    public Label Label_2364 = null!, Label_23F8 = null!, Label_2538 = null!, Label_25CC = null!, Label_27C8 = null!;
    public Label LevelTitle_3414 = null!, Label_3738 = null!, Label_48E8 = null!;
    public MenuWidget Options_1A94 = null!, Menu_1BD4 = null!, MainMenu_1D14 = null!, Menu_1E7C = null!, Menu_1F28 = null!, Menu_2068 = null!;
    public MenuWidget Menu_248C = null!, Menu_271C = null!, Menu_285C = null!, Menu_353C = null!, Menu_37CC = null!, Menu_39B0 = null!, Menu_3B8C = null!;
    public readonly MenuWidget[] Levels_3164 = new MenuWidget[4];
    public StringLabel String_3878 = null!, String_3914 = null!, String_3A5C = null!;
    public TiledPicture Picture_47A0 = null!;

    // Amedo
    private void ConstructMenus_16DB70(GameFont? font, GameTexts? texts)
    {
        for (int i = 0; i < Drawers; i++) Drawers_3BC[i] = new MenuDrawer(font);
        Dimmer_15CC = new SpriteWidget(Sprites_B4C[0]);
        TitleRings_1688 = new RingWidget(8, 1, 0x40, null, 0.5f);
        TitlePanel_171C = new RingWidget(4, 1, 0x20, null, 0.5f);
        Title_17B0 = new Label(font, texts, 0x1D, 0x44, 0.95f);
        Title_1844 = new Label(font, texts, 0xBB, 0x44, 0.95f);
        Title_18D8 = new Label(font, texts, 0x1C, 0x14, 0.05f);
        Title_196C = new Label(font, texts, 0x1E, 0x14, 0.05f);
        Title_1A00 = new Label(font, texts, 0x1F, 0x44, 0.95f);
        Options_1A94 = Menu(6);
        OptionsPanel_1B40 = new RingWidget(4, 1, 0x20, null, 0.5f);
        Menu_1BD4 = Menu(0);
        Label_1C80 = new Label(font, texts, 0x26, 0x22, 0.5f);
        MainMenu_1D14 = Menu(2);
        Logo_1DC0 = new SpriteWidget(Sprites_B4C[1], 0.5f);
        Menu_1E7C = Menu(4);
        Menu_1F28 = Menu(2);
        Label_1FD4 = new Label(font, texts, 0x16, 0x22, 0.5f);
        Menu_2068 = Menu(2);
        Label_2114 = new Label(font, texts, 0x45, 0x22, 0.5f);
        Label_21A8 = new Label(font, texts, 0x20, 0x22, 0.5f);
        Label_223C = new Label(font, texts, 0x21, 0x22, 0.5f);
        Label_22D0 = new Label(font, texts, 0x22, 0x22, 0.5f);
        Label_2364 = new Label(font, texts, 0x24, 0x22, 0.5f);
        Label_23F8 = new Label(font, texts, 0x25, 0x22, 0.5f);
        Menu_248C = Menu(2);
        Label_2538 = new Label(font, texts, 0x23, 0x22, 0.5f);
        Label_25CC = new Label(font, texts, 0x42, 0x22, 0.5f);
        Sprite_2660 = new SpriteWidget(Sprites_B4C[23], 0.5f);
        Menu_271C = Menu(2);
        Label_27C8 = new Label(font, texts, 0xB3, 0x22, 0.5f);
        Menu_285C = Menu(2);
        PauseRing_2908 = new RingWidget(4, 1, 0x20, null, 0.5f);
        PauseRing_299C = new RingWidget(4, 1, 0x20, null, 0.5f);
        PauseRing_2A30 = new RingWidget(4, 1, 0x20, null, 0.5f);
        PauseRing_2AC4 = new RingWidget(2, 1, 0x20, null, 0.5f);
        PauseRing_2B58 = new RingWidget(4, 1, 0x18, null, 0.5f);
        for (int i = 0; i < Levels_3164.Length; i++) Levels_3164[i] = Menu(5);
        LevelTitle_3414 = new Label(font, texts, 0x47, 0x22, 0.5f);
        LevelPanel_34A8 = new RingWidget(4, 1, 0x20, null, 0.5f);
        Menu_353C = Menu(1);
        Ring_35E8 = new RingWidget(4, 1, 0x20, null, 0.5f);
        Sprite_367C = new SpriteWidget(Sprites_B4C[1], 0.05f);
        Label_3738 = new Label(font, texts, 0xB2, 0x14, 0.05f);
        Menu_37CC = Menu(3);
        String_3878 = new StringLabel(font, 0x22, 0.5f);
        String_3914 = new StringLabel(font, 0x22, 0.5f);
        Menu_39B0 = Menu(7);
        String_3A5C = new StringLabel(font, 0x22, 0.5f);
        Ring_3AF8 = new RingWidget(4, 1, 0x20, null, 0.5f);
        Menu_3B8C = Menu(8);
        Flash_46E4 = new SpriteWidget(Sprites_B4C[0]);
        Picture_47A0 = new TiledPicture(Pictures_110C, 0.5f);
        Loading_482C = new SpriteWidget(Sprites_B4C[1], 0.05f);
        Label_48E8 = new Label(font, texts, 0x57, 0x22, 0.5f);
    }

    private MenuWidget Menu(int drawer) => new(Input_3B0, Drawers_3BC[drawer], Sounds_6F8, 0.5f);

    // Amedo
    private void SetMenuWidgets_16F1FC()
    {
        SetWidget_259CE8(4, Dimmer_15CC);
        SetWidget_259CE8(5, TitleRings_1688);
        SetWidget_259CE8(6, TitlePanel_171C);
        SetWidget_259CE8(7, Title_17B0);
        SetWidget_259CE8(8, Title_1844);
        SetWidget_259CE8(9, Title_18D8);
        SetWidget_259CE8(0xA, Title_196C);
        SetWidget_259CE8(0xB, Title_1A00);
        SetWidget_259CE8(0xC, Options_1A94);
        SetWidget_259CE8(0xE, MainMenu_1D14);
        SetWidget_259CE8(0xF, Logo_1DC0);
        SetWidget_259CE8(0x10, Menu_1E7C);
        SetWidget_259CE8(0x11, Menu_1F28);
        SetWidget_259CE8(0x12, Menu_2068);
        SetWidget_259CE8(0x13, PauseRing_2AC4);
        SetWidget_259CE8(0x14, Label_21A8);
        SetWidget_259CE8(0x15, Label_223C);
        SetWidget_259CE8(0x16, Label_2364);
        SetWidget_259CE8(0x17, Label_2538);
        SetWidget_259CE8(0x18, Label_27C8);
        SetWidget_259CE8(0xD, Menu_1BD4);
        for (int i = 0; i < Levels_3164.Length; i++) SetWidget_259CE8(0x19 + i, Levels_3164[i]);
        SetWidget_259CE8(0x1D, LevelTitle_3414);
        SetWidget_259CE8(0x1E, Menu_353C);
        SetWidget_259CE8(0x1F, Sprite_367C);
        SetWidget_259CE8(0x20, Menu_37CC);
        SetWidget_259CE8(0x21, String_3878);
        SetWidget_259CE8(0x22, String_3914);
        SetWidget_259CE8(0x23, String_3A5C);
    }

    public PausePage? Pause_770;
    public MainMenuPage? MainMenu_768;
    public DisableAutosavePage? DisableAutosave_774;
    public QuitPage? Quit_778;
    public ScreenPositionPage? ScreenPosition_760;
    public ExtrasPage? Extras_764;
    public GameOverPage? GameOver_76C;
    public NoticePage? Notice_77C, Notice_780, Notice_784;
    public SaveChoicesPage? SaveChoices_79C;
    public SaveSlotsPage? SaveSlots_7A0;
    public OptionsPage? Options_798;
    public readonly LevelsPage?[] Levels_788 = new LevelsPage?[4];

    // Amedo
    public void Initialise_1700D8(CharacterPad? pad, GameFont? pageFont, GameFont? font, IReadOnlyList<object?>? frontendSounds, SaveManager? saveManager)
    {
        var texts = CodeTexts_309770;
        ScreenPosition_760 = new ScreenPositionPage(this, null, texts);
        Extras_764 = new ExtrasPage(this, texts);
        GameOver_76C = new GameOverPage(this, texts);
        MainMenu_768 = new MainMenuPage(this, texts);
        Pause_770 = new PausePage(this, texts);
        DisableAutosave_774 = new DisableAutosavePage(this, null, texts);
        Quit_778 = new QuitPage(this, null, texts);
        Notice_77C = new NoticePage(this, null, texts);
        Notice_780 = new NoticePage(this, null, texts);
        Notice_784 = new NoticePage(this, null, texts);
        for (int i = 0; i < Levels_788.Length; i++) Levels_788[i] = new LevelsPage(this, i, Levels_3164[i], texts);
        Options_798 = new OptionsPage(this, null, texts);
        if (saveManager is not null)
        {
            SaveChoices_79C = new SaveChoicesPage(saveManager);
            SaveSlots_7A0 = new SaveSlotsPage(this, saveManager, Menu_3B8C);
        }
        Menu_1BD4.Home_8C = Menu_1BD4.Start_90 = ScreenPosition_760;
        Menu_353C.Home_8C = Menu_353C.Start_90 = Extras_764;
        Menu_37CC.Home_8C = Menu_37CC.Start_90 = GameOver_76C;
        MainMenu_1D14.Home_8C = MainMenu_1D14.Start_90 = MainMenu_768;
        Menu_1E7C.Home_8C = Menu_1E7C.Start_90 = Pause_770;
        Menu_1F28.Home_8C = Menu_1F28.Start_90 = DisableAutosave_774;
        Menu_2068.Home_8C = Menu_2068.Start_90 = Quit_778;
        Menu_248C.Home_8C = Menu_248C.Start_90 = Notice_77C;
        Menu_271C.Home_8C = Menu_271C.Start_90 = Notice_780;
        Menu_285C.Home_8C = Menu_285C.Start_90 = Notice_784;
        for (int i = 0; i < Levels_788.Length; i++) Levels_3164[i].Home_8C = Levels_3164[i].Start_90 = Levels_788[i];
        Options_1A94.Home_8C = Options_1A94.Start_90 = Options_798;
        Menu_39B0.Home_8C = Menu_39B0.Start_90 = SaveChoices_79C;
        Menu_3B8C.Home_8C = Menu_3B8C.Start_90 = SaveSlots_7A0;
        Menu_1BD4.Flags_88 |= MenuWidget.AllowStart;
        Menu_353C.Flags_88 |= MenuWidget.AllowStart;
        MainMenu_1D14.Flags_88 |= MenuWidget.AllowStart;
        Menu_37CC.Flags_88 &= ~MenuWidget.AllowStart;
        Menu_1E7C.Flags_88 |= MenuWidget.AllowStart;
        Menu_1F28.Flags_88 |= MenuWidget.AllowStart;
        Menu_2068.Flags_88 |= MenuWidget.AllowStart;
        Menu_248C.Flags_88 |= MenuWidget.AllowStart;
        Menu_271C.Flags_88 |= MenuWidget.AllowStart;
        Menu_285C.Flags_88 |= MenuWidget.AllowStart;
        foreach (var menu in Levels_3164) menu.Flags_88 |= MenuWidget.AllowStart;
        Options_1A94.Flags_88 |= MenuWidget.AllowStart;
        Menu_39B0.Flags_88 &= ~MenuWidget.AllowStart;
        Menu_3B8C.Flags_88 &= ~MenuWidget.AllowStart;
        Menu_1BD4.Pad_94 = Menu_353C.Pad_94 = MainMenu_1D14.Pad_94 = Menu_37CC.Pad_94 = Menu_1E7C.Pad_94 = Menu_1F28.Pad_94 = pad;
        Menu_2068.Pad_94 = Menu_248C.Pad_94 = Menu_271C.Pad_94 = Menu_285C.Pad_94 = pad;
        foreach (var menu in Levels_3164) menu.Pad_94 = pad;
        Options_1A94.Pad_94 = Menu_39B0.Pad_94 = Menu_3B8C.Pad_94 = pad;
        Menu_353C.Flags_88 |= MenuWidget.RememberPage;
        if (saveManager is not null)
        {
            saveManager.MessageLabel_54 = String_3878;
            saveManager.SlotsLabel_5C = String_3A5C;
            saveManager.Slots_64 = SaveSlots_7A0;
            saveManager.Slots_2C = SaveSlotsGroup;
            saveManager.Choices_60 = SaveChoices_79C;
            saveManager.QuestionLabel_58 = String_3914;
            saveManager.Message_24 = SaveMessageGroup;
            saveManager.Choices_28 = SaveChoicesGroup;
            saveManager.AllButOverlays_20 = AllButOverlaysGroup;
        }

        Bindings_3A4.SetDigitalBind_2B3B00(0, 5);
        Bindings_3A4.SetDigitalBind_2B3B00(1, 4);
        Bindings_3A4.SetDigitalBind_2B3B00(2, 0x16);
        Bindings_3A4.SetDigitalBind_2B3B00(3, 0);
        Bindings_3A4.SetDigitalBind_2B3B00(3, 8);
        Bindings_3A4.SetDigitalBind_2B3B00(4, 1);
        Bindings_3A4.SetDigitalBind_2B3B00(4, 9);
        Bindings_3A4.SetDigitalBind_2B3B00(5, 2);
        Bindings_3A4.SetDigitalBind_2B3B00(5, 0xA);
        Bindings_3A4.SetDigitalBind_2B3B00(6, 3);
        Bindings_3A4.SetDigitalBind_2B3B00(6, 0xB);

        var scale = CyclingScale.Global_30BB90;
        var d = Drawers_3BC[0];
        d.TitlePosition_04 = Vector2.Zero;
        d.TitleScale_0C = Vector2.Zero;
        d.ItemFlags_03 = 0x22;
        d.ItemsPosition_14 = OlegGlobals.Zero_30A2B8;
        d.ManyScale_28 = OlegGlobals.Wide_30A2C0;
        d.ManySpacing_30 = 0.05f;
        SetDrawerFonts(d, font);
        d.Scale_58 = scale;
        d = Drawers_3BC[1];
        d.TitleScale_0C = new(0.75f, 0.75f);
        d.TitlePosition_04 = Vector2.Zero;
        d.TitleFlags_02 = 0x22;
        d.ItemFlags_03 = 0x22;
        d.FewItems_00 = 9;
        d.ItemsPosition_14 = new(0f, 0.364f);
        d.FewScale_1C = new(0.75f, 0.75f);
        d.ManyItems_01 = 0x11;
        d.FewSpacing_24 = 0.05f;
        d.ManyScale_28 = new(0.5f, 0.5f);
        d.ManySpacing_30 = 0.035f;
        SetDrawerFonts(d, font);
        d.Scale_58 = scale;
        d = Drawers_3BC[2];
        d.TitlePosition_04 = Vector2.Zero;
        d.TitleScale_0C = Vector2.Zero;
        d.ItemFlags_03 = 0x22;
        d.ItemsPosition_14 = OlegGlobals.Zero_30A2B8;
        d.ManyItems_01 = 7;
        d.ManyScale_28 = OlegGlobals.Wide_30A2C0;
        d.ManySpacing_30 = 0.05f;
        SetDrawerFonts(d, font);
        d.Scale_58 = scale;
        d = Drawers_3BC[3];
        d.TitleScale_0C = new(1.5f, 1.5f);
        d.TitlePosition_04 = Vector2.Zero;
        d.TitleFlags_02 = 3;
        d.ItemFlags_03 = 3;
        d.ItemsPosition_14 = new(0f, 0.15f);
        d.ManyScale_28 = new(0.75f, 0.75f);
        d.ManySpacing_30 = 0.05f;
        SetDrawerFonts(d, font);
        d.Scale_58 = scale;
        d = Drawers_3BC[6];
        d.TitleScale_0C = new(0.75f, 0.75f);
        d.TitlePosition_04 = Vector2.Zero;
        d.TitleFlags_02 = 0x22;
        d.ItemFlags_03 = 0x22;
        d.ItemsPosition_14 = new(0f, 0.314f);
        d.ManyScale_28 = new(0.625f, 0.75f);
        d.ManySpacing_30 = 0.05f;
        SetDrawerFonts(d, font);
        d.Scale_58 = scale;
        PcGraphicsOptions.ConfigureDrawer(d); // Amedo
        d = Drawers_3BC[4];
        d.TitlePosition_04 = Vector2.Zero;
        d.TitleScale_0C = Vector2.Zero;
        d.ItemFlags_03 = 0x22;
        d.ItemsPosition_14 = OlegGlobals.Zero_30A2B8;
        d.ManyItems_01 = 7;
        d.ManyScale_28 = OlegGlobals.Wide_30A2C0;
        d.ManySpacing_30 = 0.05f;
        SetDrawerFonts(d, font);
        d.Scale_58 = scale;
        d = Drawers_3BC[5];
        d.TitlePosition_04 = Vector2.Zero;
        d.TitleScale_0C = Vector2.Zero;
        d.ItemFlags_03 = 3;
        d.TitleFlags_02 = 3;
        d.ManyScale_28 = Vector2.One;
        d.ItemsPosition_14 = Vector2.Zero;
        d.ManySpacing_30 = 0.15f;
        SetDrawerFonts(d, font);
        for (int i = 7; i < Drawers; i++)
        {
            d = Drawers_3BC[i];
            d.TitlePosition_04 = Vector2.Zero;
            d.TitleScale_0C = Vector2.Zero;
            d.ItemFlags_03 = 0x22;
            d.ItemsPosition_14 = OlegGlobals.Zero_30A2B8;
            d.ManyItems_01 = 7;
            d.ManyScale_28 = OlegGlobals.Wide_30A2C0;
            d.ManySpacing_30 = 0.05f;
            SetDrawerFonts(d, font);
            d.Scale_58 = scale;
        }

        if (frontendSounds is null || frontendSounds.Count < 3) return;
        Sounds_6F8.Sounds_00[1] = frontendSounds[1];
        Sounds_6F8.Sounds_00[3] = frontendSounds[1];
        Sounds_6F8.Sounds_00[5] = frontendSounds[1];
        Sounds_6F8.Sounds_00[7] = frontendSounds[1];
        Sounds_6F8.Sounds_00[9] = frontendSounds[1];
        Sounds_6F8.Sounds_00[10] = frontendSounds[0];
        Sounds_6F8.Sounds_00[11] = frontendSounds[2];
        Sounds_6F8.Sounds_00[12] = frontendSounds[2];
    }

    private static void SetDrawerFonts(MenuDrawer d, GameFont? font)
    {
        d.Fonts_34[1] = font;
        d.Colours_44[1] = Colour.GetColor_11BF10(15);
        d.Fonts_34[2] = font;
        d.Colours_44[2] = Colour.GetColor_11BF10(0x11);
        d.Fonts_34[3] = font;
        d.Colours_44[3] = Colour.GetColor_11BF10(0x15);
    }

    // Amedo
    internal void AddPanelRings_16A288(Vector2 size, RingWidget widget)
    {
        var r0 = widget.AddRing_25AAA0(0, null);
        var r1 = widget.AddRing_25AAA0(1, null);
        var r2 = widget.AddRing_25AAA0(2, null);
        var r3 = widget.AddRing_25AAA0(3, null);
        r0.OuterScale_2C = size;
        r0.OuterColours_38 = OlegGlobals.ColourD_30A418;
        r0.InnerColour_1C = OlegGlobals.Blue_30A3D8;
        float a = BitConverter.Int32BitsToSingle(0x3B449BA6), b = BitConverter.Int32BitsToSingle(0x3C23D70A), c = BitConverter.Int32BitsToSingle(0x3C54FDF4);
        r1.OuterScale_2C = new Vector2(size.X + a, size.Y + a);
        r1.OuterColour_20 = OlegGlobals.White_30A3C8;
        r2.OuterScale_2C = new Vector2(size.X + b, size.Y + b);
        r2.OuterColour_20 = OlegGlobals.White_30A3C8;
        r3.OuterScale_2C = new Vector2(size.X + c, size.Y + c);
        r3.OuterColour_20 = OlegGlobals.Clear_30A3D0;
        widget.ShadowColour_28 = OlegGlobals.ShadowColour_30A298;
        widget.ShadowOffset_2C = OlegGlobals.ShadowOffset_30A290;
    }

    // Amedo
    private void SetUpExtras_16A828()
    {
        Menu_353C.FadeIn_25A760(Colour.GetColor_11BF10(15), new(0.5f, 0.1f), Vector2.One);
        Ring_35E8.FadeIn_25A760(Colour.GetColor_11BF10(15), new(0.5f, 0.1f), Vector2.One);
        Sprite_367C.FadeRectangle_256A70(Colour.GetColor_11BF10(15), new(0.17f, 0.13f), new(0.24f, 0.16f));
        Label_3738.FadeIn_25A760(Colour.GetColor_11BF10(15), new(0.05f, 0.95f), new(0.625f, 0.625f));
        AddPanelRings_16A288(new Vector2(0.35f, 0.05f), Ring_35E8);
        var logo = Sprite_367C;
        logo.PulseAmount_98 = 0.1f;
        logo.RockPeriod_B0 = 7f;
        logo.RockOffset_B4 = BitConverter.Int32BitsToSingle(0x3FC90FDB);
        logo.RockAmount_B8 = BitConverter.Int32BitsToSingle(0x3E20D97C);
        logo.PulsePeriod_94 = 3f;
        logo.State_00 = (logo.State_00 & 0xFFFF87FFu) | 0x2800;
        Menu_353C.Next_1C = Ring_35E8;
        logo.Next_1C = Label_3738;
    }

    // Amedo
    private void SetUpMainMenu_16AA50()
    {
        var logo = Logo_1DC0;
        logo.RockPeriod_B0 = 7f;
        logo.RockOffset_B4 = BitConverter.Int32BitsToSingle(0x3FC90FDB);
        logo.RockAmount_B8 = BitConverter.Int32BitsToSingle(0x3E20D97C);
        logo.PulsePeriod_94 = 3f;
        logo.PulseAmount_98 = 0.1f;
        logo.State_00 = (logo.State_00 & 0xFFFF87FFu) | 0x2800;
        logo.ShadowColour_28 = OlegGlobals.ShadowColour_30A298;
        logo.ShadowOffset_2C = OlegGlobals.ShadowOffset_30A290;
        uint white = Colour.GetColor_11BF10(15);
        MainMenu_1D14.GrowIn_25A890(white, OlegGlobals.PanelCentre_30A2B0, Vector2.One, OlegGlobals.PanelCentre_30A2B0);
        logo.GrowRectangle_256D20(Colour.GetColor_11BF10(15), new(0.5f, 0.35f), new(0.7f, 0.45f), OlegGlobals.MenuCentre_30A2A8);
    }

    // Amedo
    private void SetUpLoadingScreen_16ABA0()
    {
        var logo = Loading_482C;
        logo.PulsePeriod_94 = 3f;
        logo.PulseAmount_98 = 0.1f;
        logo.RockPeriod_B0 = 7f;
        logo.RockOffset_B4 = BitConverter.Int32BitsToSingle(0x3FC90FDB);
        logo.RockAmount_B8 = BitConverter.Int32BitsToSingle(0x3E20D97C);
        logo.State_00 = (logo.State_00 & 0xFFFF87FFu) | 0x2800;
        Label_48E8.Scale_24 = CyclingScale.Global_30BB90;
        Picture_47A0.ShownColour_44 = OlegGlobals.White_30A388;
        Picture_47A0.HiddenColour_40 = OlegGlobals.Clear_30A380;
        Picture_47A0.State_00 = (Picture_47A0.State_00 & 0xFFFF87FFu) | 0x2000;
        Flash_46E4.HiddenColour_40 = Colour.GetColor_11BF10(0);
        Flash_46E4.ShownColour_44 = Colour.GetColor_11BF10(8);
        Flash_46E4.State_00 = (Flash_46E4.State_00 & 0xFFFF87FFu) | 0x800;
        logo.FadeRectangle_256A70(Colour.GetColor_11BF10(15), new(0.2f, 0.15f), new(0.3f, 0.2f));
        Label_48E8.FadeIn_25A760(Colour.GetColor_11BF10(15), new(0.5f, 0.9f), new(0.75f, 0.75f));
        logo.Next_1C = Label_48E8;
    }

    // Amedo
    private void SetUpMenuRings_16AD68()
    {
        var rings = new Ring[8];
        for (int i = 0; i < rings.Length; i++) rings[i] = TitleRings_1688.AddRing_25AAA0(i, null);
        var scale = CyclingScale.Global_30BB90;
        for (int i = 0; i < 30; i++)
        {
            int angle = WidgetEffect.AngleFrom_11BFA0(i * BitConverter.Int32BitsToSingle(0x3DD67751));
            float s = CrashEngine.Core.GameMath.SinCosVu0_2E4C30(angle * BitConverter.Int32BitsToSingle(0x38C90FDB)).Sin * 0.1f + 1f;
            scale.Samples_0C[i] = s;
            if (i == 0) scale.Value_10 = s;
        }
        int duration = CrashEngine.Runtime.Character.CharacterController.Cvt(Widget.TicksPerSecond * 0.5f);
        if ((scale.Word_00 & 0x8000) == 0)
        {
            scale.Word_00 &= 0x0000FFFFu;
            scale.Duration_08 = duration;
            scale.Start_04 = 0;
            scale.Word_00 |= 0x8000;
        }
        var b = OlegGlobals.CurveB_30A408.Keys_04;
        b[0] = new(0.96f, 0.96f); b[1] = new(0.97f, 0.97f); b[2] = new(0.98f, 0.98f); b[3] = new(0.98f, 0.98f);
        b[4] = new(0.98f, 0.98f); b[5] = new(0.99f, 0.99f); b[6] = new(1f, 1f); b[7] = new(1.02f, 1.02f);
        var a = OlegGlobals.CurveA_30A400.Keys_04;
        a[0] = new(1.03f, 1.03f); a[1] = new(1.02f, 1.02f); a[2] = new(1.02f, 1.02f); a[3] = new(1.02f, 1.02f);
        a[4] = new(1.01f, 1.01f); a[5] = new(1.005f, 1.005f); a[6] = new(0.988f, 0.988f); a[7] = new(0.97f, 0.97f);
        var blue = new Vector4(0.1f, 0.588f, 1f, 1f);
        var c = OlegGlobals.ColourC_30A410.Keys_04;
        for (int i = 0; i < c.Length; i++) c[i] = blue;
        c[5] = new(0.3f, 0.788f, 1f, 1f);
        c[6] = new(0.55f, 0.925f, 1f, 1f);
        var d = OlegGlobals.ColourD_30A418.Keys_04;
        d[0] = blue; d[1] = new(0.1f, 0.6f, 1f, 1f); d[2] = new(0.4f, 0.8f, 1f, 1f); d[3] = new(0.55f, 0.925f, 1f, 1f);
        d[4] = new(0.1f, 0.68f, 1f, 1f); d[5] = new(0.1f, 0.4f, 1f, 1f); d[6] = new(0.1f, 0.35f, 1f, 1f); d[7] = blue;
        Title_17B0.FadeIn_25A760(Colour.GetColor_11BF10(15), new(0.95f, 0.95f), new(0.625f, 0.625f));
        Title_1844.FadeIn_25A760(Colour.GetColor_11BF10(15), new(0.95f, 0.95f), new(0.625f, 0.625f));
        Title_18D8.FadeIn_25A760(Colour.GetColor_11BF10(15), new(0.05f, 0.95f), new(0.625f, 0.625f));
        Title_196C.FadeIn_25A760(Colour.GetColor_11BF10(15), new(0.05f, 0.9f), new(0.75f, 0.75f));
        Title_1A00.FadeIn_25A760(Colour.GetColor_11BF10(15), new(0.95f, 0.9f), new(0.75f, 0.75f));
        TitleRings_1688.GrowIn_25A890(Colour.GetColor_11BF10(15), OlegGlobals.MenuCentre_30A2A8, Vector2.One, OlegGlobals.MenuCentre_30A2A8);
        TitlePanel_171C.GrowIn_25A890(Colour.GetColor_11BF10(15), OlegGlobals.PanelCentre_30A2B0, Vector2.One, OlegGlobals.PanelCentre_30A2B0);
        int[] angles = [OlegGlobals.Angle15_30A390, OlegGlobals.Angle15_30A390, OlegGlobals.Angle25_30A3A0, OlegGlobals.Angle25_30A3A0,
                        OlegGlobals.Angle15_30A390, OlegGlobals.Angle15_30A390, OlegGlobals.Angle15_30A390, OlegGlobals.Angle15_30A390];
        for (int i = 0; i < rings.Length; i++) rings[i].Rotation_44 = angles[i];
        rings[0].InnerColour_1C = OlegGlobals.Blue_30A3D8;
        rings[0].OuterColour_20 = OlegGlobals.Blue_30A3D8;
        rings[1].OuterColour_20 = OlegGlobals.White_30A3C8;
        rings[2].OuterColour_20 = OlegGlobals.White_30A3C8;
        rings[3].OuterColour_20 = OlegGlobals.Blue_30A3D8;
        rings[4].OuterColours_38 = OlegGlobals.ColourC_30A410;
        rings[5].OuterColour_20 = OlegGlobals.White_30A3C8;
        rings[6].OuterColour_20 = OlegGlobals.White_30A3C8;
        rings[7].OuterColour_20 = OlegGlobals.Clear_30A3D0;
        rings[0].OuterScale_2C = new(0.17f, 0.15f);
        rings[1].OuterScale_2C = new(0.173f, 0.153f);
        rings[2].OuterScale_2C = new(0.3f, 0.25f);
        rings[3].OuterScale_2C = new(0.303f, 0.253f);
        rings[4].OuterScale_2C = new(0.375f, 0.35f);
        rings[5].OuterScale_2C = new(0.378f, 0.353f);
        rings[6].OuterScale_2C = new(0.385f, 0.36f);
        rings[7].OuterScale_2C = new(0.388f, 0.363f);
        TitleRings_1688.State_00 = (TitleRings_1688.State_00 & 0xFFFF87FFu) | 0x1000;
        AddPanelRings_16A288(new Vector2(0.12f, 0.16f), TitlePanel_171C);
        Dimmer_15CC.HiddenColour_40 = Colour.Set_101218(0f, 0f, 0f, 0f);
        Dimmer_15CC.ShownColour_44 = Colour.Set_101218(0f, 0f, 0f, 0.75f);
        Dimmer_15CC.State_00 = (Dimmer_15CC.State_00 & 0xFFFF87FFu) | 0x800;
    }

    // Amedo
    private void SetUpLevelsMenus_16BA28()
    {
        foreach (var menu in Levels_3164) menu.FadeIn_25A760(Colour.GetColor_11BF10(15), new(0.5f, 0.05f), Vector2.One);
        LevelTitle_3414.Scale_24 = CyclingScale.Global_30BB90;
        LevelTitle_3414.SlideIn_25A7C8(Colour.GetColor_11BF10(15), new(0.5f, 0.75f), new(0.625f, 0.625f), new(0f, 0.5f));
        LevelPanel_34A8.SlideIn_25A7C8(Colour.GetColor_11BF10(15), new(0.5f, 0.75f), Vector2.One, new(0f, 0.5f));
        AddPanelRings_16A288(new Vector2(0.3f, 0.05f), LevelPanel_34A8);
        LevelTitle_3414.Next_1C = LevelPanel_34A8;
    }

    // Amedo
    private void SetUpOptionsMenu_16BC48()
    {
        Options_1A94.FadeIn_25A760(Colour.GetColor_11BF10(15), new(0.5f, 0.1f), Vector2.One);
        OptionsPanel_1B40.FadeIn_25A760(Colour.GetColor_11BF10(15), new(0.5f, 0.1f), Vector2.One);
        AddPanelRings_16A288(new Vector2(0.35f, 0.05f), OptionsPanel_1B40);
        Options_1A94.Next_1C = OptionsPanel_1B40;
    }

    // Amedo
    private void SetUpPause_16BD48()
    {
        var panel = OlegGlobals.PanelCentre_30A2B0;
        var centre = OlegGlobals.MenuCentre_30A2A8;
        var one = Vector2.One;
        Menu_1E7C.GrowIn_25A890(Colour.GetColor_11BF10(15), panel, one, panel);
        Menu_1F28.GrowIn_25A890(Colour.GetColor_11BF10(15), panel, one, panel);
        Label_1FD4.GrowIn_25A890(Colour.GetColor_11BF10(15), centre, one, centre);
        Menu_2068.GrowIn_25A890(Colour.GetColor_11BF10(15), panel, one, panel);
        Label_223C.GrowIn_25A890(Colour.GetColor_11BF10(15), new(0.5f, 0.25f), one, new(0.5f, 0.3f));
        Label_2114.GrowIn_25A890(Colour.GetColor_11BF10(15), centre, one, centre);
        Label_21A8.GrowIn_25A890(Colour.GetColor_11BF10(15), centre, new(0.75f, 0.75f), centre);
        Label_22D0.GrowIn_25A890(Colour.GetColor_11BF10(15), centre, new(0.8f, 0.8f), centre);
        Label_2364.GrowIn_25A890(Colour.GetColor_11BF10(15), new(0.5f, 0.25f), new(0.85f, 0.85f), centre);
        Label_23F8.GrowIn_25A890(Colour.GetColor_11BF10(15), OlegGlobals.Middle_30A2D8, OlegGlobals.Text_30A2E0, centre);
        Menu_248C.GrowIn_25A890(Colour.GetColor_11BF10(15), panel, one, panel);
        Label_2538.GrowIn_25A890(Colour.GetColor_11BF10(15), OlegGlobals.Top_30A2C8, OlegGlobals.Big_30A2D0, centre);
        Label_25CC.GrowIn_25A890(Colour.GetColor_11BF10(15), OlegGlobals.Middle_30A2D8, OlegGlobals.Text_30A2E0, centre);
        Sprite_2660.GrowRectangle_256D20(Colour.GetColor_11BF10(15), new(0.5f, 0.675f), new(0.1f, 0.075f), panel);
        Menu_271C.GrowIn_25A890(Colour.GetColor_11BF10(15), panel, one, panel);
        Label_27C8.GrowIn_25A890(Colour.GetColor_11BF10(15), OlegGlobals.Middle_30A2D8, OlegGlobals.Text_30A2E0, centre);
        Menu_285C.GrowIn_25A890(Colour.GetColor_11BF10(15), panel, one, panel);
        Menu_1F28.Next_1C = Label_1FD4;
        Menu_2068.Next_1C = Label_2114;
        Label_223C.Next_1C = Label_22D0;
        Label_2364.Next_1C = Label_23F8;
        Label_23F8.Next_1C = Menu_248C;
        Label_2538.Next_1C = Sprite_2660;
        Sprite_2660.Next_1C = Label_25CC;
        Label_25CC.Next_1C = Menu_271C;
        Label_27C8.Next_1C = Menu_285C;

        var p0 = PauseRing_2AC4.AddRing_25AAA0(0, null);
        var p1 = PauseRing_2AC4.AddRing_25AAA0(1, null);
        var r0 = PauseRing_2B58.AddRing_25AAA0(0, null);
        var r1 = PauseRing_2B58.AddRing_25AAA0(1, null);
        var r2 = PauseRing_2B58.AddRing_25AAA0(2, null);
        var r3 = PauseRing_2B58.AddRing_25AAA0(3, null);
        p0.OuterColours_38 = OlegGlobals.ColourC_30A410;
        p0.InnerColour_1C = OlegGlobals.Blue_30A3D8;
        p0.OuterScale_2C = new(0.075f, 0.1f);
        p1.OuterColour_20 = OlegGlobals.BlueClear_30A3E0;
        p1.OuterScale_2C = new(0.077f, 0.102f);
        r0.Start_48 = OlegGlobals.AngleM36_30A3B8;
        r0.Sweep_4C = OlegGlobals.Angle122_30A3C0;
        r0.InnerColour_1C = OlegGlobals.White_30A3F0;
        r0.InnerShape_3C = OlegGlobals.CurveA_30A400;
        r0.OuterShape_40 = OlegGlobals.CurveB_30A408;
        r0.OuterColour_20 = OlegGlobals.White_30A3F0;
        r0.InnerScale_24 = new(0.33f, 0.308f);
        r0.OuterScale_2C = new(0.42f, 0.392f);
        r0.Rotation_44 = OlegGlobals.Angle21_30A398;
        r1.Start_48 = OlegGlobals.AngleM36_30A3B8;
        r1.Sweep_4C = OlegGlobals.Angle122_30A3C0;
        r1.InnerColour_1C = OlegGlobals.Clear_30A3F8;
        r1.InnerShape_3C = OlegGlobals.CurveA_30A400;
        r1.OuterShape_40 = OlegGlobals.CurveA_30A400;
        r1.OuterColour_20 = OlegGlobals.White_30A3F0;
        r1.InnerScale_24 = new(0.325f, 0.303f);
        r1.OuterScale_2C = new(0.33f, 0.308f);
        r1.Rotation_44 = OlegGlobals.Angle21_30A398;
        r2.Start_48 = OlegGlobals.AngleM36_30A3B8;
        r2.Sweep_4C = OlegGlobals.Angle122_30A3C0;
        r2.InnerColour_1C = OlegGlobals.White_30A3F0;
        r2.OuterColour_20 = OlegGlobals.Clear_30A3F8;
        r2.InnerShape_3C = OlegGlobals.CurveB_30A408;
        r2.OuterShape_40 = OlegGlobals.CurveB_30A408;
        r2.InnerScale_24 = new(0.42f, 0.392f);
        r2.OuterScale_2C = new(0.425f, 0.397f);
        r2.Rotation_44 = OlegGlobals.Angle21_30A398;
        r3.Start_48 = OlegGlobals.AngleM35_30A3A8;
        r3.Sweep_4C = OlegGlobals.Angle120_30A3B0;
        r3.InnerColour_1C = OlegGlobals.Mint_30A3E8;
        r3.InnerShape_3C = OlegGlobals.CurveA_30A400;
        r3.OuterColour_20 = OlegGlobals.Mint_30A3E8;
        r3.OuterShape_40 = OlegGlobals.CurveB_30A408;
        r3.InnerScale_24 = new(0.338f, 0.315f);
        r3.OuterScale_2C = new(0.413f, 0.385f);
        r3.Rotation_44 = OlegGlobals.Angle21_30A398;
        PauseRing_2B58.Independent_8B = 1;
        var size = new Vector2(0.066f, 0.088f);
        AddPanelRings_16A288(size, PauseRing_2908);
        AddPanelRings_16A288(size, PauseRing_299C);
        AddPanelRings_16A288(size, PauseRing_2A30);

        var middle = new Vector2(0.5f, 0.5f);
        var left = new Vector2(-0.5f, 0f);
        PauseRing_2AC4.GrowIn_25A890(Colour.GetColor_11BF10(15), new(0.71f, BitConverter.Int32BitsToSingle(0x3F10624D)), one, middle);
        PausePercent_2CA8.GrowIn_25A890(Colour.GetColor_11BF10(15), new(0.71f, BitConverter.Int32BitsToSingle(0x3F10624D)), one, middle);
        PauseRing_2B58.SlideIn_25A7C8(Colour.GetColor_11BF10(15), new(0.53f, BitConverter.Int32BitsToSingle(0x3ED81063)), one, new(0.5f, 0f));
        PauseRing_2908.SlideIn_25A7C8(Colour.GetColor_11BF10(15), new(0.175f, BitConverter.Int32BitsToSingle(0x3E5B22D2)), one, left);
        PauseWumpa_2D44.SlideIn_25A7C8(Colour.GetColor_11BF10(15), new(0.16f, BitConverter.Int32BitsToSingle(0x3E5B22D2)), one, left);
        PauseWumpaIcon_2DE0.SlideRectangle_256B98(Colour.GetColor_11BF10(15), new(0.198f, 0.199f), new(0.1f, 0.13f), left);
        PauseRing_299C.SlideIn_25A7C8(Colour.GetColor_11BF10(15), new(0.14f, BitConverter.Int32BitsToSingle(0x3EE353F7)), one, left);
        PauseLives_2E9C.SlideIn_25A7C8(Colour.GetColor_11BF10(15), new(0.108f, BitConverter.Int32BitsToSingle(0x3EE872AF)), one, left);
        PauseLivesIcon_2F38.SlideRectangle_256B98(Colour.GetColor_11BF10(15), new(0.153f, BitConverter.Int32BitsToSingle(0x3EDA9FBF)), new(0.13f, 0.13f), left);
        PauseRing_2A30.SlideIn_25A7C8(Colour.GetColor_11BF10(15), new(BitConverter.Int32BitsToSingle(0x3E666667), 0.664f), one, left);
        PauseCrystals_2FF4.SlideIn_25A7C8(Colour.GetColor_11BF10(15), new(0.2f, BitConverter.Int32BitsToSingle(0x3F2A3D70)), one, left);
        PauseCrystalIcon_3090.SlideRectangle_256B98(Colour.GetColor_11BF10(15), new(0.245f, 0.664f), new(0.125f, 0.28f), left);
        PauseWumpaIcon_2DE0.PulseAmount_98 = 0.1f;
        PauseWumpaIcon_2DE0.PulsePeriod_94 = 1.3f;
        PauseLivesIcon_2F38.BobPeriod_A0 = 0.37f;
        PauseLivesIcon_2F38.RockAmount_B8 = BitConverter.Int32BitsToSingle(0x3E490FDB);
        PauseLivesIcon_2F38.BobAmount_A8 = 0.0037f;
        PauseLivesIcon_2F38.RockPeriod_B0 = 1f;
        PauseCrystalIcon_3090.BobPeriod_A0 = 0.7f;
        PauseCrystalIcon_3090.BobAmount_A8 = 0.005f;
        for (int i = 0, k = 1; i < PauseGems_314C.Length; i++, k += 2)
        {
            var at = PauseRing_2B58.PointAt_25AB28(k * BitConverter.Int32BitsToSingle(0x3DAAAAAB));
            PauseGems_314C[i].SlideRectangle_256B98(Colour.GetColor_11BF10(15), at, new(0.1f, 0.1f), new(0.5f, 0f));
        }

        PauseRing_2AC4.Next_1C = PausePercent_2CA8;
        PausePercent_2CA8.Next_1C = PauseRing_2B58;
        PauseRing_2B58.Next_1C = Picture_2BEC;
        Picture_2BEC.Next_1C = PauseRing_2908;
        PauseRing_2908.Next_1C = PauseRing_299C;
        PauseRing_299C.Next_1C = PauseRing_2A30;
        PauseRing_2A30.Next_1C = PauseWumpaIcon_2DE0;
        PauseWumpaIcon_2DE0.Next_1C = PauseWumpa_2D44;
        PauseWumpa_2D44.Next_1C = PauseLives_2E9C;
        PauseLives_2E9C.Next_1C = PauseLivesIcon_2F38;
        PauseLivesIcon_2F38.Next_1C = PauseCrystals_2FF4;
        PauseCrystals_2FF4.Next_1C = PauseCrystalIcon_3090;
        PauseCrystalIcon_3090.Next_1C = PauseGems_314C[0];
        for (int i = 1; i < PauseGems_314C.Length; i++) PauseGems_314C[i - 1].Next_1C = PauseGems_314C[i];
    }

    // Amedo
    private void SetUpSaveScreens_16CC40()
    {
        var middle = OlegGlobals.Middle_30A2D8;
        var text = OlegGlobals.Text_30A2E0;
        var panel = OlegGlobals.PanelCentre_30A2B0;
        String_3878.GrowIn_25A890(Colour.GetColor_11BF10(15), middle, text, middle);
        String_3914.GrowIn_25A890(Colour.GetColor_11BF10(15), middle, text, middle);
        Menu_39B0.GrowIn_25A890(Colour.GetColor_11BF10(15), panel, Vector2.One, panel);
        String_3A5C.SlideIn_25A7C8(Colour.GetColor_11BF10(15), new(0.5f, 0.1f), text, new(0f, -0.5f));
        Ring_3AF8.SlideIn_25A7C8(Colour.GetColor_11BF10(15), new(0.5f, 0.1f), Vector2.One, new(0f, -0.5f));
        Menu_3B8C.GrowIn_25A890(Colour.GetColor_11BF10(15), panel, Vector2.One, panel);
        AddPanelRings_16A288(new Vector2(0.45f, 0.05f), Ring_3AF8);
        String_3914.Next_1C = Menu_39B0;
        String_3A5C.Next_1C = Ring_3AF8;
        Ring_3AF8.Next_1C = Menu_3B8C;
    }
}
