using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Runtime.Screen.Oleg;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-28
public sealed partial class AgentLabWorld
{
    public const int ModeExitCutscene = 0xB;
    public const int ModePlay = 0xC;
    public const int ModeCutscene = 0xD;
    public const int ModeCutsceneAlt = ModeTitle; // Amedo
    public const int ModeMovie = 0xE; // Amedo
    public const int ModeLoadingTitle = 0x6;
    public const int ModeLoadingLevel = 0x9; // Amedo
    public const int ModePaused = 0xF; // Amedo
    public const int ModeGallery = 0x10; // Amedo
    public const int ModeResuming = 0x11; // Amedo
    public const int ModeCredits = 0x13; // Amedo
    public const int ModeQuitToTitle = 0x14; // Amedo
    public const int NoMode = 0x18; // Amedo

    // Amedo
    public ulong Word_00 = ((ulong)NoMode << 50) | ((ulong)ModePlay << 44) | ((ulong)ModePlay << 38);
    public int GameMode { get => (int)((Word_00 >> 44) & 0x3F); private set => Word_00 = (Word_00 & ~(0x3Ful << 44)) | ((ulong)(value & 0x3F) << 44); }
    public int PreviousMode { get => (int)((Word_00 >> 38) & 0x3F); private set => Word_00 = (Word_00 & ~(0x3Ful << 38)) | ((ulong)(value & 0x3F) << 38); }
    private int _requestedMode
    {
        get { int r = (int)((Word_00 >> 50) & 0x3F); return r == NoMode ? -1 : r; }
        set => Word_00 = (Word_00 & ~(0x3Ful << 50)) | ((ulong)(value < 0 ? NoMode : value & 0x3F) << 50);
    }
    public bool StartPressed => ((Word_00 >> 31) & 1) != 0;
    public int PauseReason => (int)((Word_00 >> 27) & 0xF);
    public int SavedMode => (int)((Word_00 >> 32) & 0x3F);
    public int AutosaveState => (int)((Word_00 >> 59) & 0xF);
    private readonly ushort[] _videoModelIds_80 = new ushort[15]; // Amedo
    private readonly AgentLabAgent?[] _videoModels_A0 = new AgentLabAgent?[15];
    private int _videoModelCount_E0;

    // Amedo
    internal void SetVideoModelInstance_29E988(ushort id, AgentLabAgent instance)
    {
        bool found = false;
        for (int i = 0; i < _videoModelCount_E0; i++)
        {
            if (_videoModelIds_80[i] != id) continue;
            _videoModels_A0[i] = instance;
            found = true;
        }
        if (found || 15 <= _videoModelCount_E0) return;
        _videoModelIds_80[_videoModelCount_E0] = id;
        _videoModels_A0[_videoModelCount_E0] = instance;
        _videoModelCount_E0++;
    }

    // Amedo
    public AgentLabAgent? VideoModelInstance(ushort id)
    {
        for (int i = 0; i < _videoModelCount_E0; i++)
            if (_videoModelIds_80[i] == id) return _videoModels_A0[i];
        return null;
    }
    public int MovieFade_54 { get; private set; } // Amedo
    public int Movie_58 { get; private set; }

    // Amedo
    public void RequestMovie_17B868(int fadeTicks, int movie)
    {
        MovieFade_54 = fadeTicks;
        Movie_58 = movie;
        _requestedMode = ModeMovie;
        Log?.Invoke($"[mode {Now:0.00}s] movie {movie} requested (fade {fadeTicks} ticks)");
    }

    // Amedo
    public bool RequestCredits_17B7C8()
    {
        if (GameMode == ModeCredits) return false;
        _requestedMode = ModeCredits;
        Log?.Invoke($"[mode {Now:0.00}s] credits requested (0x17B7C8)");
        return true;
    }

    // Amedo
    private bool MovieEnded_17B980()
    {
        if (GameMode != ModeMovie) return false;
        switch (PreviousMode)
        {
            case 0x7: _requestedMode = ModeLoadingTitle; break;
            case ModePaused:
                _requestedMode = ModePaused;
                ShowPauseScreen_176260();
                break;
            default: _requestedMode = ModeExitCutscene; break;
        }
        Log?.Invoke($"[mode {Now:0.00}s] movie {Movie_58} ended -> 0x{_requestedMode:X}");
        return true;
    }
    private int _modeEnteredTicks; // Amedo 2026-10-01
    private readonly Dictionary<(Entity, ushort, ushort), AnimatedObject.SlotGroup?> _foreignGroups = new();
    private readonly List<MeshDecoder.ForeignGroupBuild> _foreignBuilds = new();

    public Func<Entity, ushort, ushort, AnimatedObject.SlotGroup?, MeshDecoder.ForeignGroupBuild?>? ForeignGroupHost { get; set; }

    public AnimatedObject.SlotGroup? ForeignGroup(AgentLabAgent agent, ushort ogiId, ushort animId)
    {
        var key = (agent.Entity, ogiId, animId);
        if (_foreignGroups.TryGetValue(key, out var cached)) return cached;
        var sameModel = _foreignGroups.FirstOrDefault(kv => kv.Key.Item1 == agent.Entity && kv.Key.Item2 == ogiId && kv.Value is not null).Value;
        AnimatedObject.SlotGroup? group = null;
        if (ForeignGroupHost?.Invoke(agent.Entity, ogiId, animId, sameModel) is { } build)
        {
            _foreignBuilds.Add(build);
            group = build.Group;
            Log?.Invoke($"[foreign {Now:0.00}s] {agent.Entity.Name}: model OGI 0x{ogiId:X} + anim 0x{animId:X}" +
                        $"{(build.CreatedAnimator ? " (new animator)" : "")}{(sameModel is not null ? " (shared mesh)" : "")}");
        }
        else Unimplemented("animation", 0x211B08, $"foreign slot OGI 0x{ogiId:X} anim 0x{animId:X} (no host or missing data)");
        _foreignGroups[key] = group;
        return group;
    }

    public void UndoForeignGroups()
    {
        for (int i = _foreignBuilds.Count - 1; i >= 0; i--) _foreignBuilds[i].Undo();
        _foreignBuilds.Clear();
        _foreignGroups.Clear();
    }

    // Amedo 2026-09-29
    public void UndoForeignGroupsUnder(Entity root)
    {
        bool Under(Entity? e)
        {
            for (; e is not null; e = e.Parent)
                if (ReferenceEquals(e, root)) return true;
            return false;
        }
        for (int i = _foreignBuilds.Count - 1; i >= 0; i--)
        {
            if (!Under(_foreignBuilds[i].Owner)) continue;
            _foreignBuilds[i].Undo();
            _foreignBuilds.RemoveAt(i);
        }
        _foreignDrop.Clear(); // Amedo
        foreach (var key in _foreignGroups.Keys)
            if (Under(key.Item1)) _foreignDrop.Add(key);
        foreach (var key in _foreignDrop) _foreignGroups.Remove(key);
        _foreignDrop.Clear();
    }

    private readonly List<(Entity, ushort, ushort)> _foreignDrop = new(); // Amedo

    public ICutsceneCamera? CutsceneCamera { get; set; } // Amedo 2026-09-29
    public bool CutsceneActive => GameMode is ModeCutscene or ModeCutsceneAlt;
    public float HudFadeTime { get; private set; }
    public bool HudHidden { get; private set; }

    // Amedo
    public bool StartCutscene_176A48(float seconds, AgentLabAgent by)
    {
        float fade = seconds * 0.5f;
        if (GameMode == ModePlay)
        {
            _requestedMode = ModeCutscene;
            HudHidden = true;
            HudFadeTime = fade;
            BottomShown = true; // Amedo
            HoldPlayer_178038();
            DisablePlayerControl_17C190();
            ClearBottomText_120FF8(); // Amedo
            Oleg.Hide_259BC0(ulong.MaxValue, Seconds(fade), 0);
            Oleg.Show_259B20(Oleg.Groups_08[Oleg.CutsceneGroup], Seconds(fade), 0); // Amedo
            Log?.Invoke($"[mode {Now:0.00}s] CutsceneStart({seconds:0.##}) by {by.Entity.Name}: 0xC -> 0xD, HUD out over {fade:0.##}s");
            Sounds?.Groups.DuckStart_1E58D8(); // Amedo 2026-10-06
            return true;
        }
        if (GameMode != ModeCutsceneAlt) return false;
        HudFadeTime = fade;
        BottomShown = true; // Amedo
        Oleg.Show_259B20(Oleg.Groups_08[Oleg.CutsceneGroup], Seconds(fade), 0); // Amedo
        return true;
    }

    // Amedo
    public bool EndCutscene_176B98(float seconds, AgentLabAgent by)
    {
        float fade = seconds * 0.5f;
        if (GameMode == ModeCutscene)
        {
            _requestedMode = ModeExitCutscene;
            HudHidden = false;
            HudFadeTime = fade;
            Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.CutsceneGroup], Seconds(fade), 0); // Amedo
            Log?.Invoke($"[mode {Now:0.00}s] CutsceneEnd({seconds:0.##}) by {by.Entity.Name}: 0xD -> 0xB, HUD back over {fade:0.##}s");
            Sounds?.Groups.DuckEnd_1E58F0(); // Amedo 2026-10-06
            ReleasePlayer_178178(true); // Amedo
            return true;
        }
        if (GameMode == ModeCutsceneAlt)
        {
            BottomShown = false; // Amedo
            Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.CutsceneGroup], Seconds(fade), 0); // Amedo
            return true;
        }
        ReleasePlayer_178178(false); // Amedo
        Sounds?.Groups.DuckEnd_1E58F0(); // Amedo 2026-10-06
        return false;
    }

    // Amedo
    private void HoldPlayer_178038()
    {
        int slot = (int)(Progression >> 8) & 0xF;
        if ((slot == 6 ? null : PlayerSlot_570(slot)) is not { OwnCharacter: { } c } who) return;
        who.EventsHeld_18 = true;
        c.AutomaticInput = true;
        bool leader = (c.MotionFlagsHigh & Character.CharacterController.LeaderBit) != 0;
        bool follower = (c.MotionFlagsHigh & Character.CharacterController.FollowerBit) != 0;
        int hp = c.HitPoints;
        if (!leader)
        {
            if (ClassicAku) GoldenEnter_23F18C(c);
            else c.Reset_131888();
        }
        if (follower) c.MotionFlagsHigh |= Character.CharacterController.FollowerBit;
        else if (leader) c.MotionFlagsHigh |= Character.CharacterController.LeaderBit;
        c.HitPoints = hp;
    }

    // Amedo
    private void ReleasePlayer_178178(bool reset)
    {
        int slot = (int)(Progression >> 8) & 0xF;
        if ((slot == 6 ? null : PlayerSlot_570(slot)) is not { OwnCharacter: { } c } who) return;
        who.EventsHeld_18 = false;
        c.AutomaticInput = false;
        bool leader = (c.MotionFlagsHigh & Character.CharacterController.LeaderBit) != 0;
        int hp = c.HitPoints;
        if (!leader && reset)
        {
            c.MotionPartReset_140230();
            if (ClassicAku) GoldenExit_23F1D4(c);
            else c.Reset_131888();
        }
        c.HitPoints = hp;
        foreach (int i in ReleaseSlots_178284) PlayerSlot_570(i)?.ClearAgentFlags(0x80000);
    }

    private static readonly int[] ReleaseSlots_178284 = [0, 1, 2, 3, 5]; // Amedo

    // Amedo
    internal void DisablePlayerControl_17C190()
    {
        uint p = Progression;
        DisableCharacter_177F28((int)(p >> 8) & 0xF);
        DisableCharacter_177F28((int)(p >> 12) & 0xF);
    }

    // Amedo
    private bool DisableCharacter_177F28(int slot)
    {
        if ((slot == 6 ? null : PlayerSlot_570(slot)) is not { } who) return false;
        if (who.OwnCharacter is { } c)
        {
            c.Pad = null;
            c.Move = System.Numerics.Vector3.Zero;
        }
        return true;
    }

    public Action? EnableCharacters_17C0C8 { get; set; } // Amedo
    private int _modeEnteredFrame = -1; // Amedo

    // Amedo
    private int StartingPlay_1745D0()
    {
        if (_modeEnteredFrame == Frame)
        {
            for (int i = 0; i < 6; i++) DisableCharacter_177F28(i);
            switch (PreviousMode)
            {
                case 0:
                case 9:
                    GameProgressEnter_167B60(RestartKind, SaveController_594.Name_0C, SaveController_594.Checkpoint_18);
                    EnableCharacters_17C0C8?.Invoke();
                    CutsceneCamera?.PlayerCameraReset_15E538();
                    goto case ModeCredits;
                case ModeCredits:
                case ModeReset:
                    ResumeChunkClocks_1917E0();
                    ResumeRun_181438();
                    goto case ModeCutscene;
                case ModeCutscene:
                case ModeMovie:
                    EnableCharacters_17C0C8?.Invoke();
                    BottomShown = false; // Amedo
                    break;
            }
            return -1;
        }
        if (PreviousMode is not (ModeCutscene or ModeMovie)) ApplyPlayerMode_1745D0();
        CutsceneCamera?.Prepare_27DEC0();
        Oleg.Hide_259BC0(ulong.MaxValue, Seconds(0.25f), 0); // Amedo
        BottomHideTicks = Seconds(0.25f);
        Log?.Invoke($"[mode {Now:0.00}s] 0xB -> 0xC (play, after 0x{PreviousMode:X})");
        return ModePlay;
    }

    // Amedo
    private void GameProgressEnter_167B60(int kind, string chunkName, ushort instance)
    {
        if (kind != 2) return;
        Clear_17A6A0(Temp_588);
        Clear_17A6A0(Level_590);
        UseRespawn(null);
        _restoring = null;
        var chunk = Chunks.Find(c => c.Name == chunkName);
        var target = chunk is null || instance == 0xFFFF ? null : Agents.FirstOrDefault(a => ReferenceEquals(a.Chunk, chunk) && a.Instance.Source.GetID() == instance);
        int mode = (int)(Progression >> 4) & 0xF;
        if (target is null || mode is < 1 or > 6 || Player is not { } p)
        {
            Log?.Invoke($"[mode {Now:0.00}s] GameProgress::Enter (kind 2): saved checkpoint {instance} in {chunkName} not here -> player stays");
            return;
        }
        Set_172070(Level_590, target);
        UseRespawn(Level_590);
        Level_590.Restored_02 = true;
        _restoring = null;
        if (target is { ControllerType: 13 }) SetZoneState(target, true);
        p.GameMatrix = target.GameMatrix;
        p.SetComebackPlacement_23CD80(target.GameMatrix);
        Log?.Invoke($"[mode {Now:0.00}s] GameProgress::Enter (kind 2): player at the saved checkpoint {target.Entity.Name} {target.GamePosition}");
    }

    private (int End, Character.CharacterController? Ctrl) _goldenResume_23F184; // Amedo

    // Amedo
    private void GoldenEnter_23F18C(Character.CharacterController c)
    {
        _goldenResume_23F184.End = 0;
        if ((c.State70 & 0xF) == 1)
        {
            int end = c.Timer74 + c.Duration78;
            if (0 < end - c.Ticks) _goldenResume_23F184.End = end;
        }
        _goldenResume_23F184.Ctrl = c;
        c.Reset_131888();
    }

    // Amedo
    private void GoldenExit_23F1D4(Character.CharacterController c)
    {
        c.Reset_131888();
        var (end, ctrl) = _goldenResume_23F184;
        _goldenResume_23F184.End = 0;
        if (end == 0 || !ReferenceEquals(ctrl, c)) return;
        int rest = end - c.Ticks;
        if (rest <= 0) rest = 1;
        c.Duration78 = rest;
        c.State70 = (c.State70 & ~0xFFul) | (c.State70 & 0xFul) | 0x10ul;
        c.MotionWord |= 0x400;
    }

    // Amedo
    internal bool GoldenCheck_2C8260(Character.CharacterController c)
    {
        var (end, ctrl) = _goldenResume_23F184;
        if (!ClassicAku || end == 0 || !ReferenceEquals(ctrl, c) || (c.State70 & 0xF) == 1) return false;
        if (0 < end - c.Ticks) return false;
        _goldenResume_23F184.End = 0;
        return true;
    }

    private void ResetCutsceneMode()
    {
        HudHidden = false;
        HudFadeTime = 0f;
    }

    private readonly HashSet<int> _reportedModes = new();

    // Amedo
    private void Update_177168()
    {
        bool start = Pad1?.GetButtonState_2B3548(StartButton, true) ?? false;
        Word_00 = (Word_00 & ~(1ul << 31)) | (start ? 1ul << 31 : 0ul);
        UpdateSaving_173238();
        TakeSnapshot(); // Amedo
        if (_requestedMode >= 0)
        {
            _modeEnteredFrame = Frame; // Amedo
            PreviousMode = GameMode; // Amedo
            GameMode = _requestedMode;
            _requestedMode = -1;
            _modeEnteredTicks = GcNowTicks; // Amedo
        }
        int next = -1;
        switch (GameMode)
        {
            case ModeBoot: next = Boot_1772C8(); break; // Amedo
            case ModeLogo: // Amedo
                next = Logo_173AB0();
                if (0 <= next) Oleg.SetUpParticles_16D368(Screen?.ParticlePage_0);
                break;
            case ModeWaitingForPad: next = WaitingForPad_173BA0(); break; // Amedo
            case ModeCheckingCard: next = CheckingCard_173C58(); break; // Amedo
            case ModeIntroMovie: next = IntroMovie_177394(); break; // Amedo
            case ModeIdentMovie: next = IdentMovie_1773E0(); break; // Amedo
            case ModeLoadingTitle: next = LoadingTitle_173CE8(); break; // Amedo
            case ModeTitle: next = Title_173F38(); break; // Amedo
            case ModeMainMenu: next = MainMenu_174108(); break; // Amedo
            case ModeNewGame: next = NewGame_174478(); break; // Amedo
            case ModeLoadingLevel: next = LoadingLevel_174240(); break; // Amedo
            case ModeExitCutscene: next = StartingPlay_1745D0(); break; // Amedo
            case ModePlay: next = Playing_174A70(); break; // Amedo
            case ModeMovie: // Amedo
                if (Frame == _modeEnteredFrame)
                {
                    PauseChunkClocks_1918A8();
                    PauseRun_181400();
                }
                if (PlayMovie_172B60(MovieFade_54, Movie_58)) MovieEnded_17B980();
                break;
            case ModePaused: next = Paused_174C70(); break; // Amedo
            case ModeGallery: next = Gallery_174D90(); break; // Amedo
            case ModeResuming: next = Resuming_1775E8(); break; // Amedo
            case ModeGameOver: next = GameOver_175008(); break; // Amedo
            case ModeCredits: next = Credits_175160(); break; // Amedo
            case ModeQuitToTitle: next = QuitToTitleFade_177668(); break; // Amedo
            case ModeCutscene: next = Watching_174F38(); break; // Amedo
            case ModeResetFade: next = FadingOut_1754B8(); break; // Amedo
            case ModeReset: next = Restarting_175618(); break; // Amedo
            default:
                if (_reportedModes.Add(GameMode)) Unimplemented("mode", 0x2F4970 + GameMode * 4, $"GameController state 0x{GameMode:X} (U4)");
                break;
        }
        if (0 <= next) _requestedMode = next;
        if (PlayerCharacter is { } c) c.CutsceneMode = CutsceneActive;
        foreach (var agent in Agents) // Amedo 2026-09-29
            if (agent.OwnCharacter is { } npc) npc.CutsceneMode = CutsceneActive;
    }
}
