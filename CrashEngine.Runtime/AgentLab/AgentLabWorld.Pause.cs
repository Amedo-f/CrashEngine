using CrashEngine.Runtime.Screen.Oleg;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabWorld : IOlegGame, Game.ISaveOptions
{
    public const int TriangleButton = 4, StartButton = 0x16, L1Button = 0x10, L2Button = 0x11, R1Button = 0x13, R2Button = 0x14;

    public int RunState_309718 { get; private set; } = 1;
    public bool ChunkClocksRunning { get; private set; } = true;
    private long _gcClockTicks;
    public int GcNowTicks => (int)_gcClockTicks;
    public uint PadFlags_30971C { get; private set; } = 0x200; // Amedo
    public System.Numerics.Vector2 ScreenOffset_309728 // Amedo
    {
        get => _screenOffset_14;
        set => _screenOffset_14 = System.Numerics.Vector2.Clamp(value, -System.Numerics.Vector2.One, System.Numerics.Vector2.One);
    }
    private System.Numerics.Vector2 _screenOffset_14; // Amedo

    // Amedo
    public bool Vibration_30971C
    {
        get => (PadFlags_30971C & 0x200) != 0;
        set
        {
            if (value) PadFlags_30971C |= 0x200;
            else DisableVibration_2B37A8();
        }
    }

    // Amedo
    private void DisableVibration_2B37A8() => PadFlags_30971C &= ~0x200u;

    // Amedo
    public float SoundLevel_1E60A8(int group) => Sounds?.Groups.Level_1E60A8(group) ?? 1f;

    // Amedo
    public void SetSoundLevel_1E5FE8(float level, int group) => Sounds?.Groups.SetLevel_1E5FE8(group, level);

    public int OutputMode_309AC8 => Sounds?.OutputMode_309AC8 ?? 1; // Amedo
    public bool Widescreen_30A744 { get => OverlayQueue.Widescreen_30A744; set => OverlayQueue.Widescreen_30A744 = value; } // Amedo
    public readonly Game.SaveController SaveController_594 = new(); // Amedo
    public string StartChunk_500 { get => Progress.StartChunk_10; set => Progress.StartChunk_10 = value; } // Amedo
    public string? CurrentChunk_309738 => RootChunk?.Name; // Amedo
    public Game.SaveManager? SaveManager_309724 { get; private set; } // Amedo
    public string? SavesDirectory { get; set; } // Amedo
    public int AutosaveTime_20, AutosavePauses_8F0; // Amedo
    private bool _snapshotTaken;

    // Amedo
    private void TakeSnapshot()
    {
        if (_snapshotTaken) return;
        _snapshotTaken = true;
        SaveController_594.Take_168810(Progress, CurrentChunk_309738, SaveChunkStates_2686B0, null, GcNowTicks, this);
    }

    // Amedo
    public void SetOutputMode_1E6190(int mode) => Sounds?.SetOutputMode_1E6190(mode);

    // Amedo
    private void PauseChunkClocks_1918A8() => ChunkClocksRunning = false;

    // Amedo
    private void ResumeChunkClocks_1917E0() => ChunkClocksRunning = true;

    // Amedo
    private void PauseRun_181400()
    {
        if (RunState_309718 == 1) RunState_309718 = 2;
        else if (RunState_309718 == 4) RunState_309718 = 3;
    }

    // Amedo
    private void ResumeRun_181438()
    {
        if (RunState_309718 == 2) RunState_309718 = 1;
        else if (RunState_309718 == 3) RunState_309718 = 4;
    }

    // Amedo
    private void StepRunState_17D298()
    {
        if (RunState_309718 == 2)
        {
            Sounds?.PauseAll_1E5E50();
            RunState_309718 = 3;
            Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] run state 2 -> 3 (sounds paused, world held)");
        }
        else if (RunState_309718 == 4)
        {
            Sounds?.ResumeAll_1E5E70();
            RunState_309718 = 1;
            Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] run state 4 -> 1 (sounds resumed)");
        }
    }

    // Amedo
    private void SetAutosaveState(int state) => Word_00 = (Word_00 & 0x87FFFFFFFFFFFFFFul) | ((ulong)(state & 0xF) << 59);

    // Amedo
    private void AutosaveOff()
    {
        SetAutosaveState(0);
        if (SaveManager_309724 is { Autosave: true } manager) manager.Autosave = false;
    }

    // Amedo
    private bool UpdateSaving_173238()
    {
        var manager = SaveManager_309724;
        int state = AutosaveState;
        if (state == 1)
        {
            if (AutosaveTime_20 == 0) AutosaveTime_20 = GcNowTicks;
            else if (Seconds(0.25f) <= GcNowTicks - AutosaveTime_20)
            {
                manager?.Request_2A11D0(2, 0);
                SetAutosaveState(2);
            }
            return false;
        }
        if (manager is null || manager.Busy) return false;
        int quarter = Seconds(0.25f);
        switch (state)
        {
            case 0:
                return false;
            case 2:
                if (manager.IsDone)
                {
                    SetAutosaveState(1);
                    AutosaveTime_20 = GcNowTicks;
                    return false;
                }
                Word_00 = (Word_00 & 0xF9FFFFFFFFFFFFFFul) | 0x0100000000000000ul;
                AutosaveOff();
                Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] autosave check failed -> autosave disabled");
                return true;
            case 3:
                Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.AutosaveGroup], quarter, 0);
                if (AutosavePauses_8F0 == 1)
                {
                    Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.DimmerGroup], quarter, 0);
                    ResumeChunkClocks_1917E0();
                    ResumeRun_181438();
                }
                Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] autosave finished (done {manager.IsDone}, saved {manager.Succeeded})");
                if (manager.IsDone)
                {
                    SetAutosaveState(1);
                    AutosaveTime_20 = GcNowTicks;
                    return false;
                }
                Word_00 = (Word_00 & 0xFCFFFFFFFFFFFFFFul) | 0x0400000000000000ul;
                AutosaveOff();
                return true;
            case 4:
                if (manager.IsDone)
                {
                    Word_00 = (Word_00 & 0xFAFFFFFFFFFFFFFFul) | 0x0200000000000000ul;
                    LoadedGame_176090(true);
                    return false;
                }
                if (manager.Autosave)
                {
                    Word_00 = (Word_00 & 0x81FFFFFFFFFFFFFFul) | 0x0100000000000000ul;
                    manager.Autosave = false;
                }
                else SetAutosaveState(0);
                Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.AllButOverlaysGroup], quarter, 0);
                Oleg.Show_259B20(Oleg.Groups_08[GameMode == Oleg.MainMenuMode ? Oleg.MainMenuGroup : Oleg.PauseGroup], quarter, 0);
                return false;
            case 5:
                if (manager.IsDone)
                {
                    NewGameChosen_175E20(true);
                    return false;
                }
                Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.AllButOverlaysGroup], quarter, 0);
                Oleg.Show_259B20(Oleg.Groups_08[Oleg.MainMenuGroup], quarter, 0);
                AutosaveOff();
                return false;
            case 6:
                if (!manager.IsDone)
                {
                    if (manager.Autosave)
                    {
                        Word_00 = (Word_00 & 0x81FFFFFFFFFFFFFFul) | 0x0100000000000000ul;
                        manager.Autosave = false;
                    }
                    else SetAutosaveState(0);
                }
                else
                {
                    ulong w = Word_00;
                    Word_00 = (w & 0xFAFFFFFFFFFFFFFFul) | 0x0200000000000000ul;
                    if (!manager.Busy)
                    {
                        if (!manager.Succeeded) AutosaveOff_17B520();
                        else
                        {
                            Word_00 = (w & 0x82FFFFFFFFFFFFFFul) | 0x0A00000000000000ul;
                            manager.Autosave = true;
                            AutosaveTime_20 = 0;
                        }
                    }
                }
                Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.AllButOverlaysGroup], quarter, 0);
                Oleg.Show_259B20(Oleg.Groups_08[Oleg.PauseGroup], quarter, 0);
                Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] save from pause finished (done {manager.IsDone}, saved {manager.Succeeded}) -> autosave state {AutosaveState}");
                return false;
            default:
                return true;
        }
    }

    // Amedo
    private bool AutosaveOff_17B520()
    {
        bool on = SaveManager_309724?.Autosave ?? false;
        SetAutosaveState(0);
        if (on) SaveManager_309724!.Autosave = false;
        return on;
    }

    // Amedo
    private bool LoadedGame_176090(bool loaded)
    {
        if (GameMode == ModeLoadingLevel) return false;
        var manager = SaveManager_309724!;
        if (!loaded)
        {
            Progress.Reset_167D38(0, GcNowTicks);
            ChooseRespawn_167D38(0);
            SaveController_594.Take_168810(Progress, CurrentChunk_309738, SaveChunkStates_2686B0, null, GcNowTicks, this);
            AutosaveOff();
        }
        else
        {
            StartChunk_500 = SaveController_594.Name_0C;
            if (!manager.Busy)
            {
                if (!manager.Succeeded) AutosaveOff_17B520();
                else
                {
                    SetAutosaveState(1);
                    manager.Autosave = true;
                    AutosaveTime_20 = 0;
                }
            }
        }
        Word_00 = (Word_00 & 0xFF03FFFFF87FFFFFul) | 0x0024000001000000ul;
        Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] game loaded ({loaded}) -> loading level {StartChunk_500} (kind 2)");
        return true;
    }

    // Amedo
    private bool NewGameChosen_175E20(bool chosen)
    {
        if (GameMode == 0xA) return false;
        var manager = SaveManager_309724!;
        ulong w = Word_00;
        if (!chosen)
        {
            Word_00 = (w & 0xF9FFFFFFFFFFFFFFul) | 0x0100000000000000ul;
            if (manager.Autosave)
            {
                Word_00 = (w & 0x81FFFFFFFFFFFFFFul) | 0x0100000000000000ul;
                manager.Autosave = false;
            }
            else Word_00 = (w & 0x81FFFFFFFFFFFFFFul) | 0x0100000000000000ul;
        }
        else
        {
            bool saved = false;
            if (!manager.Busy)
            {
                saved = true;
                if (!manager.Succeeded)
                {
                    AutosaveOff_17B520();
                    saved = false;
                }
                else
                {
                    Word_00 = (w & 0x87FFFFFFFFFFFFFFul) | 0x0800000000000000ul;
                    manager.Autosave = true;
                    AutosaveTime_20 = 0;
                }
            }
            if (saved)
            {
                AutosavePauses_8F0 = 0;
                Word_00 = (Word_00 & 0xFAFFFFFFFFFFFFFFul) | 0x0200000000000000ul;
            }
            else Word_00 = (Word_00 & 0xF9FFFFFFFFFFFFFFul) | 0x0100000000000000ul;
        }
        Word_00 = (Word_00 & 0xFF03FFFFFFFFFFFFul) | 0x0028000000000000ul;
        return true;
    }

    // Amedo
    public bool Autosave_176F28(ushort? checkpoint)
    {
        var manager = SaveManager_309724!;
        SaveController_594.Take_168810(Progress, CurrentChunk_309738, SaveChunkStates_2686B0, checkpoint, GcNowTicks, this);
        if (!manager.Autosave) return false;
        SetAutosaveState(3);
        AutosavePauses_8F0++;
        int quarter = Seconds(0.25f);
        Oleg.Show_259B20(Oleg.Groups_08[Oleg.AutosaveGroup], quarter, 0);
        manager.Request_2A11D0(6, Seconds(2f));
        if (AutosavePauses_8F0 == 1)
        {
            Oleg.Show_259B20(Oleg.Groups_08[Oleg.DimmerGroup], quarter, 0);
            PauseChunkClocks_1918A8();
            PauseRun_181400();
        }
        Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] autosave (checkpoint {checkpoint?.ToString() ?? "none"})");
        return true;
    }

    // Amedo
    private int Playing_174A70()
    {
        if (PlayerSlot_570(4) is { Asleep: false } slot4)
        {
            Log?.Invoke($"[mode {Now:0.00}s] Playing: slot 4 {slot4.Entity.Name} removed (inst+0xA4 vt+0x1C)");
            slot4.Sleep_197508();
        }
        if (Frame == _modeEnteredFrame)
        {
            ResumeChunkClocks_1917E0();
            ResumeRun_181438();
            return -1;
        }
        if (PauseReason_176698(false) != 0) return ModePaused;
        if (BottomHideTicks == 0 || GcNowTicks - _modeEnteredTicks < BottomHideTicks) return -1;
        int quarter = Seconds(0.25f);
        switch (HudMode)
        {
            case 1:
                Oleg.Show_259B20(Oleg.Groups_08[Oleg.BossGroup], quarter, 0);
                break;
            case 2:
                if (!BottomShown) Oleg.Show_259B20(Oleg.Groups_08[Oleg.WormGroup], quarter, 0);
                break;
            default:
                Oleg.Show_259B20(Oleg.Groups_08[Oleg.BothCountersGroup], quarter, Seconds(3f));
                Oleg.Show_259B20(Oleg.Groups_08[Oleg.SliderGroup], quarter, 0);
                if (!BottomShown) Oleg.Show_259B20(Oleg.Groups_08[Oleg.AmmoGroup], quarter, 0);
                break;
        }
        BottomHideTicks = 0;
        return -1;
    }

    public Func<int>? VideoState_30973C { get; set; } // Amedo
    public Action? VideoSkip_29AE00 { get; set; } // Amedo
    private bool _reportedVideoSkip;

    // Amedo
    private int Watching_174F38()
    {
        if (Frame == _modeEnteredFrame)
        {
            ResumeChunkClocks_1917E0();
            ResumeRun_181438();
            return -1;
        }
        if (PauseReason_176698(false) != 0) return ModePaused;
        bool cross = Pad1?.GetButtonState_2B3548(CrossButton, true) ?? false;
        if (!cross) return -1;
        if (VideoState_30973C is { } state)
        {
            if (state() == 3) VideoSkip_29AE00?.Invoke();
        }
        else if (!_reportedVideoSkip)
        {
            _reportedVideoSkip = true;
            Unimplemented("mode", 0x174FD0, "Watching: x skips the cutscene (VideoController [0x30973C] state 3 -> Skip 0x29AE00)");
        }
        return -1;
    }

    // Amedo
    private int Paused_174C70()
    {
        if (PauseReason_176698(true) == 0) return -1;
        var menu = Oleg.ShownMenu_171740();
        if (menu is null || (menu.Flags_88 & MenuWidget.AllowStart) == 0 || !ReferenceEquals(menu.Current_A4, MenuPage.Exit_3B6B50)) return -1;
        int quarter = Seconds(0.25f);
        Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.AllButOverlaysGroup], quarter, 0);
        if (SavedMode == ModeCutscene) Oleg.Show_259B20(Oleg.Groups_08[Oleg.CutsceneGroup], quarter, 0);
        else if (!BottomShown) BottomHideTicks = quarter;
        else Oleg.Show_259B20(Oleg.Groups_08[Oleg.BottomTextGroup], quarter, 0);
        Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] {MenuName(menu)} closed -> resuming 0x{SavedMode:X}");
        return ModeResuming;
    }

    private string MenuName(MenuWidget menu) => menu == Oleg.Menu_1E7C ? "pause menu" : menu == Oleg.Menu_2068 ? "quit menu" : menu == Oleg.Menu_1F28 ? "autosave menu" :
        menu == Oleg.Options_1A94 ? "options menu" : $"menu {Array.IndexOf(Oleg.Widgets_208, menu)}";

    // Amedo
    private int Resuming_1775E8() => GcNowTicks - _modeEnteredTicks < Seconds(0.25f) ? -1 : SavedMode;

    // Amedo
    private void ShowPauseScreen_176260()
    {
        int quarter = Seconds(0.25f);
        Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.AllButOverlaysGroup], quarter, 0);
        int group = PauseReason switch
        {
            1 => Oleg.PauseGroup,
            2 => 15,
            3 => 16,
            4 => 17,
            5 => 18,
            6 => 19,
            8 => 20,
            9 => 21,
            10 => 22,
            11 => 23,
            12 => 24,
            _ => -1,
        };
        if (group >= 0) Oleg.Show_259B20(Oleg.Groups_08[group], quarter, 0);
        Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] pause screen {PauseReason} (group {group})");
    }

    // Amedo
    private int PausePage_176470()
    {
        int reason = PauseReason;
        bool input = Oleg.Menu_1BD4.Phase != 3 && Oleg.Options_1A94.Phase != 3;
        bool left = false, right = false;
        if (input && Pad1 is { } pad)
        {
            left = pad.GetButtonState_2B3548(L1Button, true) || pad.GetButtonState_2B3548(L2Button, true);
            right = pad.GetButtonState_2B3548(R1Button, true) || pad.GetButtonState_2B3548(R2Button, true);
        }
        int furthest = (int)((Progress.Word_04 >> 26) & 0x1F);
        if (left)
        {
            switch (reason)
            {
                case 1: return 12;
                case 8: return 1;
                case 9: return 8;
                case 10: return 9;
                case 11: return 10;
                case 12: return furthest < 6 ? 8 : furthest < 13 ? 9 : furthest < 20 ? 10 : 11;
                default: return reason;
            }
        }
        if (right)
        {
            switch (reason)
            {
                case 1: return 8;
                case 8: return furthest < 6 ? 12 : 9;
                case 9: return furthest < 13 ? 12 : 10;
                case 10: return furthest < 20 ? 12 : 11;
                case 11: return 12;
                case 12: return 1;
                default: return reason;
            }
        }
        return reason is < 7 and > 1 ? 1 : reason;
    }

    // Amedo
    private int PauseReason_176698(bool paused)
    {
        ulong w = Word_00;
        int reason = (int)((w >> 27) & 0xF);
        if (reason == 7) return 7;
        int next;
        if ((w & (1ul << 58)) != 0)
        {
            next = 6;
            Word_00 = w & 0xF8FFFFFFFFFFFFFFul;
        }
        else if (GameMode is ModePaused or ModeGallery)
        {
            int save = AutosaveState;
            if (save is 4 or 6) return reason;
            next = reason < 7 && 4 <= reason ? reason : 0;
        }
        else if ((w & (1ul << 56)) != 0)
        {
            next = 4;
            Word_00 = w & 0xF8FFFFFFFFFFFFFFul;
        }
        else if ((w & (1ul << 57)) != 0)
        {
            Word_00 = w & 0xF8FFFFFFFFFFFFFFul;
            next = ((w >> 59) & 0xF) == 0 ? 0 : 5;
        }
        else
        {
            if (((w >> 59) & 0xF) == 3) return 0;
            next = 0;
        }
        if (next == 0)
        {
            if (Pad1 is null) next = 2;
            else if (!paused) next = StartPressed ? 1 : 0;
            else next = PausePage_176470();
        }
        Word_00 = (Word_00 & ~(0xFul << 27)) | ((ulong)(next & 0xF) << 27);
        if (paused && next == reason) return next;
        if (next == 0) return 0;
        ShowPauseScreen_176260();
        if (paused) return next;
        Word_00 = (Word_00 & 0xFFFFFFC0FFFFFFFFul) | ((ulong)GameMode << 32);
        PauseChunkClocks_1918A8();
        PauseRun_181400();
        Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] paused (reason {next}) from 0x{GameMode:X}");
        return next;
    }

    // Amedo
    public bool QuitToTitle_17B828()
    {
        _requestedMode = ModeQuitToTitle;
        return true;
    }

    // Amedo
    private int QuitToTitleFade_177668()
    {
        Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.AllButOverlaysGroup], Seconds(0.25f), 0);
        Oleg.Fader_13B8.HiddenColour_40 = Colour.GetColor_11BF10(0);
        Oleg.Fader_13B8.ShownColour_44 = Colour.GetColor_11BF10(8);
        return ModeLoadingTitle;
    }

    // Amedo
    public bool DisableAutosave_17BCB0()
    {
        ulong w = Word_00;
        if ((w & 0x7800000000000000ul) == 0) return false;
        Word_00 = (w & 0x8103FFFFFFFFFFFFul) | 0x0144000000000000ul;
        if (SaveManager_309724 is not { Autosave: true } manager) return false;
        manager.Autosave = false;
        return true;
    }

    // Amedo
    public bool SaveFromPause_17BF88()
    {
        AutosavePauses_8F0 = 0;
        Word_00 = (Word_00 & 0x80FFFFFFFFFFFFFFul) | 0x3000000000000000ul;
        SaveManager_309724?.Request_2A11D0(4, Seconds(2f));
        Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] save from pause (request 4)");
        return true;
    }

    // Amedo
    public bool LoadFromPause_17BDD8()
    {
        Word_00 = (Word_00 & 0x80FFFFFFFFFFFFFFul) | 0x2000000000000000ul;
        SaveManager_309724?.Request_2A11D0(5, Seconds(2f));
        Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] load from pause (request 5)");
        return true;
    }

    public const int CrossButton = 5; // Amedo
    public ulong GalleryWord_08; // Amedo
    public string GalleryPath_5C = ""; // Amedo
    public int PictureHold_14; // Amedo
    public Character.CharacterPad? Pad_34 => Pad1; // Amedo

    // Amedo
    public bool RequestGallery_17BBD0(int fadeTicks, string path, int first, int last)
    {
        bool paused = GameMode == ModePaused;
        if (!paused) return false;
        MovieFade_54 = fadeTicks;
        GalleryWord_08 = (GalleryWord_08 & 0xFFFFFFFFF000000Ful) | ((ulong)(first & 0xFF) << 20) | ((ulong)(first & 0xFF) << 4) | ((ulong)(last & 0xFF) << 12);
        GalleryPath_5C = path;
        _requestedMode = ModeGallery;
        Log?.Invoke($"[mode {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] gallery {path} {first}..{last}");
        return true;
    }

    // Amedo
    private bool ShowPictureScreen_172D88(int group)
    {
        int hold = PictureHold_14;
        PictureHold_14 = 0;
        ulong w = Oleg.Picture_318;
        bool busy = (w & (1ul << 32)) != 0;
        if (busy || (w & 0x1FE00000000ul) == 12ul << 33)
        {
            if (!busy) Oleg.LoadPicture_170ED0(2, false);
            return false;
        }
        bool loaded = (((w >> 41) ^ (w >> 33)) & 0xFF) == 0;
        int index = WidgetController.IndexOf_259AF0(Oleg.Groups_08[Oleg.PictureGroup]);
        var picture = index < 0 ? null : Oleg.Widgets_208[index];
        int half = Seconds(0.5f);
        if (loaded)
        {
            if (1 < picture!.Phase)
            {
                if (picture.Phase != 3) return false;
                if (hold == 0) hold = Frame;
                PictureHold_14 = hold;
                return true;
            }
            Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.AllButOverlaysGroup], half, 0);
            Oleg.Show_259B20(Oleg.Groups_08[Oleg.FlashGroup], half, 0);
            Oleg.Show_259B20(Oleg.Groups_08[group], half, 0);
            return false;
        }
        if (picture!.Phase != 3)
        {
            if (1 < picture.Phase) return false;
            Oleg.ReleasePicture_170D78(2);
            return false;
        }
        Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.AllButOverlaysGroup], half, 0);
        Oleg.Show_259B20(Oleg.Groups_08[Oleg.FlashGroup], half, 0);
        return false;
    }

    // Amedo
    private int Gallery_174D90()
    {
        bool next = true;
        if (Frame != _modeEnteredFrame)
        {
            if (!ShowPictureScreen_172D88(Oleg.GalleryGroup)) next = false;
            else
            {
                bool back = Pad1?.GetButtonState_2B3548(TriangleButton, true) ?? false;
                bool on = Pad1?.GetButtonState_2B3548(CrossButton, true) ?? false;
                if (back)
                {
                    ShowPauseScreen_176260();
                    return ModePaused;
                }
                next = false;
                if (on)
                {
                    int current = (int)((GalleryWord_08 >> 20) & 0xFF), last = (int)((GalleryWord_08 >> 12) & 0xFF);
                    if (current == last)
                    {
                        ShowPauseScreen_176260();
                        return ModePaused;
                    }
                    next = true;
                    GalleryWord_08 = (GalleryWord_08 & 0xFFFFFFFFF00FFFFFul) | ((ulong)((current + 1) & 0xFF) << 20);
                }
            }
        }
        if (next)
        {
            int current = (int)((GalleryWord_08 >> 20) & 0xFF);
            int request = ((Oleg.Picture_318 >> 33) & 0xFF) == 9 ? 10 : 9;
            Oleg.CustomPicture_398 = GalleryPath_5C + (current < 10 ? "0" : "") + current.ToString(System.Globalization.CultureInfo.InvariantCulture);
            uint high = (uint)(Oleg.Picture_318 >> 32);
            high = (high & 0xFFFE01FFu) | ((uint)request << 9);
            Oleg.Picture_318 = (Oleg.Picture_318 & 0xFFFFFFFFul) | ((ulong)high << 32);
        }
        return -1;
    }
}
