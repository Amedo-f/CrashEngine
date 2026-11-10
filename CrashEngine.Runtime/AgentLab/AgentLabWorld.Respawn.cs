using System.Numerics;
using CrashEngine.Runtime.Screen.Oleg;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-28
public sealed partial class AgentLabWorld
{
    public const int ModeGameOver = 0x12, ModeResetFade = 0x15, ModeReset = 0x16; // Amedo
    public const int ResetKindDeath = 3;

    private bool _sceneResetPending; // Amedo

    public int Lives { get => Progress.Lives; set => Progress.Lives = value; } // Amedo
    public Matrix4x4? RespawnMatrix { get; private set; }
    public AgentLabAgent? RespawnAgent { get; private set; }
    public string? RespawnChunkName { get; private set; } // Amedo 2026-09-29
    public Matrix4x4 RespawnToAnchor { get; private set; } = Matrix4x4.Identity;
    public float FadeAlpha => 0f; // Amedo
    public int RespawnMode => GameMode is ModeResetFade or ModeReset ? GameMode : 0; // Amedo
    public int Respawns { get; private set; }
    public int ResetKind { get; private set; }
    public bool ResetPending => _sceneResetPending; // Amedo
    public int RestartKind => (int)((Word_00 >> 23) & 0xF); // Amedo

    // Amedo
    public void MoveRespawn(Matrix4x4 move, string? anchor)
    {
        foreach (var cp in new[] { Temp_588, Level_590 })
            if (cp.Set_00 && cp.Chunk_04 != anchor) cp.ToAnchor *= move;
        if (RespawnChunkName is { } name && name != anchor) RespawnToAnchor *= move;
    }

    // Amedo
    public bool RestartFromCheckpoint_17B6C0()
    {
        if (Progress.Lives == 0) return GameOver_17B768();
        if (GameMode == ModeResetFade) return false;
        Word_00 = (Word_00 & 0xFF03FFFFF87FFFFFul) | 0x0054000001800000ul;
        Log?.Invoke($"[AgentLab {Now:0.00}s] RestartFromCheckpoint -> mode 0x15 (kind 3), lives {Lives}");
        return true;
    }

    // Amedo
    public bool RequestRestart_17B640(int kind)
    {
        if (GameMode == ModeResetFade) return false;
        Word_00 = (Word_00 & 0xFF03FFFFF87FFFFFul) | 0x0054000000000000ul | ((ulong)(kind & 0xF) << 23);
        Log?.Invoke($"[AgentLab {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] RequestRestart (kind {kind}) -> mode 0x15");
        return true;
    }

    // Amedo
    public bool GameOver_17B768()
    {
        if (GameMode == ModeGameOver) return false;
        _requestedMode = ModeGameOver;
        Log?.Invoke($"[AgentLab {Now:0.00}s] no lives left -> game over (mode 0x12)");
        return true;
    }

    // Amedo
    private int FadingOut_1754B8()
    {
        if (Frame == _modeEnteredFrame)
        {
            PauseChunkClocks_1918A8();
            PauseRun_181400();
            Oleg.Fader_13B8.HiddenColour_40 = Colour.GetColor_11BF10(0);
            Oleg.Fader_13B8.ShownColour_44 = Colour.GetColor_11BF10(8);
            int half = Seconds(0.5f);
            Oleg.Hide_259BC0(ulong.MaxValue, half, 0);
            Oleg.Show_259B20(Oleg.Groups_08[Oleg.FlashGroup], half, 0);
            Sounds?.Groups.FadeOut_1E6118(0.5f); // Amedo
            return -1;
        }
        int index = WidgetController.IndexOf_259AF0(Oleg.Groups_08[Oleg.FlashGroup]);
        return index >= 0 && Oleg.Widgets_208[index]?.Phase == 3 ? ModeReset : -1;
    }

    // Amedo
    private int Restarting_175618()
    {
        int kind = RestartKind;
        Progress.Reset_167D38(kind, GcNowTicks);
        ChooseRespawn_167D38(kind); // Amedo 2026-11-09
        if (RespawnChunkName is { } chunk) StartChunk_500 = chunk;
        if (kind == 2)
        {
            SaveController_594.Lives = 5;
            SaveController_594.TakeOptions_1798F8(this);
            StartChunk_500 = SaveController_594.Name_0C;
            return ModeLoadingLevel;
        }
        if (StartChunk_500.Length > 0 && !ChunkObjectsLoaded_2AC8B0(StartChunk_500)) return ModeLoadingLevel; // Amedo 2026-11-09
        ResetGame_173040(kind);
        return ModeExitCutscene;
    }

    // Amedo 2026-11-09
    private bool ChunkObjectsLoaded_2AC8B0(string name)
    {
        string wanted = Streaming.StreamLink.Normalize(name);
        foreach (var c in Chunks)
            if (string.Equals(c.Name, wanted, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // Amedo
    private int LoadingLevel_174240()
    {
        if (Frame == _modeEnteredFrame)
        {
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
            int kind = RestartKind;
            Oleg.Reset_179E60();
            if (StartChunk_500.Length != 0) StreamLoadStartChunk_2AA3E8(kind);
            Progress.Reset_167D38(kind, GcNowTicks);
            ChooseRespawn_167D38(kind);
            if (kind == 2) SaveController_594.Restore_1685C0(true, Progress, RestoreChunkStates_263B38, GcNowTicks, this);
            Log?.Invoke($"[AgentLab {Now:0.00}s gc {GcNowTicks / 9000.0:0.00}s] loading level (kind {kind}) from {StartChunk_500}, lives {Lives}");
            return -1;
        }
        int mode = (GalleryWord_08 & 1) == 0 ? 1 : 2;
        return ChunkLoaded_1681F8(mode) ? ModeExitCutscene : -1;
    }

    // Amedo
    private void ResetGame_173040(int kind)
    {
        PauseChunkClocks_1918A8();
        PauseRun_181400();
        ResetKind = kind;
        _sceneResetPending = true;
        Progress.Reset_167D38(kind, GcNowTicks);
        ChooseRespawn_167D38(kind); // Amedo
        Log?.Invoke($"[AgentLab {Now:0.00}s] mode 0x16: scene reset (0x173040, kind {kind})");
    }

    // Amedo
    private int GameOver_175008()
    {
        if (Frame != _modeEnteredFrame)
        {
            ShowPictureScreen_172D88(Oleg.GameOverGroup);
            return -1;
        }
        uint p = Progression;
        int picture = ((p >> 8) & 0xF) switch
        {
            1 => 4,
            0 => ((p >> 12) & 0xF) == 1 ? 6 : 5,
            3 => 8,
            5 => 7,
            _ => -1,
        };
        if (picture >= 0)
        {
            uint high = (uint)(Oleg.Picture_318 >> 32);
            high = (high & 0xFFFE01FFu) | ((uint)picture << 9);
            Oleg.Picture_318 = (Oleg.Picture_318 & 0xFFFFFFFFul) | ((ulong)high << 32);
        }
        PauseChunkClocks_1918A8();
        PauseRun_181400();
        return -1;
    }

    public void AddLives(int n, AgentLabAgent caller)
    {
        if (n <= 0 && !ReferenceEquals(caller, Player)) return;
        ChangeLives_1712D8(n, fromWumpa: false); // Amedo 2026-10-01
        Log?.Invoke($"[AgentLab {Now:0.00}s] lives {(n < 0 ? "" : "+")}{n} -> {Lives}");
    }

    // Amedo
    public sealed class Checkpoint
    {
        public bool Set_00, Restored_02;
        public uint PlayerMode;
        public string Chunk_04 = "";
        public AgentLabAgent? Agent;
        public Matrix4x4 Placement_20, ToAnchor;
    }

    public readonly Checkpoint Temp_588 = new(), Level_590 = new(); // Amedo

    // Amedo
    public bool SetRespawn(AgentLabAgent checkpoint, bool level)
    {
        Release_17A770(level ? Temp_588 : Level_590, level, checkpoint);
        int mode = (int)(Progression >> 4) & 0xF;
        if (mode is < 1 or > 6) return false;
        return Set_172070(level ? Level_590 : Temp_588, checkpoint);
    }

    // Amedo
    private bool Release_17A770(Checkpoint cp, bool clear, AgentLabAgent? instance)
    {
        bool other = cp.Agent is { } old && !ReferenceEquals(old, instance);
        if (other && cp.Agent is { ControllerType: 13 } crate) crate.TriggerEventSlot(15);
        if (clear) Forget(cp);
        cp.Restored_02 = false;
        return other;
    }

    internal void ForgetCheckpointAgent(AgentLabAgent agent)
    {
        foreach (var cp in (ReadOnlySpan<Checkpoint>)[Temp_588, Level_590])
            if (ReferenceEquals(cp.Agent, agent)) cp.Agent = null;
    }

    // Amedo 2026-11-09
    private void CarryCheckpoints(AgentLabWorld from)
    {
        string root = RootChunk.Name;
        Matrix4x4? oldRoot = from.Temp_588.Set_00 && from.Temp_588.Chunk_04 == root ? from.Temp_588.ToAnchor
                           : from.Level_590.Set_00 && from.Level_590.Chunk_04 == root ? from.Level_590.ToAnchor
                           : from.RespawnChunkName == root ? from.RespawnToAnchor : null;
        var rebase = oldRoot is { } r && Matrix4x4.Invert(r, out var inv) ? inv * RootChunk.ToAnchor : (Matrix4x4?)null;
        foreach (var (cp, old) in new[] { (Temp_588, from.Temp_588), (Level_590, from.Level_590) })
        {
            cp.Set_00 = old.Set_00;
            cp.Restored_02 = old.Restored_02;
            cp.PlayerMode = old.PlayerMode;
            cp.Chunk_04 = old.Chunk_04;
            cp.Placement_20 = old.Placement_20;
            cp.ToAnchor = old.Chunk_04 == root ? RootChunk.ToAnchor : rebase is { } m ? old.ToAnchor * m : old.ToAnchor;
            string? name = old.Agent?.Entity.Name;
            cp.Agent = name is null ? null : Agents.FirstOrDefault(a => a.Chunk.Name == old.Chunk_04 && a.Entity.Name == name);
            if (cp.Set_00) Log?.Invoke($"[AgentLab {Now:0.00}s] level switch keeps the {(ReferenceEquals(cp, Level_590) ? "level" : "temporary")} respawn point {name ?? "-"} in {cp.Chunk_04}{(cp.Agent is null && name is not null ? " (not in this level yet)" : "")}");
        }
    }

    // Amedo
    private static void Forget(Checkpoint cp)
    {
        cp.Set_00 = false;
        cp.Agent = null;
        cp.Chunk_04 = "";
    }

    // Amedo
    private bool Set_172070(Checkpoint cp, AgentLabAgent checkpoint)
    {
        if (ReferenceEquals(cp.Agent, checkpoint)) return !cp.Restored_02;
        Release_17A770(cp, false, checkpoint);
        cp.Set_00 = true;
        cp.Agent = checkpoint;
        cp.Placement_20 = checkpoint.GameMatrix;
        cp.Chunk_04 = checkpoint.Chunk.Name;
        cp.ToAnchor = checkpoint.Chunk.ToAnchor;
        cp.PlayerMode = PlayerModeWord_172070();
        Log?.Invoke($"[AgentLab {Now:0.00}s] respawn point ({(ReferenceEquals(cp, Level_590) ? "level" : "temporary")}) = {checkpoint.Entity.Name} at {checkpoint.GamePosition} in {checkpoint.Chunk.Name}");
        return true;
    }

    // Amedo
    private void Clear_17A6A0(Checkpoint cp)
    {
        if (cp.Agent is { ControllerType: 13 } crate) SetZoneState(crate, false);
        Forget(cp);
    }

    // Amedo
    private void ChooseRespawn_167D38(int kind)
    {
        Checkpoint? chosen = null;
        switch (kind)
        {
            case 3:
                chosen = Temp_588.Set_00 ? Temp_588 : Level_590.Set_00 ? Level_590 : null;
                if (chosen is not null) Progression = (Progression & 0xFFFF000Fu) | (chosen.PlayerMode << 4); // Amedo
                break;
            case 2:
                if (Level_590.Set_00)
                {
                    chosen = Level_590;
                    Clear_17A6A0(Temp_588);
                }
                break;
            case 0:
            case 1:
                Clear_17A6A0(Temp_588);
                Clear_17A6A0(Level_590);
                break;
        }
        UseRespawn(chosen);
        if (chosen?.Agent is { ControllerType: 13 } crate) SetZoneState(crate, true); // Amedo
    }

    // Amedo
    private void UseRespawn(Checkpoint? chosen)
    {
        RespawnAgent = chosen?.Agent;
        RespawnMatrix = chosen?.Placement_20;
        RespawnChunkName = chosen?.Chunk_04;
        RespawnToAnchor = chosen?.ToAnchor ?? Matrix4x4.Identity;
        RespawnPlayerMode = chosen?.PlayerMode;
        _restoring = chosen;
    }

    private Checkpoint? _restoring; // Amedo

    // Amedo 2026-09-29
    public uint? RespawnPlayerMode { get; private set; }

    // Amedo 2026-09-29
    private uint PlayerModeWord_172070()
    {
        int mode = (int)(Progression >> 4) & 0xF;
        int a = (int)(Progression >> 8) & 0xF, b = (int)(Progression >> 12) & 0xF;
        var first = a == 6 ? null : PlayerSlot_570(a)?.OwnCharacter;
        var second = mode is 1 or 6 || b == 6 ? null : PlayerSlot_570(b)?.OwnCharacter;
        uint ka = first is null ? 6u : (uint)(first.Bones.Kind & 0xF);
        uint kb = second is null ? 6u : (uint)(second.Bones.Kind & 0xF);
        return (uint)mode | (ka << 4) | (kb << 8);
    }

    // Amedo
    internal void StopFilteredObjectNodes_263C30()
    {
        foreach (var agent in Agents.Concat(_pendingAgents).ToList())
        {
            agent.ReleaseParts_23CF38();
            agent.StopRunners_23E498();
        }
    }

    internal void ClearForReset()
    {
        StepQueuedObjects_1985C0(); // Amedo
        ResetPhysicsBodies(); // Amedo
        foreach (var agent in Agents.Concat(_pendingAgents))
        {
            agent.LeaveTree();
            agent.InChunk_A0 = false; // Amedo
        }
        foreach (var chunk in Chunks) chunk.RigidBodies_164?.Release_2524F8(); // Amedo
        _attachmentsGroup.Clear(); // Amedo
        Agents.Clear();
        _pendingAgents.Clear();
        _global.Clear(); // Amedo
        ClearInstanceIds(); // Amedo
        _messages.Clear(); // Amedo 2026-09-30
        _orderDirty = true; // Amedo 2026-10-01
        BottomShown = false; // Amedo
        BottomHideTicks = 0;
        ResetHud_179E60(); // Amedo
        Spawned.Clear();
        _byInstance.Clear();
        Player = null;
        PlayerCharacter = null;
        Current = null;
        _nextPhase = 0;
        _nextRegister = 0; // Amedo
        ResetTriggers();
        ResetCutsceneMode();
        _lifeStates.Clear();
        if (ResetKind < 2) _keptStates.Clear();
    }

    internal void FinishReset()
    {
        Respawns++;
        foreach (var cp in (ReadOnlySpan<Checkpoint>)[Temp_588, Level_590]) // Amedo
            if (cp.Agent is { } was) cp.Agent = Find(was.Instance.Section, was.Instance.Source.GetID());
        if (RespawnAgent is { } old) RespawnAgent = Find(old.Instance.Section, old.Instance.Source.GetID());
        if (_restoring is { } cpRestored) cpRestored.Restored_02 = true; // Amedo
        _restoring = null; // Amedo
        if (Player is { } p)
        {
            var m = RespawnMatrix ?? p.GameMatrix;
            p.GameMatrix = m;
            if (RespawnMatrix is { } comeback) p.SetComebackPlacement_23CD80(comeback); // Amedo
            Log?.Invoke($"[AgentLab {Now:0.00}s] scene reset #{Respawns}: {Agents.Count} agents rebuilt, {p.Entity.Name} at {m.Translation} " +
                        $"({(RespawnAgent is { } r ? r.Entity.Name : "level start")}), hp {PlayerCharacter?.HitPoints}, states kept {_keptStates.Count}");
        }
        _sceneResetPending = false; // Amedo
    }

    // Amedo 2026-09-29
    private void ApplyPlayerMode_1745D0()
    {
        uint p = Progression;
        int a = (int)(p >> 8) & 0xF, b = (int)(p >> 12) & 0xF;
        if ((a == 6 ? null : PlayerSlot_570(a))?.OwnCharacter is not { } first) return;
        var secondAgent = b == 6 ? null : PlayerSlot_570(b); // Amedo 2026-10-05
        var second = secondAgent?.OwnCharacter;
        switch ((p >> 4) & 0xF)
        {
            case 2: first.EnterVehicle_13B938(Character.Vehicles.Vehicle.Humiliskate, second, 0); break;
            case 3: first.EnterVehicle_13B938(Character.Vehicles.Vehicle.Rollerbrawl, second, 0); break;
            case 4: // Amedo 2026-10-05
                if (second is null || secondAgent is null) break;
                Character.CharacterController.Link_132D38(first, second);
                secondAgent.Deliver(0x39, secondAgent);
                break;
        }
    }
}
