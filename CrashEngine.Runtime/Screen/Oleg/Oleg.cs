using System.Globalization;
using System.Numerics;
using CrashEngine.Runtime.Character;
using CrashEngine.Runtime.Character.Vehicles;
using CrashEngine.Runtime.Game;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Screen.Oleg;

// Amedo
public sealed class OlegClock : IOlegClock
{
    public int Now_04 { get; set; }
    public int FrameTicks_08 { get; set; }
}

// Amedo
public sealed class CyclingScale : IWidgetScale
{
    public uint Word_00 = 0x1E;
    public int Start_04, Duration_08;
    public readonly float[] Samples_0C = new float[30];
    public float Value_10;

    public float Scale_10 => Value_10;

    public static readonly CyclingScale Global_30BB90 = new();

    // Amedo
    public void StaticInit_178778()
    {
        Word_00 = 0x1E;
        Start_04 = 0;
        Duration_08 = 0;
        Array.Clear(Samples_0C);
        Value_10 = 0f;
    }

    // Amedo
    public bool Step_179F18(IOlegClock clock)
    {
        if ((Word_00 & 0x8000) == 0) return false;
        int count = (int)(Word_00 & 0x7FFF);
        if (Start_04 == 0)
        {
            Value_10 = Samples_0C[0];
            Start_04 = clock.Now_04;
            return true;
        }
        int elapsed = clock.Now_04 - Start_04;
        while (!(elapsed < Duration_08))
        {
            if (((Word_00 >> 16) & 0xFF) != 0)
            {
                uint repeats = ((Word_00 >> 24) - 1) & 0xFF;
                Word_00 = (Word_00 & 0x00FFFFFFu) | (repeats << 24);
                if (repeats == 0)
                {
                    Value_10 = Samples_0C[count - 1];
                    if ((Word_00 & 0x8000) != 0) Word_00 &= 0xFFFF7FFFu;
                    return false;
                }
            }
            elapsed -= Duration_08;
            Start_04 += Duration_08;
        }
        float t = elapsed * Widget.SecondsPerTick / (Duration_08 * Widget.SecondsPerTick);
        if (t < 1f)
        {
            float f = (count - 1) * t;
            int i = GameMath.Trunc(f);
            Value_10 = Samples_0C[i] + (Samples_0C[i + 1] - Samples_0C[i]) * (f - i);
        }
        else Value_10 = Samples_0C[count - 1];
        return true;
    }
}

// Amedo
public sealed partial class Oleg : WidgetController
{
    public const int SpriteCount = 46, FirstIconSprite = 6, PictureCount = 8, HudIcons = 3, FirstHudIconSprite = 3;
    public const int FaderSlot = 1, BottomBandSlot = 2, BottomTextSlot = 3;
    public const int CutsceneGroup = 0, FaderGroup = 1, BottomTextGroup = 2, BothCountersGroup = 31, WormGroup = 32, BossGroup = 33;
    public const int SliderGroup = 34, LivesGroup = 35, WumpaGroup = 36, PickupGroup = 37, AmmoGroup = 38, AllButOverlaysGroup = 42;
    public const int OptionsGroup = 9, ScreenPositionGroup = 10, MainMenuGroup = 11, PauseGroup = 12, DisableAutosaveGroup = 13, QuitGroup = 14; // Amedo
    public const int MainMenuMode = 8; // Amedo
    public const int NoPadGroup = 15, GalleryGroup = 25, GameOverGroup = 26; // Amedo
    public const int FlashGroup = 39, PictureGroup = 40, LoadingGroup = 41; // Amedo
    public const int DimmerGroup = 3, SaveMessageGroup = 27, SaveChoicesGroup = 28, SaveSlotsGroup = 29, AutosaveGroup = 30; // Amedo
    public IOlegGame? Game_3095C8; // Amedo

    private static readonly float IconY = BitConverter.Int32BitsToSingle(0x3DF5C290);
    private static readonly uint ShadowColour_30A298 = Colour.Set_101218(0f, 0f, 0f, 0.5f);
    private static readonly Vector2 ShadowOffset_30A290 = new(0.01f, 0.01f);

    public byte PickupSprite_310, WumpaPending_311;
    public int PickupGem_312;
    public ulong Picture_318 = 25ul << 1 | 25ul << 9 | 12ul << 33 | 12ul << 41;
    public int SaveStatus_320;
    public int WumpaTimer_324;
    public readonly CurveScaleEffect Pulse_328 = new(new(1f, 1f), new(1.2f, 1.2f), new(1.3f, 1.3f), new(1f, 1f));
    public readonly SlideEffect Slide_348 = new();
    public readonly SpinEffect Spin_360 = new();
    public readonly RockEffect Rock_378 = new();
    public readonly GameProgress Progress_390;
    public GameFont.Page? ParticleMaterial_818; // Amedo
    public readonly Sprite ParticleSprite_81C = new(); // Amedo
    public readonly ColourCurve ParticleColours_83C = new(new Vector4[8]); // Amedo
    public readonly Vector2Curve ParticleScales_844 = new(new Vector2[8]); // Amedo
    public readonly FloatCurve ParticleTurn_84C = new(2), ParticleSpin_854 = new(2); // Amedo
    public const int GemEmitters = 7, LivesEmitter = 7; // Amedo
    public readonly Emitter2D[] Emitters_85C = new Emitter2D[GemEmitters + 1]; // Amedo
    public readonly SparkleEffect Sparkle_AC0 = new(); // Amedo
    private static readonly Vector4[] Sparkles_30BA90 = // Amedo
    [
        new(BitConverter.Int32BitsToSingle(0x3DF0F0F2), BitConverter.Int32BitsToSingle(0x3E048485), 1f, 1f),
        new(1f, 1f, 1f, 1f),
        new(BitConverter.Int32BitsToSingle(0x3D40C0C2), BitConverter.Int32BitsToSingle(0x3ED6D6D8), BitConverter.Int32BitsToSingle(0x3E5CDCDE), 1f),
        new(BitConverter.Int32BitsToSingle(0x3F30B0B1), 0f, BitConverter.Int32BitsToSingle(0x3F30B0B1), 1f),
        new(BitConverter.Int32BitsToSingle(0x3F57D7D9), BitConverter.Int32BitsToSingle(0x3DA8A8A9), BitConverter.Int32BitsToSingle(0x3DD8D8DA), 1f),
        new(BitConverter.Int32BitsToSingle(0x3F48C8CA), BitConverter.Int32BitsToSingle(0x3F48C8CA), 0f, 1f),
        new(BitConverter.Int32BitsToSingle(0x3F6AEAEC), 0f, 0f, 1f),
    ];
    public const int CrystalSparkle = 6, CrystalPickup = 0x14; // Amedo
    public readonly GameFont? Font_394;
    public readonly GameTexts? CodeTexts_309770;
    public readonly GameFont.Page?[] HudIcons_B40 = new GameFont.Page?[HudIcons];
    public readonly Sprite[] Sprites_B4C = new Sprite[SpriteCount];
    public readonly Sprite[] Pictures_110C = new Sprite[PictureCount];
    public readonly TextLine TextLine_120C;
    public readonly SpriteWidget Letterbox_1240, LetterboxBottom_12FC, Fader_13B8, BottomBand_1474;
    public readonly StringLabel BottomText_1530;
    public readonly SpriteWidget Picture_2BEC, PauseWumpaIcon_2DE0, PauseLivesIcon_2F38, PauseCrystalIcon_3090;
    public readonly StringLabel PausePercent_2CA8, PauseWumpa_2D44, PauseLives_2E9C, PauseCrystals_2FF4;
    public readonly SpriteWidget[] PauseGems_314C = new SpriteWidget[GameProgress.Gems];
    public readonly Label SaveLabel_3C38;
    public readonly SpriteWidget SaveIcon_3CCC;
    public readonly StringLabel WormText_3D88, TimeText_3EE0;
    public readonly SpriteWidget WormIcon_3E24, ClockIcon_3F7C;
    public readonly HudBarWidget BossBar_4038;
    public readonly SliderWidget Gauge_40C8;
    public readonly StringLabel WumpaText_4164, LivesText_42BC, AmmoText_458C;
    public readonly SpriteWidget WumpaIcon_4200, LivesIcon_4358, OneUp_4414, Pickup_44D0, AmmoIcon_4628;

    // Amedo
    public Oleg(GameFont? font, GameProgress progress, GameTexts? codeTexts = null, IReadOnlyList<GameFont.Page?>? icons = null)
    {
        CyclingScale.Global_30BB90.StaticInit_178778(); // Amedo
        MenuPage.StaticInit_2597C0(); // Amedo
        LevelWidget.ClearSbss(); // Amedo
        Progress_390 = progress;
        Font_394 = font;
        CodeTexts_309770 = codeTexts;
        Input_3B0 = new MenuInput(Bindings_3A4);
        for (int i = 0; i < SpriteCount; i++) Sprites_B4C[i] = new Sprite();
        for (int i = 0; i < Emitters_85C.Length; i++) Emitters_85C[i] = new Emitter2D(); // Amedo
        for (int i = 0; i < PictureCount; i++) Pictures_110C[i] = new Sprite();
        TextLine_120C = new TextLine(font);
        Letterbox_1240 = new SpriteWidget(Sprites_B4C[0]);
        LetterboxBottom_12FC = new SpriteWidget(Sprites_B4C[0]);
        Fader_13B8 = new SpriteWidget(Sprites_B4C[0]);
        BottomBand_1474 = new SpriteWidget(Sprites_B4C[0]);
        BottomText_1530 = new StringLabel(font, 0x22, 0.5f);
        Picture_2BEC = new SpriteWidget(Sprites_B4C[2], 0.5f);
        PausePercent_2CA8 = new StringLabel(font, 0x22, 0.5f);
        PauseWumpa_2D44 = new StringLabel(font, 0x60, 0.5f);
        PauseWumpaIcon_2DE0 = new SpriteWidget(Sprites_B4C[19], 0.5f);
        PauseLives_2E9C = new StringLabel(font, 0x60, 0.5f);
        PauseLivesIcon_2F38 = new SpriteWidget(Sprites_B4C[6], 0.5f);
        PauseCrystals_2FF4 = new StringLabel(font, 0x60, 0.5f);
        PauseCrystalIcon_3090 = new SpriteWidget(Sprites_B4C[21], 0.5f);
        SaveLabel_3C38 = new Label(font, codeTexts, 0x43, 0x22, 0.5f);
        SaveIcon_3CCC = new SpriteWidget(Sprites_B4C[23], 0.5f);
        WormText_3D88 = new StringLabel(font, 0x30, 0.95f);
        WormIcon_3E24 = new SpriteWidget(Sprites_B4C[4], 0.05f);
        TimeText_3EE0 = new StringLabel(font, 0x30, 0.95f);
        ClockIcon_3F7C = new SpriteWidget(Sprites_B4C[24], 0.05f);
        BossBar_4038 = new HudBarWidget(Sprites_B4C, progress, 0.05f);
        Gauge_40C8 = new SliderWidget(0.5f) { Bar_8C = Sprites_B4C[12], Value_88 = 0.5f, Knob_90 = Sprites_B4C[19] };
        WumpaText_4164 = new StringLabel(font, 0x14, 0.05f);
        WumpaIcon_4200 = new SpriteWidget(Sprites_B4C[19], 0.05f);
        LivesText_42BC = new StringLabel(font, 0x44, 0.95f);
        LivesIcon_4358 = new SpriteWidget(Sprites_B4C[6], 0.95f);
        OneUp_4414 = new SpriteWidget(Sprites_B4C[6], 0.2f);
        Pickup_44D0 = new SpriteWidget(Sprites_B4C[20], 0.5f);
        AmmoText_458C = new StringLabel(font, 0x44, 0.95f);
        AmmoIcon_4628 = new SpriteWidget(Sprites_B4C[22], 0.95f);
        ConstructMenus_16DB70(font, codeTexts);
        for (int i = 6; i < 12; i++) Sprites_B4C[i].Centred_08 = true;
        for (int i = 30; i < SpriteCount; i++) Sprites_B4C[i].Centred_08 = true;
        foreach (int i in (ReadOnlySpan<int>)[1, 2, 19, 20, 21, 23, 29]) Sprites_B4C[i].Centred_08 = true;
        ParticleSprite_81C.Centred_08 = true; // Amedo
        WumpaTimer_324 = GameMath.Trunc(Widget.TicksPerSecond * 0.15f);
        Pulse_328.Duration_00 = 0.15f;
        for (int i = 0; i < GameProgress.Gems; i++) PauseGems_314C[i] = new SpriteWidget(Sprites_B4C[13 + i], 0.5f);
        SetGroups_16A040();
        LoadIcons_16CE50(icons);
        uint white = Colour.GetColor_11BF10(15);
        BossBar_4038.SlideIn_25A7C8(white, new(0.05f, 0.05f), new(1f, 1f), new(-0.5f, -0f));
        Menu_1BD4.GrowIn_25A890(Colour.GetColor_11BF10(15), OlegGlobals.PanelCentre_30A2B0, Vector2.One, OlegGlobals.PanelCentre_30A2B0);
        Label_1C80.GrowIn_25A890(Colour.GetColor_11BF10(15), OlegGlobals.MenuCentre_30A2A8, new(0.625f, 0.625f), OlegGlobals.MenuCentre_30A2A8);
        Menu_1BD4.Next_1C = Label_1C80;
        SetUpTimeHud_16A408();
        SetUpOverlays_16A588();
        SetUpExtras_16A828();
        SetUpMainMenu_16AA50();
        SetUpLoadingScreen_16ABA0();
        Menu_37CC.FadeIn_25A760(Colour.GetColor_11BF10(15), new(0.5f, 0.1f), Vector2.One);
        SetUpMenuRings_16AD68();
        SetUpLevelsMenus_16BA28();
        SetUpOptionsMenu_16BC48();
        SetUpPause_16BD48();
        SetUpSaveScreens_16CC40();
        Gauge_40C8.SlideIn_25A7C8(white, new(0.5f, 0.15f), new(0.5f, 0.1f), new(0f, -0.5f));
        SaveLabel_3C38.FadeIn_25A760(white, new(0.5f, 0.4f), new(0.75f, 0.75f));
        SaveIcon_3CCC.SlideRectangle_256B98(white, new(0.5f, 0.85f), new(0.1f, 0.1f), new(0f, 0.5f));
        SaveIcon_3CCC.Next_1C = SaveLabel_3C38;
        WumpaText_4164.SlideIn_25A7C8(white, new(0.16f, 0.19f), new(1.5f, 1.5f), new(-0.5f, 0f));
        WumpaIcon_4200.SlideRectangle_256B98(white, new(0.1f, IconY), new(0.11f, 0.15f), new(-0.5f, 0f));
        LivesText_42BC.SlideIn_25A7C8(white, new(0.81f, 0.19f), new(1.5f, 1.5f), new(0.5f, 0f));
        LivesIcon_4358.SlideRectangle_256B98(white, new(0.88f, IconY), new(0.16f, 0.15f), new(0.5f, 0f));
        AmmoText_458C.FadeIn_25A760(white, new(BitConverter.Int32BitsToSingle(0x3F4CCCCC), 0.95f), new(1f, 1f));
        AmmoIcon_4628.FadeRectangle_256A70(white, new(0.875f, 0.875f), new(0.15f, 0.15f));
        Pickup_44D0.GrowRectangle_256D20(white, new(0.5f, 0.2f), new(0.2f, 0.2f), new(0.5f, 0.2f));
        OneUp_4414.GrowRectangle_256D20(white, new(0.34f, 0.12f), new(0.16f, 0.15f), new(0.34f, 0.12f));
        WumpaText_4164.Next_1C = WumpaIcon_4200;
        LivesText_42BC.Next_1C = LivesIcon_4358;
        AmmoText_458C.Next_1C = AmmoIcon_4628;
        SetMenuWidgets_16F1FC();
        SetWidget_259CE8(0, Letterbox_1240);
        SetWidget_259CE8(FaderSlot, Fader_13B8);
        SetWidget_259CE8(BottomBandSlot, BottomBand_1474);
        SetWidget_259CE8(BottomTextSlot, BottomText_1530);
        SetWidget_259CE8(0x24, SaveIcon_3CCC);
        SetWidget_259CE8(0x25, WormText_3D88);
        SetWidget_259CE8(0x26, BossBar_4038);
        SetWidget_259CE8(0x27, Gauge_40C8);
        SetWidget_259CE8(0x28, WumpaText_4164);
        SetWidget_259CE8(0x29, LivesText_42BC);
        SetWidget_259CE8(0x2A, Pickup_44D0);
        SetWidget_259CE8(0x2B, AmmoText_458C);
        SetWidget_259CE8(0x2C, Flash_46E4);
        SetWidget_259CE8(0x2D, Picture_47A0);
        SetWidget_259CE8(0x2E, Loading_482C);
    }

    // Amedo
    private void SetGroups_16A040()
    {
        ReadOnlySpan<ulong> groups =
        [
            0x9, 0x2, 0xC, 0x10, 0x80, 0x100, 0x200, 0x400, 0x800, 0x12B0, 0x22F0, 0xC2F0, 0x90EF0, 0x20EF0, 0x40EF0, 0x100030,
            0x200030, 0x400270, 0x800270, 0x1000270, 0x22000CB0, 0x24000CB0, 0x28000CB0, 0x30000CB0, 0x40000EB0,
            0x8002ul << 30 | 0x80, 0xC004ul << 30 | 0x200, 0x8000ul << 18 | 0x30, 0x8000ul << 19 | 0x270, 0x8000ul << 20 | 0x270,
            0x8000ul << 21, 0xC000ul << 26, 0x8000ul << 22, 0x9000ul << 26, 0x8000ul << 24, 0x8000ul << 26, 0x8000ul << 25,
            0x8000ul << 27, 0x8000ul << 28, 0x8000ul << 29, 0x8000ul << 30, 0xC000ul << 31, 0xFFFFFFFFFFFFFFF8ul, 0xFFFFFFFFFFFEFFFFul,
        ];
        groups.CopyTo(Groups_08);
    }

    // Amedo
    private void LoadIcons_16CE50(IReadOnlyList<GameFont.Page?>? icons)
    {
        for (int i = 0; i < SpriteCount; i++)
        {
            if (i < FirstIconSprite) Sprites_B4C[i].SetMaterial_1ABD50(null);
            else Sprites_B4C[i].SetMaterial_1ABD50(icons is not null && i < icons.Count ? icons[i] : null);
        }
    }

    // Amedo
    private void SetUpTimeHud_16A408()
    {
        uint white = Colour.GetColor_11BF10(15);
        WormText_3D88.FadeIn_25A760(white, new(0.15f, 0.8f), new(1f, 1f));
        WormIcon_3E24.FadeRectangle_256A70(white, new(0.1f, 0.9f), new(0.1f, 0.1f));
        TimeText_3EE0.FadeIn_25A760(white, new(0.15f, 0.9f), new(1f, 1f));
        ClockIcon_3F7C.FadeRectangle_256A70(white, new(0.1f, 0.9f), new(0.1f, 0.1f));
        WormText_3D88.Next_1C = WormIcon_3E24;
        WormIcon_3E24.Next_1C = TimeText_3EE0;
        TimeText_3EE0.Next_1C = ClockIcon_3F7C;
    }

    // Amedo
    private void SetUpOverlays_16A588()
    {
        uint black = Colour.GetColor_11BF10(8);
        Letterbox_1240.HiddenColour_40 = Letterbox_1240.ShownColour_44 = black;
        Letterbox_1240.ShownPosition_60 = new(0f, 0f);
        Letterbox_1240.HiddenPosition_58 = new(0f, -0.15f);
        Letterbox_1240.ShownVector_80 = new(1f, 0.15f);
        Letterbox_1240.HiddenVector_78 = new(1f, 0f);
        Letterbox_1240.State_00 = (Letterbox_1240.State_00 & 0xFFFF87FFu) | 0x1000;
        LetterboxBottom_12FC.HiddenColour_40 = LetterboxBottom_12FC.ShownColour_44 = black;
        LetterboxBottom_12FC.ShownPosition_60 = new(0f, 0.85f);
        LetterboxBottom_12FC.HiddenPosition_58 = new(0f, 1f);
        LetterboxBottom_12FC.ShownVector_80 = new(1f, 1f);
        LetterboxBottom_12FC.HiddenVector_78 = new(1f, 1.15f);
        LetterboxBottom_12FC.State_00 = (LetterboxBottom_12FC.State_00 & 0xFFFF87FFu) | 0x1000;
        Fader_13B8.HiddenColour_40 = Colour.GetColor_11BF10(0);
        Fader_13B8.ShownColour_44 = black;
        Fader_13B8.State_00 = (Fader_13B8.State_00 & 0xFFFF87FFu) | 0x800;
        BottomBand_1474.HiddenColour_40 = Colour.Set_101218(0f, 0f, 0f, 0f);
        BottomBand_1474.ShownColour_44 = Colour.Set_101218(0f, 0f, 0f, 0.5f);
        BottomBand_1474.State_00 = (BottomBand_1474.State_00 & 0xFFFF87FFu) | 0x1000;
        BottomBand_1474.ShownPosition_60 = BottomBand_1474.HiddenPosition_58 = new(0f, 0.85f);
        BottomBand_1474.ShownVector_80 = BottomBand_1474.HiddenVector_78 = new(1f, 1f);
        BottomText_1530.HiddenColour_40 = Colour.Set_101218(1f, 1f, 1f, 0f);
        BottomText_1530.ShownColour_44 = Colour.Set_101218(1f, 1f, 1f, 1f);
        BottomText_1530.ShownPosition_60 = BottomText_1530.HiddenPosition_58 = new(0.5f, 0.9f);
        BottomText_1530.ShownVector_80 = BottomText_1530.HiddenVector_78 = new(0.5f, 0.5f);
        Letterbox_1240.Next_1C = LetterboxBottom_12FC;
    }

    // Amedo
    public void SetHudIcon_1711A8(int slot, GameFont.Page? material)
    {
        HudIcons_B40[slot] = material;
        Sprites_B4C[FirstHudIconSprite + slot].SetMaterial_1ABD50(material);
    }

    // Amedo
    private void PictureLoaded_179D80(int kind, IReadOnlyList<GameFont.Page?> pages)
    {
        SetPictureWord(kind, PictureWord(kind) & ~1u);
        if (kind == 1) Sprites_B4C[2].SetMaterial_1ABD50(pages.Count > 0 ? pages[0] : null);
        else if (kind == 0) Sprites_B4C[1].SetMaterial_1ABD50(pages.Count > 0 ? pages[0] : null);
        else if (kind == 2)
            for (int i = 0; i < Pictures_110C.Length; i++) Pictures_110C[i].SetMaterial_1ABD50(i < pages.Count ? pages[i] : null);
    }

    // Amedo
    public void Reset_179E60()
    {
        TextLine_120C.Text_08 = "";
        WumpaPending_311 = 0;
        WumpaTimer_324 = GameMath.Trunc(Widget.TicksPerSecond * 0.15f);
        for (int i = FirstHudIconSprite; i < FirstIconSprite; i++) Sprites_B4C[i].SetMaterial_1ABD50(null);
    }

    // Amedo
    public void AddLives_1712D8(int n, bool fromWumpa, Action? oneUpSound)
    {
        int lives = Progress_390.Lives + n;
        Progress_390.Lives = lives < 0 ? 0 : lives < 100 ? lives & 0x7F : 100;
        Show_259B20(Groups_08[LivesGroup], GameMath.Trunc(Widget.TicksPerSecond), GameMath.Trunc(Widget.TicksPerSecond * 1.5f));
        Rock_378.Count_0C = 0;
        Rock_378.Elapsed_04 = 0f;
        Rock_378.Progress_08 = 0f;
        Rock_378.Duration_00 = 0.45f;
        Rock_378.Count_0C = 7;
        LivesIcon_4358.Effect_20 = Rock_378;
        LivesIcon_4358.Next_1C?.SetEffect_259950(Rock_378);
        if (!fromWumpa) return;
        OneUp_4414.Sprite_88 = Sprites_B4C[FirstIconSprite + Progress_390.Character];
        OneUp_4414.Appear_254EE0(GameMath.Trunc(Widget.TicksPerSecond * 0.5f), GameMath.Trunc(Widget.TicksPerSecond));
        Slide_348.Count_0C = 0;
        Slide_348.Elapsed_04 = 0f;
        Slide_348.Progress_08 = 0f;
        Slide_348.Duration_00 = 0.75f;
        Slide_348.Count_0C++;
        Spin_360.Count_0C = 0;
        Spin_360.Elapsed_04 = 0f;
        Spin_360.Progress_08 = 0f;
        Spin_360.Duration_00 = 0.75f;
        Spin_360.Count_0C++;
        Slide_348.Next_10 = Spin_360;
        OneUp_4414.Effect_20 = Slide_348;
        OneUp_4414.Next_1C?.SetEffect_259950(Slide_348);
        oneUpSound?.Invoke();
    }

    // Amedo
    public void Update_1714A8(IOlegClock clock, CharacterController? player, int runState, Action? oneUpSound)
    {
        CyclingScale.Global_30BB90.Step_179F18(clock);
        if (runState == 3)
        {
            WumpaTimer_324 = GameMath.Trunc(Widget.TicksPerSecond * 0.15f);
            Hide_259BC0(Groups_08[WumpaGroup], GameMath.Trunc(Widget.TicksPerSecond * 0.1f), 0);
            OneUp_4414.Disappear_255040(GameMath.Trunc(Widget.TicksPerSecond * 0.1f), 0);
        }
        else if (WumpaPending_311 != 0)
        {
            int frame = clock.FrameTicks_08;
            if (!(frame < WumpaTimer_324))
            {
                int wumpa = Progress_390.Wumpa + 1;
                if (!(wumpa < 100))
                {
                    wumpa -= 100;
                    AddLives_1712D8(1, true, oneUpSound);
                }
                Progress_390.Wumpa = wumpa & 0x7F;
                float interval = 0.15f;
                if (!(WumpaPending_311 < 2))
                    interval *= CrashEngine.Core.GameMath.SinCosVu0_2E4C30(1f / WumpaPending_311).Sin;
                WumpaPending_311--;
                Pulse_328.Duration_00 = interval;
                WumpaTimer_324 = GameMath.Trunc(interval * Widget.TicksPerSecond);
                Pulse_328.Count_0C++;
                Show_259B20(Groups_08[WumpaGroup], GameMath.Trunc(Widget.TicksPerSecond * 0.1f), GameMath.Trunc(Widget.TicksPerSecond * 1.5f));
                SetEffect_259A90(Groups_08[WumpaGroup], Pulse_328);
                if (WumpaPending_311 == 0 && !(Pulse_328.Count_0C < 2)) Pulse_328.Count_0C = 1;
            }
            else WumpaTimer_324 -= frame;
        }
        UpdateHud_169640(player);
        Update_259DB8(clock);
        TextLine_120C.Widget_30 = BottomText_1530;
        TextLine_120C.Update_2567C0(clock);
    }

    // Amedo
    public MenuWidget? ShownMenu_171740()
    {
        if (Menu_1BD4.Phase == 3) return Menu_1BD4;
        if (Menu_353C.Phase == 3) return Menu_353C;
        if (MainMenu_1D14.Phase == 3) return MainMenu_1D14;
        foreach (var levels in Levels_3164)
            if (levels.Phase == 3) return levels;
        if (Options_1A94.Phase == 3) return Options_1A94;
        if (Menu_1E7C.Phase == 3) return Menu_1E7C;
        if (Menu_1F28.Phase == 3) return Menu_1F28;
        if (Menu_2068.Phase == 3) return Menu_2068;
        if (Menu_271C.Phase == 3) return Menu_271C;
        if (Menu_248C.Phase == 3) return Menu_248C;
        return Menu_285C.Phase == 3 ? Menu_285C : null;
    }

    private readonly NumberText _wumpa = new(), _crystals = new(), _lives = new(), _ammo = new(), _percent = new();
    private (int Count, int Total) _wormKey = (-1, -1);
    private int _timeKey = int.MinValue;

    // Amedo
    private sealed class NumberText
    {
        private int _value = int.MinValue;
        private string _text = "";

        public string Of(int value, string suffix = "")
        {
            if (value == _value) return _text;
            _value = value;
            return _text = value.ToString(CultureInfo.InvariantCulture) + suffix;
        }
    }

    private static void SetOff(Widget w, bool off) => w.State_00 = (w.State_00 & ~Widget.Off) | (off ? Widget.Off : 0u);

    // Amedo
    public void UpdateHud_169640(CharacterController? player)
    {
        var gun = player?.Gun;
        var vehicle = player?.Vehicle;
        var p = Progress_390;
        int mode = p.HudMode;
        bool worm = mode == 2, boss = mode == 1;
        int lives = p.Lives;
        var wrestle = vehicle is { Kind: Vehicle.Wrestle } ? vehicle as Wrestle : null;
        string wumpa = _wumpa.Of(p.Wumpa);
        int crystals = 0;
        for (int i = 0; i < GameProgress.LevelWords; i++) crystals += (int)((p.LevelWords_34[i] >> 8) & 1);
        string crystalText = _crystals.Of(crystals);
        MenuWidget? levels = null;
        foreach (var menu in Levels_3164)
            if (menu.Phase >= 2) { levels = menu; break; }
        if (levels?.Home_8C is { } page)
        {
            int index = page.Selected_0C[0];
            int levelIndex = index < page.Items_3C.Count ? (int)(page.Items_3C[index].Word_04 & 0xFFF) : 0;
            LevelTitle_3414.TextIndex_90 = ((p.Word_04 >> 26) & 0x1F) < levelIndex ? 0x58 : GameProgress.LevelSlot_2E6C40[levelIndex] + 0x47;
        }
        if (worm)
        {
            int seconds = GameMath.Trunc(p.WormTicks_7C * Widget.SecondsPerTick);
            int minutes = seconds / 60, rest = seconds - minutes * 60;
            var key = (p.WormCount, p.WormTotal);
            if (_wormKey != key)
            {
                _wormKey = key;
                WormText_3D88.Text_90 = key.WormCount.ToString(CultureInfo.InvariantCulture) + "/" + key.WormTotal.ToString(CultureInfo.InvariantCulture);
            }
            if (_timeKey != seconds)
            {
                _timeKey = seconds;
                TimeText_3EE0.Text_90 = minutes.ToString(CultureInfo.InvariantCulture) + (rest < 10 ? ":0" : ":") + rest.ToString(CultureInfo.InvariantCulture);
            }
        }
        SetOff(WormText_3D88, !worm);
        SetOff(WormIcon_3E24, !(worm && HudIcons_B40[1] is not null));
        SetOff(TimeText_3EE0, !worm);
        SetOff(ClockIcon_3F7C, !worm);
        if (!boss) WumpaText_4164.Text_90 = wumpa;
        SetOff(BossBar_4038, !boss);
        SetOff(WumpaText_4164, boss);
        if (wrestle is not null)
        {
            float gauge = wrestle.Gauge_161228();
            Gauge_40C8.Left_98 = Sprites_B4C[5];
            Gauge_40C8.Right_94 = Sprites_B4C[6];
            Gauge_40C8.Value_88 = gauge - 0.5f;
        }
        SetOff(Gauge_40C8, wrestle is null);
        PauseWumpa_2D44.Text_90 = wumpa;
        PauseCrystals_2FF4.Text_90 = crystalText;
        string livesText = _lives.Of(lives < 2 ? 0 : lives - 1);
        Pickup_44D0.Sprite_88 = Sprites_B4C[PickupSprite_310];
        if (LivesIcon_4358.Phase >= 2)
        {
            LivesText_42BC.Text_90 = livesText;
            LivesIcon_4358.Sprite_88 = Sprites_B4C[FirstIconSprite + p.Character];
        }
        if (PauseLivesIcon_2F38.Phase >= 2)
        {
            PauseLives_2E9C.Text_90 = livesText;
            PauseLivesIcon_2F38.Sprite_88 = Sprites_B4C[FirstIconSprite + p.Character];
            PauseLivesIcon_2F38.Emitter_8C = p.Character != 0 ? null : Emitters_85C[LivesEmitter]; // Amedo
        }
        if (gun is not null) AmmoText_458C.Text_90 = _ammo.Of(gun.Ammo);
        SetOff(AmmoIcon_4628, !(gun is not null && !boss));
        SetOff(AmmoText_458C, !(gun is not null && !boss));
        if (SaveIcon_3CCC.Phase >= 2)
        {
            if ((uint)SaveStatus_320 < 2)
            {
                SetOff(SaveLabel_3C38, false);
                SaveIcon_3CCC.Scale_24 = CyclingScale.Global_30BB90;
            }
            else
            {
                SetOff(SaveLabel_3C38, true);
                SaveIcon_3CCC.Scale_24 = null;
            }
        }
        int level = p.Level;
        ref uint entry = ref p.LevelWord(level);
        int gems = 0;
        for (int gem = 0; gem < GameProgress.Gems; gem++) gems += p.GemsFound_1795A8(gem);
        PausePercent_2CA8.Text_90 = _percent.Of(GameProgress.LevelPercent_2E6CA8[(p.Word_04 >> 21) & 0x1F] + gems / 3, "%");
        bool shown = (Picture_318 & 1) == 0 && (int)((Picture_318 >> 1) & 0xFF) == level;
        if (shown)
        {
            bool small = level is 6 or 0 or 0xD or 0x14;
            var size = small ? new Vector2(0.4f, 0.4f) : new Vector2(0.6f, 0.4f);
            Picture_2BEC.GrowRectangle_256D20(Colour.GetColor_11BF10(15), new(0.45f, 0.364f), size, new(0.5f, 0.5f));
            Picture_2BEC.ShadowColour_28 = ShadowColour_30A298;
            Picture_2BEC.ShadowOffset_2C = ShadowOffset_30A290;
            SetOff(Picture_2BEC, false);
            Picture_2BEC.PulseAmount_98 = 0.03f;
            Picture_2BEC.PulsePeriod_94 = 0.6f;
        }
        else
        {
            if ((Picture_318 & 1) == 0)
            {
                Picture_318 = (Picture_318 & 0xFFFFFFFFFFFE01FFul) | ((ulong)(uint)level << 9);
                LoadPicture_170ED0(1, false);
            }
            SetOff(Picture_2BEC, true);
        }
        for (int gem = 0; gem < GameProgress.Gems; gem++)
        {
            var w = PauseGems_314C[gem];
            if ((entry & 0xFF & (1u << gem)) != 0)
            {
                w.Sprite_88 = Sprites_B4C[13 + gem];
                w.Emitter_8C = Emitters_85C[gem + 1]; // Amedo
            }
            else
            {
                w.Sprite_88 = Sprites_B4C[12];
                w.Emitter_8C = null;
            }
        }
    }

    // Amedo
    public void PlayPickupEffect_16D0D0(int group)
    {
        if (group == LivesGroup)
        {
            Rock_378.Count_0C = 0;
            Rock_378.Elapsed_04 = 0f;
            Rock_378.Progress_08 = 0f;
            Rock_378.Duration_00 = 0.45f;
            Rock_378.Count_0C = 7;
            SetEffect_259A90(Groups_08[LivesGroup], Rock_378);
            return;
        }
        if (group != PickupGroup) return;
        var radial = Sparkle_AC0.Emitter_20;
        radial.Pool_00.Clear_17C298();
        radial.Count_38 = 4;
        radial.Life_30 = 0.5f;
        radial.Chance_34 = 1.9f;
        Sparkle_AC0.Count_0C = 1;
        Sparkle_AC0.Elapsed_04 = 0f;
        Sparkle_AC0.Progress_08 = 0f;
        radial.Colour_50 = Sparkles_30BA90[PickupSprite_310 == CrystalPickup ? CrystalSparkle : PickupGem_312 & 0xF];
        SetEffect_259A90(Groups_08[group], Sparkle_AC0);
    }

    // Amedo
    public void SetUpParticles_16D368(GameFont.Page? particlePage)
    {
        ParticleMaterial_818 = particlePage is { } page ? page with { Blend = GameFont.Page.BlendAdd } : null; // Amedo
        var sprite = ParticleSprite_81C;
        sprite.SetMaterial_1ABDB0(ParticleMaterial_818, new Vector2(0.25f, 0f), new Vector2(0.5f, 0.25f)); // Amedo
        var scales = ParticleScales_844.Keys_04;
        scales[0] = new(0.5f, 0.5f);
        scales[1] = new(0.6f, 0.6f);
        scales[2] = new(0.8f, 0.8f);
        scales[3] = new(1.25f, 1.25f);
        scales[4] = new(1.5f, 1.5f);
        scales[5] = new(1.1f, 1.1f);
        scales[6] = new(0.6f, 0.6f);
        scales[7] = new(0.1f, 0.1f);
        var colours = ParticleColours_83C.Keys_04;
        colours[0] = new(0.2f, 0.2f, 0.2f, 0.1f);
        colours[1] = new(0.6f, 0.6f, 0.6f, 0.5f);
        colours[2] = new(0.8f, 0.8f, 0.8f, 0.9f);
        colours[3] = new(1f, 1f, 1f, 1f);
        colours[4] = new(1f, 1f, 1f, 1f);
        colours[5] = new(0.8f, 0.8f, 0.8f, 0.9f);
        colours[6] = new(0.4f, 0.4f, 0.4f, 0.5f);
        colours[7] = new(0.1f, 0.1f, 0.1f, 0.1f);
        ParticleTurn_84C.Keys_04[0] = 0f;
        ParticleTurn_84C.Keys_04[1] = BitConverter.Int32BitsToSingle(0x3F860A92);
        ParticleSpin_854.Keys_04[0] = 0f;
        ParticleSpin_854.Keys_04[1] = BitConverter.Int32BitsToSingle(0x40C90FDB);
        var first = Emitters_85C[0];
        first.SetSprite_1AB5E8(sprite, 6);
        first.Chance_34 = 0.03f;
        first.Life_30 = 0.5f;
        first.Spread_1C = new(0.65f, 0.75f);
        first.Size_14 = new(0.05f, 0.05f);
        first.Turns_44 = ParticleTurn_84C;
        first.Colours_3C = ParticleColours_83C;
        first.Scales_40 = ParticleScales_844;
        for (int i = 1; i < GemEmitters; i++)
        {
            var e = Emitters_85C[i];
            e.SetSprite_1AB5E8(sprite, 4);
            e.Life_30 = 0.45f;
            e.Chance_34 = 0.023f;
            e.Size_14 = new(0.05f, 0.05f);
            e.Spread_1C = new(0.5f, 0.5f);
            e.Colours_3C = ParticleColours_83C;
            e.Scales_40 = ParticleScales_844;
            e.Turns_44 = ParticleTurn_84C;
        }
        var lives = Emitters_85C[LivesEmitter];
        lives.SetSprite_1AB5E8(sprite, 2);
        lives.Life_30 = 0.4f;
        lives.Chance_34 = 0.001f;
        lives.Size_14 = new(0.05f, 0.05f);
        lives.Spread_1C = new(0.33f, 0.25f);
        lives.Turns_44 = ParticleTurn_84C;
        lives.Colours_3C = ParticleColours_83C;
        lives.Scales_40 = ParticleScales_844;
        lives.Centre_24 = new(-0.01f, 0.04f);
        var radial = Sparkle_AC0.Emitter_20;
        radial.SetSprite_1AB5E8(sprite, 0x80);
        radial.Size_14 = new(0.052f, 0.052f);
        radial.Spread_1C = new(0.5f, 0.5f);
        radial.Turns_44 = ParticleSpin_854;
        radial.Colours_3C = ParticleColours_83C;
        radial.Scales_40 = ParticleScales_844;
        Sparkle_AC0.Duration_00 = 1.6f;
        radial.Colour_50 = new(0f, 1f, 0f, 1f);
    }

    public uint Picture_314 = 1u << 1 | 1u << 9; // Amedo
    public string LanguageName_309758 = "American"; // Amedo
    public string? CustomPicture_398; // Amedo
    public Func<string, IReadOnlyList<GameFont.Page?>?>? ReadPicture; // Amedo

    private static readonly string[] LevelPictures_2E6CC8 =
    [
        "Hub01", "Level01", "Level01", "Level02", "Level03", "Hub01", "Hub02", "Level04", "Level04", "Level05", "Level06", "Level06", "Hub02",
        "Hub03", "Level07", "Level08", "Level08", "Level09", "Level10", "Hub03", "Hub04", "Level11", "Level12", "Level13", "Hub04",
    ];
    private static readonly string?[] Pictures_2E6D30 =
    [
        "Legal", @"Loading\Loading1", @"Loading\Loading2", @"Loading\Loading3", @"GameOver\Cortex", @"GameOver\Crash", @"GameOver\CrashAndCortex",
        @"GameOver\Mecha", @"GameOver\Nina", null, null, @"Credits\CreditNew",
    ];
    private const string PictureExtension_309610 = ".psm";

    private uint PictureWord(int kind) => kind switch { 0 => Picture_314, 1 => (uint)Picture_318, _ => (uint)(Picture_318 >> 32) };

    private void SetPictureWord(int kind, uint w)
    {
        if (kind == 0) Picture_314 = w;
        else if (kind == 1) Picture_318 = (Picture_318 & 0xFFFFFFFF00000000ul) | w;
        else Picture_318 = (Picture_318 & 0xFFFFFFFFul) | ((ulong)w << 32);
    }

    // Amedo
    public void ContinueFrom(Oleg old, int clockShift)
    {
        Fader_13B8.HiddenColour_40 = old.Fader_13B8.HiddenColour_40;
        Fader_13B8.ShownColour_44 = old.Fader_13B8.ShownColour_44;
        for (int kind = 0; kind < 3; kind++)
        {
            uint was = old.PictureWord(kind);
            SetPictureWord(kind, (PictureWord(kind) & 0xFFFE01FFu) | (was & 0x1FE00u));
            if (((was >> 1) & 0xFF) == ((was >> 9) & 0xFF)) LoadPicture_170ED0(kind, false);
        }
        CopyStatesFrom(old, clockShift);
    }

    // Amedo
    public void ReleasePicture_170D78(int kind)
    {
        switch (kind)
        {
            case 1:
                if (((PictureWord(1) >> 1) & 0xFF) == 25) return;
                Sprites_B4C[2].SetMaterial_1ABD50(null);
                SetPictureWord(1, (PictureWord(1) & 0xFFFFFE01u) | 0x32);
                break;
            case 0:
                if (((PictureWord(0) >> 1) & 0xFF) == 1) return;
                Sprites_B4C[1].SetMaterial_1ABD50(null);
                SetPictureWord(0, (PictureWord(0) & 0xFFFFFE01u) | 2);
                break;
            case 2:
                if (((PictureWord(2) >> 1) & 0xFF) == 0xC) return;
                foreach (var picture in Pictures_110C) picture.SetMaterial_1ABD50(null);
                SetPictureWord(2, (PictureWord(2) & 0xFFFFFE01u) | 0x18);
                break;
        }
    }

    // Amedo
    public void LoadPicture_170ED0(int kind, bool wait)
    {
        uint w = PictureWord(kind);
        if (((w >> 1) & 0xFF) == ((w >> 9) & 0xFF)) return;
        string path;
        switch (kind)
        {
            case 1:
                path = @"Language\Titles\" + LanguageName_309758 + @"\" + LevelPictures_2E6CC8[(int)((PictureWord(1) >> 9) & 0xFF)];
                break;
            case 0:
                path = @"Language\Titles\" + LanguageName_309758 + @"\Crash";
                break;
            case 2:
            {
                int i = (int)((PictureWord(2) >> 9) & 0xFF);
                if (i == 0) path = @"Language\" + Pictures_2E6D30[0] + @"\" + LanguageName_309758;
                else if (i is 9 or 10) path = CustomPicture_398 ?? "";
                else path = @"Language\" + Pictures_2E6D30[i];
                break;
            }
            default:
                path = "";
                break;
        }
        path += PictureExtension_309610;
        ReleasePicture_170D78(kind);
        w = PictureWord(kind);
        SetPictureWord(kind, (w & 0xFFFFFE01u) | ((w >> 8) & 0x1FE) | 1);
        if (ReadPicture?.Invoke(path) is { } pages) PictureLoaded_179D80(kind, pages);
    }

    // Amedo
    public void Draw_17A0B8(OverlayQueue overlay)
    {
        Draw_259E28(overlay);
        TextLine_120C.Draw_25ADA0(overlay);
    }
}
