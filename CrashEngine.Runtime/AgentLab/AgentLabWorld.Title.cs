using CrashEngine.Runtime.Screen.Oleg;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabWorld
{
    public const int ModeBoot = 0, ModeLogo = 1, ModeWaitingForPad = 2, ModeCheckingCard = 3, ModeIntroMovie = 4, ModeIdentMovie = 5;
    public const int ModeTitle = 0x7, ModeMainMenu = 0x8, ModeNewGame = 0xA;
    public const string DiscTitleChunk = @"Levels\Earth\Hub\Beach";
    public string TitleChunk_30BB00 { get; private set; } = DiscTitleChunk;
    public const int IntroMovie = 0x14, IdentMovie = 0, AttractMovie = 0xD, FirstGameMovie = 1;

    public int StreamState_309738 { get; set; } // Amedo
    public int GcDtTicks { get; private set; } // Amedo
    public const string CreditsChunk_30BB10 = @"Levels\Ice\Hub\LabExt";
    public static readonly int[] CreditsMusic_2F5418 = [0x3A, 0x1C, 0x88, 0x1E, 0x23, 0x25, 0x29, 0x36, 0x3C, 0x3D, 0x1B];
    public const int CreditsPicture = 11, PressStartText = 0x46;
    public CreditsScroller? Credits_4F50;
    private int _creditsTrack_7E08;
    private float _creditsTimer_7E04;
    private bool _reportedLevelSwitch;

    // Amedo
    public void BootToTitle(string? startChunk = null)
    {
        Word_00 = (Word_00 & ~((0x3Ful << 44) | (0x3Ful << 38))) | ((ulong)ModeBoot << 44) | ((ulong)ModeBoot << 38);
        _modeEnteredFrame = Frame;
        _snapshotTaken = true;
        StartChunk_500 = startChunk ?? "";
        TitleChunk_30BB00 = RootChunk.Name;
        WorldShown = false; // Amedo
        Log?.Invoke($"[mode {Now:0.00}s] boot (state 0){(startChunk is null ? " to the title" : " straight into " + startChunk)}");
    }

    // Amedo
    private int Boot_1772C8()
    {
        if (StreamState_309738 == 1) return ModeExitCutscene;
        PauseChunkClocks_1918A8();
        PauseRun_181400();
        return ModeLogo;
    }

    // Amedo
    private int Logo_173AB0()
    {
        if (Frame == _modeEnteredFrame)
        {
            uint high = (uint)(Oleg.Picture_318 >> 32) & 0xFFFE01FFu;
            Oleg.Picture_318 = (Oleg.Picture_318 & 0xFFFFFFFFul) | ((ulong)high << 32);
            return -1;
        }
        if (!ShowPictureScreen_172D88(Oleg.PictureGroup)) return -1;
        if (Frame != PictureHold_14) return ModeWaitingForPad;
        LoadLogo_173B60();
        return -1;
    }

    // Amedo
    public void LoadLogo_173B60()
    {
        Oleg.Picture_314 &= 0xFFFE01FFu;
        Oleg.LoadPicture_170ED0(0, false);
    }

    // Amedo
    private int WaitingForPad_173BA0()
    {
        if (Pad1 is not null) return ModeCheckingCard;
        if (Frame == _modeEnteredFrame)
        {
            Oleg.Hide_259BC0(ulong.MaxValue, Seconds(0.1f), 0);
            Oleg.Show_259B20(Oleg.Groups_08[Oleg.NoPadGroup], Seconds(0.25f), 0);
        }
        return -1;
    }

    // Amedo
    private int CheckingCard_173C58()
    {
        var manager = SaveManager_309724;
        if (Frame == _modeEnteredFrame)
        {
            Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.PictureGroup], Seconds(0.5f), 0);
            manager?.Request_2A11D0(1, Seconds(2f));
        }
        return manager is { Busy: true } ? -1 : ModeIntroMovie;
    }

    // Amedo
    private bool PlayMovie_172B60(int fadeTicks, int movie, bool allowSkip = true)
    {
        var player = MoviePlayer_309720;
        int hold = PictureHold_14;
        PictureHold_14 = 0;
        if (player is null)
        {
            MovieSkipped = false;
            return true;
        }
        if (Frame == _modeEnteredFrame)
        {
            Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.AllButOverlaysGroup], fadeTicks, 0);
            Oleg.Show_259B20(Oleg.Groups_08[Oleg.FlashGroup], fadeTicks, 0);
            return false;
        }
        if (!(fadeTicks + Seconds(0.1f) < GcNowTicks - _modeEnteredTicks)) return false;
        if (hold == 0)
        {
            var (name, flags) = MovieTable_2E6D60[movie];
            int track = ((flags >> 1) & 7) != 1 ? AudioTrack_309754 : 0;
            PictureHold_14 = Frame;
            Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.FlashGroup], fadeTicks, 0);
            if (player.Start_2AFB18(name, track, (flags & 1) != 0, (int)((flags >> 4) & 0xFFF), (int)((flags >> 16) & 0xFFF))) return false;
            MovieSkipped = false;
            Oleg.Show_259B20(Oleg.Groups_08[Oleg.FlashGroup], 0, 0);
            return true;
        }
        PictureHold_14 = hold;
        if (!player.Playing)
        {
            MovieSkipped = false;
            Oleg.Show_259B20(Oleg.Groups_08[Oleg.FlashGroup], 0, 0);
            return true;
        }
        if (!allowSkip || !(Pad1?.GetButtonState_2B3548(CrossButton, true) ?? false)) return false;
        MovieSkipped = true;
        Oleg.Show_259B20(Oleg.Groups_08[Oleg.FlashGroup], 0, 0);
        player.Stop_2AFBC8();
        return true;
    }

    public Video.MoviePlayer? MoviePlayer_309720 { get; set; } // Amedo
    public bool MovieSkipped { get; private set; } // Amedo
    public int AudioTrack_309754 { get; set; } // Amedo

    // Amedo
    public static readonly (string Name, uint Flags)[] MovieTable_2E6D60 =
    [
        (@"FMV\TTIdent", Movie(false, 512, 448)), (@"FMV\H01_a", Movie(true, 512, 288)), (@"FMV\H01_f", Movie(true, 512, 288)),
        (@"FMV\B01_a", Movie(true, 512, 288)), (@"FMV\B01_b", Movie(true, 512, 288)), (@"FMV\H02_a", Movie(true, 512, 288)),
        (@"FMV\H02_b", Movie(true, 512, 288)), (@"FMV\H02_ka", Movie(true, 512, 288)), (@"FMV\H03_f", Movie(true, 512, 288)),
        (@"FMV\H02_n", Movie(true, 512, 288)), (@"FMV\B04_a", Movie(true, 512, 288)), (@"FMV\H04_da", Movie(true, 512, 288)),
        (@"FMV\H04_db", Movie(true, 512, 288)), (@"FMV\Attract", Movie(false, 512, 448)), (@"FMV\Bonus\Bonus1", Movie(false, 640, 480)),
        (@"FMV\Bonus\Bonus2", Movie(false, 640, 480)), (@"FMV\Bonus\Bonus3", Movie(false, 640, 480)), (@"FMV\Bonus\Bonus4", Movie(false, 640, 480)),
        (@"FMV\Complete", Movie(false, 640, 480)), (@"FMV\Spyro", Movie(false, 512, 448)), (@"FMV\Vivendi", Movie(false, 640, 480)),
    ];

    private static uint Movie(bool wide, int width, int height) => (wide ? 1u : 0u) | 1u << 1 | (uint)width << 4 | (uint)height << 16; // Amedo

    // Amedo
    private int IntroMovie_177394() => PlayMovie_172B60(Seconds(0.5f), IntroMovie) ? ModeIdentMovie : -1;

    // Amedo
    private int IdentMovie_1773E0()
    {
        if (!PlayMovie_172B60(0, IdentMovie)) return -1;
        if (StartChunk_500.Length <= 0) return ModeLoadingTitle;
        Word_00 = (Word_00 & 0xFFFFFFFFF87FFFFFul) | 0x01000000ul;
        SaveController_594.Take_168810(Progress, CurrentChunk_309738, SaveChunkStates_2686B0, null, GcNowTicks, this);
        return ModeLoadingLevel;
    }

    // Amedo
    public bool WorldShown { get; private set; } = true; // Amedo

    private void StreamLoadStartChunk_2AA3E8(int kind)
    {
        WorldShown = true; // Amedo
        if (string.Equals(Streaming.StreamLink.Normalize(StartChunk_500), RootChunk.Name, StringComparison.OrdinalIgnoreCase))
        {
            ResetKind = kind;
            _sceneResetPending = true;
            Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] stream load {StartChunk_500} (= this level: scene rebuild)");
            return;
        }
        if (LevelSwitch is not null) // Amedo
        {
            if (_reportedLevelSwitch) return;
            _reportedLevelSwitch = true;
            ResetKind = kind;
            _levelSwitchPending = true;
            Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] stream load {StartChunk_500} (another level, kind {kind}: the host opens it)");
            LevelSwitch(StartChunk_500, kind);
            return;
        }
        if (_reportedLevelSwitch) return;
        _reportedLevelSwitch = true;
        Unimplemented("mode", 0x2AA3E8, $"stream load of another level ({StartChunk_500}, we are on {RootChunk.Name}) = U7 level switch");
    }

    // Amedo
    public Action<string, int>? LevelSwitch;

    // Amedo
    public void ContinueLevelSwitch(AgentLabWorld from)
    {
        const ulong states = (0x3Ful << 50) | (0x3Ful << 44) | (0x3Ful << 38);
        Word_00 = (from.Word_00 & ~states) | ((ulong)NoMode << 50) | ((ulong)ModeLoadingLevel << 44) | ((ulong)ModeLoadingLevel << 38);
        GalleryWord_08 = from.GalleryWord_08;
        TitleChunk_30BB00 = from.TitleChunk_30BB00;
        AutosaveTime_20 = from.AutosaveTime_20;
        AutosavePauses_8F0 = from.AutosavePauses_8F0;
        Progress.CopyFrom(from.Progress);
        SaveController_594.CopyFrom(from.SaveController_594);
        if (SaveManager_309724 is { } manager && from.SaveManager_309724 is { } old) manager.CopyStateFrom(old); // Amedo
        if (AutosaveState == 2)
        {
            SetAutosaveState(1);
            AutosaveTime_20 = 0;
        }
        Oleg.ContinueFrom(from.Oleg, GcNowTicks - from.GcNowTicks); // Amedo
        PictureHold_14 = 0;
        _modeEnteredFrame = Frame;
        _snapshotTaken = true;
        PauseChunkClocks_1918A8();
        PauseRun_181400();
        Log?.Invoke($"[mode {Now:0.00}s] level switch: continuing the load of {StartChunk_500} (kind {RestartKind}, autosave {SaveManager_309724?.Autosave})");
    }

    // Amedo
    private bool ChunkLoaded_1681F8(int mode) => !_sceneResetPending && !_levelSwitchPending; // Amedo
    private bool _levelSwitchPending; // Amedo

    // Amedo
    private int LoadingTitle_173CE8()
    {
        if (Frame == _modeEnteredFrame)
        {
            ulong w = Word_00;
            PauseChunkClocks_1918A8();
            PauseRun_181400();
            if (((w >> 38) & 0x3F) == ModeMovie)
            {
                ResetGame_173040(0);
                return ModeTitle;
            }
            StartChunk_500 = TitleChunk_30BB00;
            float r = Rng.GetRandFloat_18C0B8();
            int picture = 3;
            if (r < 1f) picture = GameMath.Trunc(r * 3f) + 1;
            uint high = (uint)(Oleg.Picture_318 >> 32);
            high = (high & 0xFFFE01FFu) | (((uint)picture & 0xFF) << 9);
            Oleg.Picture_318 = (Oleg.Picture_318 & 0xFFFFFFFFul) | ((ulong)high << 32);
            return -1;
        }
        if (!ShowPictureScreen_172D88(Oleg.LoadingGroup)) return -1;
        if (Frame == PictureHold_14)
        {
            Oleg.Reset_179E60();
            if (StartChunk_500.Length != 0) StreamLoadStartChunk_2AA3E8(0);
            return -1;
        }
        int mode = (GalleryWord_08 & 1) == 0 ? 1 : 2;
        return ChunkLoaded_1681F8(mode) ? ModeTitle : -1;
    }

    // Amedo
    private int Title_173F38()
    {
        if (Frame == _modeEnteredFrame)
        {
            for (int i = 0; i < 6; i++) DisableCharacter_177F28(i);
            Oleg.Hide_259BC0(ulong.MaxValue, Seconds(0.5f), 0);
            Progress.Reset_167D38(0, GcNowTicks);
            ChooseRespawn_167D38(0);
            SaveController_594.Take_168810(Progress, CurrentChunk_309738, SaveChunkStates_2686B0, null, GcNowTicks, this);
            if (!(SwitchCharacter_177A90?.Invoke(4, true) ?? false)) EnableCharacters_17C0C8?.Invoke();
            ResumeChunkClocks_1917E0();
            ResumeRun_181438();
            Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] title");
            return -1;
        }
        if (Pad1?.GetButtonState_2B3548(StartButton, true) ?? false) return ModeMainMenu;
        if (GcNowTicks - _modeEnteredTicks < Seconds(50f)) return -1;
        RequestRandomMovie_17B8B0(Seconds(0.5f), AttractMovie, AttractMovie);
        return -1;
    }

    // Amedo
    public bool RequestRandomMovie_17B8B0(int fadeTicks, int first, int last)
    {
        float r = Rng.GetRandFloat_18C0B8();
        if (r < 1f) last = first + GameMath.Trunc((last - first + 1) * r);
        RequestMovie_17B868(fadeTicks, last);
        return true;
    }

    // Amedo
    private int MainMenu_174108()
    {
        if (Frame == _modeEnteredFrame)
        {
            Oleg.Hide_259BC0(ulong.MaxValue, Seconds(0.25f), 0);
            Oleg.Show_259B20(Oleg.Groups_08[Oleg.MainMenuGroup], Seconds(0.25f), 0);
            PauseChunkClocks_1918A8();
            PauseRun_181400();
            Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] main menu");
            return -1;
        }
        var menu = Oleg.ShownMenu_171740();
        bool exit = menu is not null && (menu.Flags_88 & MenuWidget.AllowStart) != 0 && ReferenceEquals(menu.Current_A4, MenuPage.Exit_3B6B50);
        if (exit) return ModeTitle;
        UpdateSaving_173238();
        return -1;
    }

    // Amedo
    public bool NewGame_17BEB0()
    {
        Word_00 = (Word_00 & 0x80FFFFFFFFFFFFFFul) | 0x2800000000000000ul;
        SaveManager_309724?.Request_2A11D0(3, Seconds(2f));
        Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] new game (request 3)");
        return true;
    }

    // Amedo
    private int NewGame_174478()
    {
        if (Frame == _modeEnteredFrame)
        {
            Oleg.Fader_13B8.HiddenColour_40 = Colour.GetColor_11BF10(0);
            Oleg.Fader_13B8.ShownColour_44 = Colour.GetColor_11BF10(8);
            int half = Seconds(0.5f);
            Oleg.Hide_259BC0(ulong.MaxValue, half, 0);
            Oleg.Show_259B20(Oleg.Groups_08[Oleg.FlashGroup], half, 0);
            return -1;
        }
        int index = WidgetController.IndexOf_259AF0(Oleg.Groups_08[Oleg.FlashGroup]);
        if (index < 0 || Oleg.Widgets_208[index]?.Phase != 3) return -1;
        SaveController_594.Restore_1685C0(false, Progress, RestoreChunkStates_263B38, GcNowTicks, this);
        ResetGame_173040(0);
        RequestMovie_17B868(Seconds(0.5f), FirstGameMovie);
        Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] new game started");
        return -1;
    }

    // Amedo
    private int Credits_175160()
    {
        if (Frame == _modeEnteredFrame)
        {
            uint high = (uint)(Oleg.Picture_318 >> 32);
            high = (high & 0xFFFE01FFu) | ((uint)CreditsPicture << 9);
            Oleg.Picture_318 = (Oleg.Picture_318 & 0xFFFFFFFFul) | ((ulong)high << 32);
            PauseChunkClocks_1918A8();
            PauseRun_181400();
            _creditsTrack_7E08 = 0;
            _creditsTimer_7E04 = 19f;
            return -1;
        }
        if (!ShowPictureScreen_172D88(Oleg.PictureGroup)) return -1;
        if (Frame == PictureHold_14)
        {
            Credits_4F50 = new CreditsScroller(Oleg.Font_394, Screen?.ReadCredits() ?? "");
            Oleg.Reset_179E60();
            StartChunk_500 = CreditsChunk_30BB10;
            if (StartChunk_500.Length != 0) StreamLoadStartChunk_2AA3E8(0);
            Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] credits ({Credits_4F50.LineCount} lines)");
            return -1;
        }
        if (Credits_4F50 is null || !Credits_4F50.Update_25C540(GcDtTicks))
        {
            int mode = (GalleryWord_08 & 1) == 0 ? 1 : 2;
            if (!ChunkLoaded_1681F8(mode)) return -1;
            Credits_4F50 = null;
            return ModeExitCutscene;
        }
        if (19f < _creditsTimer_7E04)
        {
            if (_creditsTrack_7E08 < CreditsMusic_2F5418.Length)
            {
                _creditsTimer_7E04 = 0f;
                Music?.Begin(0, CreditsMusic_2F5418[_creditsTrack_7E08], 1f, 1f, _creditsTrack_7E08 == 0 ? 0f : 3f, "credits");
                _creditsTrack_7E08++;
            }
        }
        else _creditsTimer_7E04 += GcDtTicks * Widget.SecondsPerTick;
        return -1;
    }

    // Amedo
    public void Draw_177908(OverlayQueue overlay)
    {
        if (GameMode == ModeCredits) Credits_4F50?.Draw_25C5F0(overlay);
        else if (GameMode == ModeTitle && !BottomShown && _modeEnteredFrame + 0x32 < Frame)
        {
            overlay.Font_28 = Oleg.Font_394;
            overlay.Colour_10 = Colour.GetColor_11BF10(15);
            float size = CyclingScale.Global_30BB90.Value_10 * 0.75f;
            overlay.Scale_2C = new System.Numerics.Vector2(size, size);
            overlay.Flags_34 = 0x22;
            overlay.QueueText_1A03A0(Oleg.CodeTexts_309770?[PressStartText] ?? "", new System.Numerics.Vector2(0.5f, BitConverter.Int32BitsToSingle(0x3F666666)));
        }
        Oleg.Draw_17A0B8(overlay);
    }
}
