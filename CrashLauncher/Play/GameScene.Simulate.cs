using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Runtime;
using ImGuiNET;
using Silk.NET.Input;

namespace CrashLauncher;

// Amedo 2026-09-26
public sealed partial class GameScene
{
    private SceneSimulation? _simulation;
    private bool _simSound = true; // Amedo 2026-09-28
    private float _simVolume = 1f;
    private bool _simGameCamera = true;
    private bool _playPanels; // Amedo 2026-09-28
    private bool _simPerf = true;

#if DEBUG
    private const bool DebugBuild = true;
#else
    private const bool DebugBuild = false;
#endif

    // Amedo 2026-09-28
    private void DrawPerfWindow(SceneSimulation sim)
    {
        float frameBudget = PerfStats.FrameBudgetMs; // Amedo 2026-09-29
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(ViewOrigin.X + ViewW - 8f, ViewOrigin.Y + 8f), ImGuiCond.Always, new System.Numerics.Vector2(1f, 0f)); // Amedo
        ImGui.SetNextWindowBgAlpha(0.45f);
        ImGui.Begin("##perfwindow", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings |
                                    ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoInputs);
        static float Ms(PerfSection s) => PerfStats.Ms(s);
        var warn = new System.Numerics.Vector4(1f, 0.8f, 0.25f, 1f);
        var bad  = new System.Numerics.Vector4(1f, 0.35f, 0.3f, 1f);
        var head = PerfStats.FrameMs > frameBudget * 1.05f ? warn : new System.Numerics.Vector4(0.6f, 1f, 0.6f, 1f);
        ImGui.TextColored(head, $"FPS {PerfStats.Fps:0}   frame {PerfStats.FrameMs:0.0} ms (peak {PerfStats.FramePeakMs:0.0})   {PerfStats.FrameMode} (F3)"); // Amedo 2026-09-29
        if (DebugBuild) { ImGui.SameLine(); ImGui.TextColored(bad, "  DEBUG build (slower)"); }
        float work = Ms(PerfSection.Update) + Ms(PerfSection.Render) + Ms(PerfSection.Ui);
        ImGui.TextColored(work > frameBudget ? warn : new System.Numerics.Vector4(1f, 1f, 1f, 1f),
            $"CPU {work:0.0} ms = update {Ms(PerfSection.Update):0.0} + render {Ms(PerfSection.Render):0.0} + ui {Ms(PerfSection.Ui):0.0}");
        ImGui.SameLine();
        ImGui.TextColored(PerfStats.GpuMs > frameBudget ? warn : new System.Numerics.Vector4(1f, 1f, 1f, 1f), $"   GPU {PerfStats.GpuMs:0.0} ms");
        ImGui.SameLine();
        ImGui.TextDisabled($"   draws {CrashEngine.Importer.MeshRenderer.DrawCallsThisFrame}");
        ImGui.Text($"sim {Ms(PerfSection.Sim):0.00} ms: agents {Ms(PerfSection.SimAgents):0.00} ({sim.World.SteppedAgents}/{sim.World.Agents.Count} ran)" +
                   $" | Crash {Ms(PerfSection.SimPlayer):0.00} | triggers {Ms(PerfSection.SimTriggers):0.00} | physics {Ms(PerfSection.SimPhysics):0.00} | gate {Ms(PerfSection.SimGate):0.00}");
        ImGui.Text($"camera {Ms(PerfSection.Camera):0.00} | particles {Ms(PerfSection.Particles):0.00} | sounds {Ms(PerfSection.Sounds):0.00} | music {Ms(PerfSection.Music):0.00}" +
                   $" | streaming {Ms(PerfSection.Streaming):0.00} (peak {PerfStats.PeakMs(PerfSection.Streaming):0.0})"); // Amedo 2026-09-29
        ImGui.TextColored(PerfStats.Gen2PerSecond > 0 || PerfStats.AllocKbPerFrame > 256f ? warn : new System.Numerics.Vector4(0.75f, 0.75f, 0.75f, 1f),
            $"memory {PerfStats.AllocKbPerFrame:0} KB allocated/frame | GC gen0 {PerfStats.Gen0PerSecond}/s gen1 {PerfStats.Gen1PerSecond}/s gen2 {PerfStats.Gen2PerSecond}/s" +
            $" | GC pause peak {PerfStats.GcPausePeakMs:0.0} ms"); // Amedo 2026-09-29
        if (Input.KeyDown(Key.F9)) MemoryReport.Check();
        ImGui.Text(MemoryReport.Line(sim));
        ImGui.TextDisabled(MemoryReport.LastCheck);
        ImGui.End();
    }

    private bool Simulating => _simulation is not null;
    private bool PlayFullView => Simulating && !_playPanels; // Amedo 2026-09-28

    // Amedo 2026-09-28
    private void HandlePlayViewKeys()
    {
        if (!Simulating || ImGui.GetIO().WantCaptureKeyboard) return;
        if (Input.KeyDown(Key.Tab)) _playPanels = !_playPanels;
        if (Input.KeyDown(Key.F7)) SetSimGameCamera(!_simGameCamera); // Amedo 2026-10-06
    }

    // Amedo 2026-09-28
    private void DrawPlayOverlay()
    {
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(8f, 8f));
        ImGui.SetNextWindowBgAlpha(0.35f);
        ImGui.Begin("##playoverlay", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove |
                                     ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav |
                                     ImGuiWindowFlags.NoDocking);
        DrawSimulationButtons();
        if (Simulating)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Panels##playpanels")) _playPanels = true;
        }
        ImGui.End();
    }

    // Amedo 2026-10-04
    private PlaySettings CurrentPlaySettings(ChunkSource? source) =>
        PlaySettings.Resolve(ProjectPlayValues.From(GetGameProject()), _extractedRoot, Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, source?.GlobalRm2);

    private static bool _playFromStart; // Amedo
    private static bool _recordInput; // Amedo

    private string? PlayLevelName => Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Rm2Path is { } rm2 ? Path.ChangeExtension(rm2, null) : null; // Amedo

    // Amedo
    private string? PlayFromStartProblem() => PlayLevelName is null ? "No level loaded." : null;

    private string? _fromStartProblem; // Amedo
    private long _fromStartCheckedAt = long.MinValue;

    // Amedo
    private string? CachedPlayFromStartProblem()
    {
        long now = Environment.TickCount64;
        if (now - _fromStartCheckedAt < 1000) return _fromStartProblem;
        _fromStartCheckedAt = now;
        return _fromStartProblem = PlayFromStartProblem();
    }

    private void StartSimulation(bool fromStart) // Amedo
    {
        if (Simulating) return;
        if (fromStart && PlayFromStartProblem() is { } problem) { _browser.Log("Play From Start: " + problem); return; } // Amedo
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var sharedSource = chunkRoot?.Get<ChunkSource>(); // Amedo 2026-09-29
        var settings = CurrentPlaySettings(sharedSource); // Amedo
        uint rngAtStart = CrashEngine.Core.GameRandom.Shared.State; // Amedo
        _simulation = chunkRoot is null ? null : StartSimOn(chunkRoot, settings, sharedSource); // Amedo
        if (_simulation is null) { _browser.Log("Play Scene: no level loaded."); return; }
        if (fromStart) // Amedo
        {
            if (settings.BootStraight) _simulation.World.BootToTitle(PlayLevelName);
            else _simulation.World.BootToTitle();
        }
        if (DebugBuild && _recordInput && PlayLevelName is { } tapeLevel) // Amedo
        {
            InputTape.Current?.Dispose();
            InputTape.Current = InputTape.StartRecording(InputTape.NewPath(tapeLevel), tapeLevel, "editor",
                [("mode", fromStart ? (settings.BootStraight ? "boot-straight" : "from-start") : "scene"), ("gamecamera", _simGameCamera ? "1" : "0"),
                 ("disc", _extractedRoot), ("saved", SavedChunksDir), ("rng", rngAtStart.ToString("X8"))]);
            _browser.Log($"Play Scene: recording input -> {InputTape.Current.Path} (Save Chunk first, so the replay loads the same level)");
        }
        RememberStartupChunks(); // Amedo 2026-10-02
        _playPanels = !_playMaximized; // Amedo 2026-10-26
        _simulation.Volume = _simVolume; // Amedo 2026-09-28
        _simulation.Muted = !_simSound;
        if (_simulation.StartAudio() is { } audioError) _browser.Log("Play Scene: sound output unavailable: " + audioError);
        var agents = _simulation.World.Agents;
        _browser.Log($"Play Scene: AgentLab running on {agents.Count(a => a.Behaviour is not null)} instance(s), " +
                     $"{agents.Count(a => a.Behaviour is null)} without a behaviour.");
    }

    // Amedo 2026-09-28
    private static ChunkSource? SourceOf(Entity? e)
    {
        for (; e is not null; e = e.Parent)
            if (e.Get<ChunkSource>() is { } src) return src;
        return null;
    }

    // Amedo 2026-09-28
    private object? ParseStreamedChunk(string name)
    {
        using var pkg = PackageReader.Open(_extractedRoot);
        pkg.ShadowDir = SavedChunksDir;
        return pkg.OpenByPath(name + ".rm2") is null ? null : ChunkImporter.ParseChunk(pkg, name + ".rm2", name + ".sm2");
    }

    private void StopSimulation()
    {
        if (_simulation is not { } sim) return;
        _simulation = null;
        if (_camera is not null) _camera.External = false; // Amedo 2026-09-28
        sim.Stop();
        Input.SetVibration(0f, 0f); // Amedo
        if (InputTape.Current is { Replaying: false } tape) // Amedo
        {
            InputTape.Current = null;
            tape.Dispose();
            _browser.Log($"Play Scene: input recording saved: {tape.Path} ({tape.Step + 1} logic steps, {(tape.Step + 1) * (double)SceneSimulation.LogicTicks / 9000.0:0.0} s)");
        }
        if (sim.World.Unhandled.Count > 0)
            _browser.Log("Play Scene: unimplemented opcodes hit (most used first): " + sim.UnhandledSummary());
        EndLevelSwitch(); // Amedo
        _browser.Log("Play Scene: stopped, scene restored.");
        CollectLeftovers();
        PrepareForPlay(); // Amedo 2026-10-02
    }

    private CrashEngine.Runtime.Streaming.ChunkHost? _playHost; // Amedo 2026-10-02
    private ChunkSource? _playHostSource;
    private Dictionary<string, List<string>>? _startupChunks;
    private string StartupChunksFile => Path.Combine(ModeDir("Cache"), "PlayChunks.json");
    private string? PlayLevelKey => Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Rm2Path.ToLowerInvariant();

    // Amedo 2026-10-02
    private CrashEngine.Runtime.Streaming.ChunkHost PlayHost(ChunkSource? sharedSource)
    {
        if (_playHost is not null && ReferenceEquals(_playHostSource, sharedSource)) return _playHost;
        DropPlayHost();
        _playHostSource = sharedSource;
        return _playHost = new CrashEngine.Runtime.Streaming.ChunkHost
        {
            Parse = ParseStreamedChunk,
            Build = (name, parsed) => ChunkImporter.BuildChunk(Engine.Instance.GL, null, (ChunkImporter.ParsedChunk)parsed, out _,
                applyGlobalEffects: false, linkedScenery: false, shared: sharedSource, editorExtras: false),
            BuildInBackground = true,
        };
    }

    // Amedo 2026-10-02
    private void DropPlayHost()
    {
        if (_playHost is { } host) SceneSimulation.DropPrepared(host);
        _playHost = null;
        _playHostSource = null;
    }

    // Amedo 2026-10-02
    private Dictionary<string, List<string>> StartupChunks()
    {
        if (_startupChunks is not null) return _startupChunks;
        try
        {
            _startupChunks = File.Exists(StartupChunksFile)
                ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(StartupChunksFile)) ?? new()
                : new();
        }
        catch { _startupChunks = new(); }
        return _startupChunks;
    }

    // Amedo 2026-10-02
    private void RememberStartupChunks()
    {
        if (_playHost is not { } host || PlayLevelKey is not { } key) return;
        var map = StartupChunks();
        if (map.TryGetValue(key, out var was) && was.SequenceEqual(host.StartupChunks)) return;
        map[key] = host.StartupChunks.ToList();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StartupChunksFile)!);
            File.WriteAllText(StartupChunksFile, System.Text.Json.JsonSerializer.Serialize(map, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { _browser.Log("Play Scene: couldn't remember the linked scenes: " + ex.Message); }
    }

    // Amedo 2026-10-02
    private void PrepareForPlay()
    {
        if (Simulating || PlayLevelKey is not { } key || !StartupChunks().TryGetValue(key, out var names) || names.Count == 0) return;
        var source = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
        SceneSimulation.PrepareBeforePlay(PlayHost(source), names, null);
    }

    private bool _openLeavePlayPopup; // Amedo 2026-09-28
    private bool _cancelLeavePlay;
    private bool _leavePlayOpen;
    private Action? _leaveTo;
    private const string LeavePlayPopup = "Play Scene is running##leaveplay";

    private bool _releaseGpu;

    protected override void OnDestroy()
    {
        if (!_releaseGpu) return;
        _releaseGpu = false;
        CrashEngine.Renderer.RenderPipeline.SkydomeDraws.Clear();
        int freed = CrashEngine.Renderer.GpuOwner.Release(this);
        _browser.Log($"Left the scene: freed {freed} GPU meshes and textures.");
        CollectLeftovers();
    }

    private static void CollectLeftovers()
    {
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    // Amedo 2026-09-28
    private void LeaveScene(Action? leave = null)
    {
        var go = leave ?? (() => Engine.Instance.ActiveScene = _browser); // Amedo 2026-10-02
        leave = () => { DropPlayHost(); _releaseGpu = true; go(); };
        if (!Simulating) { leave(); return; }
        if (_leavePlayOpen) { _cancelLeavePlay = true; return; }
        _leaveTo = leave;
        _openLeavePlayPopup = true;
    }

    // Amedo 2026-09-28
    private void DrawLeavePlayConfirmPopup()
    {
        if (_openLeavePlayPopup)
        {
            ImGui.OpenPopup(LeavePlayPopup);
            _openLeavePlayPopup = false;
        }
        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new System.Numerics.Vector2(0.5f, 0.5f));
        _leavePlayOpen = ImGui.BeginPopupModal(LeavePlayPopup, ImGuiWindowFlags.AlwaysAutoResize);
        if (!_leavePlayOpen) return;
        ImGui.TextWrapped("This scene is in Play mode. Do you want to stop it and leave?");
        ImGui.Spacing();
        if (ImGui.Button("Yes, stop and leave", new System.Numerics.Vector2(170f, 0f)))
        {
            ImGui.CloseCurrentPopup();
            _leavePlayOpen = false;
            StopSimulation();
            var leave = _leaveTo;
            _leaveTo = null;
            leave?.Invoke();
        }
        ImGui.SameLine();
        if (ImGui.Button("No, keep playing", new System.Numerics.Vector2(170f, 0f)) || _cancelLeavePlay)
        {
            ImGui.CloseCurrentPopup();
            _leavePlayOpen = false;
            _leaveTo = null;
        }
        _cancelLeavePlay = false;
        ImGui.EndPopup();
    }

    private void UpdateSimulation()
    {
        UpdateGameCameraOverlay(); // Amedo 2026-10-06
        if (_simulation is not { } sim) return;
        if (_leavePlayOpen) return; // Amedo 2026-09-28
        if (_camera is not null && !_simGameCamera) // Amedo 2026-10-06
        {
            var ev = _camera.View;
            sim.SetControlCamera(new System.Numerics.Vector3(ev.M11, ev.M21, ev.M31), new System.Numerics.Vector3(ev.M12, ev.M22, ev.M32),
                                 -new System.Numerics.Vector3(ev.M13, ev.M23, ev.M33));
        }
        else sim.ClearControlCamera();
        if (_camera is not null && !sim.ViewFromGameCamera(_camera.Aspect)) // Amedo 2026-10-06
        {
            sim.SetViewer(_camera.View * _camera.Projection, _camera.Transform.Position); // Amedo 2026-09-27
            var v = _camera.View;
            sim.SetCamera(new System.Numerics.Vector3(v.M11, v.M21, v.M31), new System.Numerics.Vector3(v.M12, v.M22, v.M32),
                          -new System.Numerics.Vector3(v.M13, v.M23, v.M33));
        }
        ReadPad(sim.Pad);
        ApplySimulatorTouch(sim.Pad); // Amedo 2026-10-26
        sim.Tick(EngineTime.Delta);
        if (_switchTo is not null) // Amedo
        {
            ApplyLevelSwitch();
            return;
        }
        Input.SetVibration(sim.Pad.LargeMotor_551 / 255f, sim.Pad.SmallMotor_550 ? 1f : 0f); // Amedo
        ApplyGameCamera(sim); // Amedo 2026-09-28
    }

    // Amedo 2026-09-28
    private void ApplyGameCamera(SceneSimulation sim)
    {
        if (_camera is null) return;
        sim.GameViewBars = _simGameCamera; // Amedo
        if (!_simGameCamera || sim.GameCamera is not { } gameCam || !sim.GameCameraView(out var eye, out var forward, out var up, out float fov))
        {
            _camera.External = false;
            return;
        }
        var (shownFov, shift) = sim.PresentGameView(fov, _camera.Aspect); // Amedo
        _camera.SetExternal(eye, forward, up, shownFov, gameCam.Near, gameCam.Far, shift);
    }

    // Amedo 2026-09-27
    private static void ReadPad(CrashEngine.Runtime.Character.CharacterPad pad)
    {
        pad.Clear();
        static byte On(bool held) => held ? (byte)255 : (byte)0;
        if (!ImGui.GetIO().WantCaptureKeyboard)
        {
            pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.Up]       = On(Input.KeyHeld(Key.Up));
            pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.Down]     = On(Input.KeyHeld(Key.Down));
            pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.Left]     = On(Input.KeyHeld(Key.Left));
            pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.Right]    = On(Input.KeyHeld(Key.Right));
            pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.Cross]    = On(Input.KeyHeld(Key.Space));
            pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.Square]   = On(Input.KeyHeld(Key.J));
            pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.Circle]   = On(Input.KeyHeld(Key.K));
            pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.Triangle] = On(Input.KeyHeld(Key.I));
            pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.L1]       = On(Input.KeyHeld(Key.U));
            pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.R1]       = On(Input.KeyHeld(Key.O));
            pad.Start = Input.KeyHeld(Key.Enter);
        }
        if (Input.Gamepad is not { } gp) return;
        foreach (var b in gp.Buttons)
        {
            if (!b.Pressed) continue;
            switch (b.Name)
            {
                case ButtonName.A:           pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.Cross] = 255; break;
                case ButtonName.X:           pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.Square] = 255; break;
                case ButtonName.B:           pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.Circle] = 255; break;
                case ButtonName.Y:           pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.Triangle] = 255; break;
                case ButtonName.LeftBumper:  pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.L1] = 255; break;
                case ButtonName.RightBumper: pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.R1] = 255; break;
                case ButtonName.DPadUp:      pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.Up] = 255; break;
                case ButtonName.DPadDown:    pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.Down] = 255; break;
                case ButtonName.DPadLeft:    pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.Left] = 255; break;
                case ButtonName.DPadRight:   pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.Right] = 255; break;
                case ButtonName.Start:       pad.Start = true; break;
                case ButtonName.Back:        pad.Select = true; break;
                case ButtonName.LeftStick:   pad.L3 = true; break;
                case ButtonName.RightStick:  pad.R3 = true; break;
            }
        }
        if (gp.Triggers.Count > 0 && gp.Triggers[0].Position > 0.5f) pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.L2] = 255;
        if (gp.Triggers.Count > 1 && gp.Triggers[1].Position > 0.5f) pad.Pressure[CrashEngine.Runtime.Character.CharacterPad.R2] = 255;
        if (gp.Thumbsticks.Count > 0) pad.SetHostStick(gp.Thumbsticks[0].X, -gp.Thumbsticks[0].Y);
        if (gp.Thumbsticks.Count > 1) pad.SetHostStick(gp.Thumbsticks[1].X, -gp.Thumbsticks[1].Y, right: true);
    }

    private void DrawSimulationButtons()
    {
        if (_simulation is not { } sim)
        {
            DrawPlayButton();
            return;
        }
        DrawSimulationScreen(sim);
        if (!DrawStopButton()) return;
        ImGui.SameLine();
        DrawGameViewControls(); // Amedo 2026-10-26
        ImGui.SameLine();
        DrawPlayStatus(sim);
    }

    // Amedo 2026-10-26
    private void DrawPlayButton()
    {
        var fromStartProblem = _playFromStart ? CachedPlayFromStartProblem() : null; // Amedo
        ImGui.BeginDisabled(fromStartProblem is not null);
        if (ImGui.SmallButton((_playFromStart ? "Play From Start" : "Play") + "##playscenebtn")) StartSimulation(_playFromStart); // Amedo
        ImGui.EndDisabled();
        ImGui.SameLine(0f, 1f); // Amedo
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(ImGui.GetStyle().FramePadding.X, 0f));
        if (ImGui.ArrowButton("##playmodebtn", ImGuiDir.Down)) ImGui.OpenPopup("##playmodepopup");
        ImGui.PopStyleVar();
        if (ImGui.BeginPopup("##playmodepopup"))
        {
            if (ImGui.Selectable("Play", !_playFromStart)) _playFromStart = false;
            if (ImGui.Selectable("Play From Start", _playFromStart)) _playFromStart = true;
            if (DebugBuild) // Amedo
            {
                ImGui.Separator();
                ImGui.MenuItem("Record Input", null, ref _recordInput);
            }
            ImGui.EndPopup();
        }
    }

    // Amedo 2026-10-26
    private void DrawSimulationScreen(SceneSimulation sim)
    {
        var viewSize = new System.Numerics.Vector2(ViewW, ViewH); // Amedo
        sim.DrawScreen(ImGui.GetBackgroundDrawList(), ViewOrigin, viewSize); // Amedo 2026-09-30
        DrawGameCameraLabel(sim); // Amedo 2026-10-06
        if (sim.World.FadeAlpha > 0f) // Amedo 2026-09-28
            ImGui.GetBackgroundDrawList().AddRectFilled(ViewOrigin, ViewOrigin + viewSize, // Amedo
                ImGui.GetColorU32(new System.Numerics.Vector4(0f, 0f, 0f, sim.World.FadeAlpha)));
        if (_simPerf) DrawPerfWindow(sim);
    }

    // Amedo 2026-10-26
    private bool DrawStopButton()
    {
        if (ImGui.SmallButton("Stop##stopscenebtn")) { StopSimulation(); return false; } // Amedo 2026-09-27
        if (InputTape.Current is { Replaying: false }) // Amedo
        {
            ImGui.SameLine();
            ImGui.TextColored(new System.Numerics.Vector4(1f, 0.3f, 0.3f, 1f), "REC");
        }
        return true;
    }
}
