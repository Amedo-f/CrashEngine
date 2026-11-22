using System.Numerics;
using System.Reflection;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Runtime;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;

namespace CrashGame;

// Amedo 2026-09-30
sealed class PlayScene : Scene
{
    private readonly GameConfig _cfg;
    private readonly PerfLog _perf = new();
    private CameraComponent _cam = null!;
    private SceneSimulation? _sim;
    private bool _showPerf = true;
    private SystemMonitor? _monitor;
    private string _gpuName = "GPU";
    private long _vramTotal = -1;
    private string[] _overlay = Array.Empty<string>();
    private double _overlayAt = -1;
    private bool _shutDown;
    private CrashEngine.Runtime.AgentLab.AgentLabWorld? _from; // Amedo
    private string? _switchTo; // Amedo
    private bool _switching; // Amedo
    private bool _collectLastLevel; // Amedo 2026-11-19
    public static Func<string[]>? OverlayLines; // Amedo 2026-10-26
    public static float OverlayScale = 1f; // Amedo 2026-10-26
    public static Vector2 OverlayPosition = new(10f, 10f); // Amedo 2026-10-26
    public static Action<ImDrawListPtr, Vector2>? ScreenOverlay; // Amedo 2026-10-26
    public static int LastDraws => PerfLog.LastDraws; // Amedo 2026-10-27
    public static volatile string? ShotPath;

    public PlayScene(GameConfig cfg, CrashEngine.Runtime.AgentLab.AgentLabWorld? from = null, PreloadedLevel? preloaded = null) // Amedo
    {
        _cfg = cfg;
        _from = from;
        _preloaded = preloaded; // Amedo 2026-11-09
    }

    // Amedo 2026-11-09
    public sealed record PreloadedLevel(Entity Root, GpuUploadQueue Uploads, CrashEngine.Runtime.Streaming.ChunkHost? Host);

    private readonly PreloadedLevel? _preloaded; // Amedo 2026-11-09
    private Task<PreloadedLevel?>? _preloadMain; // Amedo 2026-11-09
    private CrashEngine.Runtime.Streaming.ChunkHost? _preloadHost; // Amedo 2026-11-09
    private System.Diagnostics.Stopwatch? _preloadClock; // Amedo 2026-11-09
    private CrashEngine.Runtime.Streaming.ChunkHost? _host; // Amedo 2026-11-09
    private static readonly Dictionary<string, List<string>> StartupChunksByLevel = new(StringComparer.OrdinalIgnoreCase); // Amedo 2026-11-09

    protected override void Build()
    {
        var built = System.Diagnostics.Stopwatch.StartNew(); // Amedo 2026-11-09
        var closer = new Entity("Shutdown");
        closer.Add(new ShutdownHook { OnClose = Shutdown });
        AddRoot(closer);
        SetupWindow();
        GraphicsSettings.Changed += OnGraphicsChanged; // Amedo

        var gl = Engine.Instance.GL;
        _monitor = OperatingSystem.IsWindows() ? new SystemMonitor() : null; // Amedo 2026-10-26
        _gpuName = gl.GetStringS(Silk.NET.OpenGL.StringName.Renderer)?.Split('/')[0] ?? "GPU";
        gl.GetError();
        gl.GetInteger((Silk.NET.OpenGL.GLEnum)0x9048, out int vramKb);
        if (gl.GetError() == Silk.NET.OpenGL.GLEnum.NoError && vramKb > 0) _vramTotal = vramKb * 1024L;
        var pipeEnt = new Entity("RenderPipeline");
        var pipe = pipeEnt.Add(new RenderPipeline());
        pipeEnt.Add(new GameShadowPass());
        AddRoot(pipeEnt);

        var camEnt = new Entity("Camera");
        _cam = camEnt.Add(new CameraComponent());
        _cam.Far = 20000f;
        pipe.Camera = _cam;
        AddRoot(camEnt);

        Entity chunk;
        ChunkImporter.HdTextureDir = _cfg.HdTextureDirectory; // Amedo 2026-10-02
        SmoothNormalCache.Directory = Path.Combine(AppContext.BaseDirectory, "Cache", "SmoothNormals"); // Amedo
        GameShadowDefs.CratesReceiveShadows = ProjectPlayValues.FromTson(_cfg.Tson).CratesReceiveShadows; // Amedo
        if (_preloaded is { } ready) // Amedo 2026-11-09
        {
            chunk = ready.Root;
            ready.Uploads.Upload(double.PositiveInfinity);
            if (chunk.Get<ChunkSource>()?.Sm2 is { } readySm2) MeshDecoder.ApplyGlobalEffects(readySm2, chunk);
        }
        else
            using (var pkg = PackageReader.Open(_cfg.Disc))
            {
                pkg.ShadowDir = _cfg.SavedChunks;
                var parsed = ChunkImporter.ParseChunk(pkg, _cfg.Level + ".rm2", _cfg.Level + ".sm2");
                chunk = OnLevelGpu(() => ChunkImporter.BuildChunk(gl, pkg, parsed, out _, editorExtras: false));
            }
        if (MeshDecoder.LastFogColor is { } fog) pipe.FogColor = fog;
        if (MeshDecoder.LastFogColorIndex is { } fogIndex) pipe.FogIndex = fogIndex;
        var src = chunk.Get<ChunkSource>();
        chunk.Add(new GameLightingEnvironment { Scenery = src?.Sm2?.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM) });
        chunk.Add(new GameShadowCasters());
        AddRoot(chunk);
        _levelRoot = chunk; // Amedo 2026-11-09
        OnLevelGpu(() => AddParticles(gl, chunk, src));

        var shared = src;
        var host = _host = _preloaded?.Host ?? MakeHost(gl, shared, _cfg); // Amedo 2026-11-15
        var play = PlaySettings.Resolve(ProjectPlayValues.FromTson(_cfg.Tson), _cfg.Disc, _cfg.ProjectFolder, src?.GlobalRm2, Path.Combine(AppContext.BaseDirectory, "Saves")); // Amedo
        _sim = SceneSimulation.Start(chunk, Console.WriteLine, play,
            spawnHost: (parent, inst, section) => OnLevelGpu(() =>
            {
                var entity = ChunkImporter.ImportInstance(inst, parent, new Dictionary<uint, CrashEngine.Stealth.PatrolPath>(), section);
                if (SourceOf(parent)?.MeshTables is { } tables) MeshDecoder.BuildMeshForInstance(gl, tables, entity);
                return entity;
            }),
            groupHost: (e, ogi, anim, same) => SourceOf(e)?.MeshTables is { } tables ? OnLevelGpu(() => MeshDecoder.BuildForeignGroup(gl, tables, e, ogi, anim, same)) : null,
            chunkHost: host, continuesLoading: _from is not null); // Amedo 2026-11-16
        if (_sim is null) throw new InvalidOperationException($"Could not start the level {_cfg.Level}");
        StartupChunksByLevel[_cfg.Level] = host.StartupChunks.ToList(); // Amedo 2026-11-09
        _sim.World.LevelSwitch = (chunk, _) => _switchTo = chunk; // Amedo
        _perf.GameClock = () => _sim?.World.Now ?? 0; // Amedo
        if (_from is { } from) // Amedo
        {
            _from = null; // the last level's world is only needed for what carries over
            _sim.World.ContinueLevelSwitch(from);
            _collectLastLevel = true; // Amedo 2026-11-19
        }
        else
        {
            _sim.World.BootToTitle(play.BootStraight ? _cfg.Level : null);
            _collectLastLevel = true; // Amedo 2026-11-21
        }
        if (_cfg.RecordInput && InputTape.Current is null) // Amedo
        {
            InputTape.Current = InputTape.StartRecording(InputTape.NewPath(_cfg.Level), _cfg.Level, "game",
                [("mode", play.BootStraight ? "boot-straight" : "from-start"),
                 ("disc", _cfg.Disc), ("saved", _cfg.SavedChunks)]);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => InputTape.Current?.Dispose();
            Console.WriteLine($"[CrashGame] recording input -> {InputTape.Current.Path}");
        }
        _sim.Volume = _cfg.Volume;
        if (_sim.StartAudio() is { } audioError) Console.WriteLine("[CrashGame] sound output unavailable: " + audioError);
        Console.WriteLine($"[CrashGame] playing {_cfg.Level}: {_sim.World.Agents.Count} agents (scene ready in {built.ElapsedMilliseconds} ms{(_preloaded is null ? "" : ", built in the background")})");
        if (src is not null) _sim.ReleaseDiscGraphics(_cfg.Level, src); // Amedo 2026-11-20
    }

    // Amedo 2026-11-09
    private static CrashEngine.Runtime.Streaming.ChunkHost MakeHost(Silk.NET.OpenGL.GL gl, ChunkSource? shared, GameConfig cfg) => new() // Amedo 2026-11-15
    {
        Parse = name => ParseStreamed(cfg, name),
        Build = (name, parsed) => ChunkImporter.BuildChunk(gl, null, (ChunkImporter.ParsedChunk)parsed, out _,
            applyGlobalEffects: false, linkedScenery: false, shared: shared, editorExtras: false),
        BuildInBackground = true,
    };

    private const double UploadBudgetMs = 3.0; // Amedo 2026-11-09
    private Entity? _levelRoot; // Amedo 2026-11-09

    // Amedo 2026-11-09
    private void UnloadForLevelSwitch()
    {
        long before = GpuStats.TextureBytes + GpuStats.MeshBytes;
        _sim?.UnloadForLevelSwitch();
        if (_levelRoot is { } root) root.Active = false;
        _levelGpu.DisposeAll();
        Console.WriteLine($"[CrashGame] this level unloaded before the next one loads: GPU {before / 1048576} -> {(GpuStats.TextureBytes + GpuStats.MeshBytes) / 1048576} MB");
    }

    // Amedo 2026-11-19
    private void CollectLastLevel()
    {
        _collectLastLevel = false;
        Collect("last level");
    }

    // Amedo 2026-11-20
    private const long HeldGarbageBytes = 32L << 20;
    private long _liveAfterCollect;
    private int _lastRunState = 1;
    private long _backgroundIndex = -1; // Amedo 2026-11-21

    // Amedo 2026-11-20
    private void CollectWhenHeld(SceneSimulation sim)
    {
        int runState = sim.World.RunState_309718;
        var finished = GC.GetGCMemoryInfo(GCKind.Background); // Amedo 2026-11-21
        if (_backgroundIndex >= 0 && finished.Index > _backgroundIndex)
        {
            _liveAfterCollect = finished.HeapSizeBytes - finished.FragmentedBytes;
            _backgroundIndex = -1;
        }
        if (runState == 3 && _lastRunState != 3 && GC.GetTotalMemory(false) - Math.Min(LiveAfterLastFullGc(), _liveAfterCollect) >= HeldGarbageBytes) CollectInBackground(); // Amedo 2026-11-21
        _lastRunState = runState;
    }

    // Amedo 2026-11-21
    private static long LiveAfterLastFullGc()
    {
        var background = GC.GetGCMemoryInfo(GCKind.Background);
        var blocking = GC.GetGCMemoryInfo(GCKind.FullBlocking);
        var last = background.Index > blocking.Index ? background : blocking;
        return last.Index == 0 ? 0 : last.HeapSizeBytes - last.FragmentedBytes;
    }

    // Amedo 2026-11-21
    private void CollectInBackground()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        long heap = GC.GetTotalMemory(false), live = Math.Min(LiveAfterLastFullGc(), _liveAfterCollect); // Amedo 2026-11-21
        _backgroundIndex = GC.GetGCMemoryInfo(GCKind.Background).Index;
        GC.Collect(2, GCCollectionMode.Forced, blocking: false, compacting: false);
        Console.WriteLine($"[CrashGame] world held: background collect started in {watch.Elapsed.TotalMilliseconds:0.0} ms (C# {heap / 1048576} MB, alive after the last own / full collect {live / 1048576} MB)");
    }

    // Amedo 2026-11-20
    private void Collect(string why)
    {
        long heap = GC.GetTotalMemory(false), before, setBefore;
        using (var self = System.Diagnostics.Process.GetCurrentProcess()) { before = self.PrivateMemorySize64; setBefore = self.WorkingSet64; } // Amedo 2026-11-20
        var watch = System.Diagnostics.Stopwatch.StartNew();
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers(); // Amedo 2026-11-20
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true); // Amedo 2026-11-20
        _liveAfterCollect = GC.GetTotalMemory(false);
        using var after = System.Diagnostics.Process.GetCurrentProcess(); // Amedo 2026-11-20
        Console.WriteLine($"[CrashGame] {why} collected in {watch.ElapsedMilliseconds} ms: C# {heap / 1048576} -> {_liveAfterCollect / 1048576} MB, process memory {before / 1048576} -> {after.PrivateMemorySize64 / 1048576} MB, in use {setBefore / 1048576} -> {after.WorkingSet64 / 1048576} MB");
    }

    // Amedo 2026-11-09
    private bool LevelReady(string next)
    {
        var gl = Engine.Instance.GL;
        if (_preloadMain is null)
        {
            var cfg = _cfg.ForLevel(next);
            _preloadClock = System.Diagnostics.Stopwatch.StartNew();
            UnloadForLevelSwitch(); // Amedo 2026-11-09
            Console.WriteLine($"[CrashGame] loading {next} in the background (the loading screen keeps running)");
            _preloadMain = SceneSimulation.OnBuildLane<PreloadedLevel>(() =>
            {
                try
                {
                    using var pkg = PackageReader.Open(cfg.Disc);
                    pkg.ShadowDir = cfg.SavedChunks;
                    var parsed = ChunkImporter.ParseChunk(pkg, cfg.Level + ".rm2", cfg.Level + ".sm2");
                    var uploads = new GpuUploadQueue();
                    Entity root;
                    using (uploads.Begin()) root = ChunkImporter.BuildChunk(gl, pkg, parsed, out _, applyGlobalEffects: false, editorExtras: false);
                    uploads.Warm();
                    return new PreloadedLevel(root, uploads, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CrashGame] background load of {cfg.Level} failed, loading it the old way: {ex.GetType().Name}: {ex.Message}");
                    return null;
                }
            });
            return false;
        }
        if (!_preloadMain.IsCompleted) return false;
        if (_preloadMain.Result is not { } main) return true;
        if (_preloadHost is null)
        {
            _preloadHost = MakeHost(gl, main.Root.Get<ChunkSource>(), _cfg.ForLevel(next)); // Amedo 2026-11-15
            if (StartupChunksByLevel.TryGetValue(_cfg.ForLevel(next).Level, out var names) && names.Count > 0)
                SceneSimulation.PrepareBeforePlay(_preloadHost, names, Console.WriteLine);
            Console.WriteLine($"[CrashGame] {next} built in the background in {_preloadClock!.ElapsedMilliseconds} ms (process memory {System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 1048576} MB, GPU {(GpuStats.TextureBytes + GpuStats.MeshBytes) / 1048576} MB); linked scenes to prepare: {(names is { Count: > 0 } ? string.Join(",", names) : "none known yet")}");
            return false;
        }
        if (!main.Uploads.Upload(UploadBudgetMs)) return false;
        lock (_preloadHost.Prepared)
            if (_preloadHost.Prepared.Values.Any(t => !t.IsCompleted)) return false;
        return true;
    }

    private static object? ParseStreamed(GameConfig cfg, string name) // Amedo 2026-11-15
    {
        using var pkg = PackageReader.Open(cfg.Disc);
        pkg.ShadowDir = cfg.SavedChunks;
        return pkg.OpenByPath(name + ".rm2") is null ? null : ChunkImporter.ParseChunk(pkg, name + ".rm2", name + ".sm2");
    }

    private static ChunkSource? SourceOf(Entity? e)
    {
        for (; e is not null; e = e.Parent)
            if (e.Get<ChunkSource>() is { } src) return src;
        return null;
    }

    private static void AddParticles(Silk.NET.OpenGL.GL gl, Entity chunk, ChunkSource? src)
    {
        var fxRoot = new Entity("ParticleEffects");
        var effects = fxRoot.Add(new ParticleEffectsRenderer());
        if (src?.GlobalRm2 is { } global)
        {
            effects.Pages = MeshDecoder.GetParticleTexturePages(gl, global);
            effects.Decals = MeshDecoder.GetDecalAssets(gl, global);
        }
        chunk.AddChild(fxRoot);
        var markers = new Entity("ParticleEmitters") { Active = false };
        chunk.AddChild(markers);
        if (src?.Rm2?.GetItem<PS2AnyParticleData>((uint)Constants.LEVEL_PARTICLES_ITEM) is not { } data) return;
        foreach (var em in data.ParticleEmitters)
        {
            var name = ParticleNames.Clean(em.Name);
            if (string.IsNullOrWhiteSpace(name)) continue;
            var sys = MeshDecoder.FindParticleSystem(src.Rm2, name) ?? (src.GlobalRm2 is { } g ? MeshDecoder.FindParticleSystem(g, name) : null);
            if (sys is null) continue;
            var marker = new Entity($"ParticleEmitter_{name}");
            marker.Transform.Position = new Vector3(em.Position.X, em.Position.Y, em.Position.Z);
            markers.AddChild(marker);
            effects.AddEmitter(marker, em, sys);
        }
    }

    private void SetupWindow()
    {
        if (typeof(Engine).GetField("_win", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(Engine.Instance) is not IWindow win) return;
        _window = win;
        win.Title = "Crash Twinsanity";
        if (!Windowed && win.Monitor is { } monitor) // Amedo
        {
            win.WindowBorder = WindowBorder.Hidden;
            win.Position = monitor.Bounds.Origin;
            win.Size = WindowSize(monitor); // Amedo
        }
        if (!_cfg.Hidden) win.IsVisible = true;
    }

    private IWindow? _window;
    private bool Windowed => _cfg.Windowed || !GraphicsSettings.Current.Fullscreen; // Amedo

    // Amedo
    private void OnGraphicsChanged(GraphicsSettings old, GraphicsSettings now)
    {
        if (old.Fullscreen == now.Fullscreen || _cfg.Windowed || _window is not { Monitor: { } monitor } win) return;
        if (now.Fullscreen)
        {
            _fullscreenChecks = 120;
            KeepFullscreen();
            return;
        }
        var screen = ScreenSize(monitor);
        var size = new Silk.NET.Maths.Vector2D<int>(screen.X * 2 / 3, screen.Y * 2 / 3);
        win.WindowBorder = WindowBorder.Resizable;
        win.Size = size;
        win.Position = monitor.Bounds.Origin + (screen - size) / 2;
        Console.WriteLine($"[CrashGame] windowed {size.X}x{size.Y}");
    }
    private int _fullscreenChecks = 120;

    private int _fullscreenLogs;

    private static Silk.NET.Maths.Vector2D<int> ScreenSize(IMonitor monitor) => monitor.VideoMode.Resolution ?? monitor.Bounds.Size;

    private static Silk.NET.Maths.Vector2D<int> WindowSize(IMonitor monitor) => ScreenSize(monitor) + new Silk.NET.Maths.Vector2D<int>(0, 1); // Amedo

    private void KeepFullscreen()
    {
        if (Windowed || _window is not { Monitor: { } monitor } win || _fullscreenChecks <= 0) return; // Amedo
        _fullscreenChecks--;
        var bounds = monitor.Bounds;
        var screen = WindowSize(monitor); // Amedo
        var fb = win.FramebufferSize;
        if (_fullscreenChecks == 115)
            Console.WriteLine($"[CrashGame] window {win.Size.X}x{win.Size.Y} framebuffer {fb.X}x{fb.Y} at {win.Position.X},{win.Position.Y} state {win.WindowState} border {win.WindowBorder}; monitor {screen.X}x{screen.Y} (work area {bounds.Size.X}x{bounds.Size.Y}) at {bounds.Origin.X},{bounds.Origin.Y}");
        if (win.Size == screen && win.Position == bounds.Origin && fb == screen) return;
        if (win.WindowState != WindowState.Normal) win.WindowState = WindowState.Normal;
        win.WindowBorder = WindowBorder.Hidden;
        win.Position = bounds.Origin;
        win.Size = screen;
        if (_fullscreenLogs++ < 3)
            Console.WriteLine($"[CrashGame] fix fullscreen: window {win.Size.X}x{win.Size.Y} framebuffer {win.FramebufferSize.X}x{win.FramebufferSize.Y} -> monitor {screen.X}x{screen.Y}");
    }

    protected override void OnUpdate()
    {
        _perf.Frame();
        KeepFullscreen();
        if (_collectLastLevel) CollectLastLevel(); // Amedo 2026-11-19
        if (Input.KeyDown(Key.Escape) || (_cfg.QuitAfter > 0 && EngineTime.Total >= _cfg.QuitAfter))
        {
            Engine.Instance.Quit();
            return;
        }
        if (Input.KeyDown(Key.F1)) _showPerf = !_showPerf;
        if (Input.KeyDown(Key.F9)) MemoryReport.Check();
        if (_sim is not { } sim) return;
        sim.SetViewer(_cam.View * _cam.Projection, _cam.Transform.Position);
        var v = _cam.View;
        sim.SetCamera(new Vector3(v.M11, v.M21, v.M31), new Vector3(v.M12, v.M22, v.M32), -new Vector3(v.M13, v.M23, v.M33));
        PadInput.Read(sim.Pad);
        sim.Tick(EngineTime.Delta);
        CollectWhenHeld(sim); // Amedo 2026-11-20
        if (_switchTo is { } next && !_switching && LevelReady(next)) // Amedo 2026-11-09
        {
            _switching = true;
            Console.WriteLine($"[CrashGame] switching level {_cfg.Level} -> {next}{(_preloadClock is { } clock ? $" after {clock.ElapsedMilliseconds} ms of loading" : "")} (process memory {System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 1048576} MB, GPU {(GpuStats.TextureBytes + GpuStats.MeshBytes) / 1048576} MB)");
            InputTape.Current?.Note('w', next); // Amedo
            var preloaded = _preloadMain?.Result is { } main ? main with { Host = _preloadHost } : null; // Amedo 2026-11-09
            var swap = System.Diagnostics.Stopwatch.StartNew(); // Amedo 2026-11-09
            Engine.Instance.ActiveScene = new PlayScene(_cfg.ForLevel(next), sim.World, preloaded);
            Console.WriteLine($"[CrashGame] the switch frame took {swap.ElapsedMilliseconds} ms (old level stopped + new one started)"); // Amedo 2026-11-09
            return;
        }
        Input.SetVibration(sim.Pad.LargeMotor_551 / 255f, sim.Pad.SmallMotor_550 ? 1f : 0f); // Amedo
        if (sim.GameCamera is { } gameCam && sim.GameCameraView(out var eye, out var forward, out var up, out float fov))
        {
            var (shownFov, shift) = sim.PresentGameView(fov, _cam.Aspect); // Amedo
            _cam.SetExternal(eye, forward, up, shownFov, gameCam.Near, gameCam.Far, shift);
        }
        else _cam.External = false;
    }

    public override void OnImGuiRender()
    {
        if (_cfg.ShotAt > 0 && !_shotTaken && EngineTime.Total >= _cfg.ShotAt)
        {
            _shotTaken = true;
            SaveShot(Path.Combine(AppContext.BaseDirectory, $"shot_{GamePaths.FileName(_cfg.Level)}_{DateTime.Now:yyyyMMdd_HHmmss}.png"));
        }
        if (ShotPath is { } shot)
        {
            ShotPath = null;
            SaveShot(shot);
        }
        _sim?.DrawScreen(ImGui.GetBackgroundDrawList(), Vector2.Zero, ImGui.GetIO().DisplaySize); // Amedo 2026-09-30
        if (_sim is { } sim && sim.World.FadeAlpha > 0f)
            ImGui.GetBackgroundDrawList().AddRectFilled(Vector2.Zero, ImGui.GetIO().DisplaySize,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, sim.World.FadeAlpha)));
        ScreenOverlay?.Invoke(ImGui.GetBackgroundDrawList(), ImGui.GetIO().DisplaySize); // Amedo 2026-10-26
        if (!_showPerf) return;
        if (EngineTime.Total - _overlayAt >= 0.25 || _overlayAt < 0) RefreshOverlay();
        ImGui.SetNextWindowPos(OverlayPosition); // Amedo 2026-10-26
        ImGui.SetNextWindowBgAlpha(0.6f);
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoInputs |
                                       ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav;
        if (ImGui.Begin("##perf", flags))
        {
            ImGui.SetWindowFontScale(OverlayScale); // Amedo 2026-10-26
            foreach (var line in _overlay) ImGui.TextUnformatted(line);
        }
        ImGui.End();
    }

    private static string Pct(float v) => v < 0 ? "--" : $"{v:0}%";
    private static string Mb(long bytes) => bytes < 0 ? "--" : $"{bytes / 1048576.0:0} MB";
    private static string Gb(long bytes) => $"{bytes / 1073741824.0:0.0} GB";

    private void RefreshOverlay()
    {
        _overlayAt = EngineTime.Total;
        if (OverlayLines is { } custom) // Amedo 2026-10-26
        {
            _overlay = custom();
            return;
        }
        var m = _monitor;
        string vram = m is null ? "--" : _vramTotal > 0
            ? $"VRAM game {Mb(m.VramGame)}, used {Gb(Math.Max(0, m.VramUsed))} of {Gb(_vramTotal)}"
            : $"VRAM game {Mb(m.VramGame)}, used {Mb(m.VramUsed)}";
        _overlay = new[]
        {
            $"FPS {PerfStats.Fps:0}   frame {PerfStats.FrameMs:0.00} ms   worst {PerfStats.FramePeakMs:0.0} ms   missed {PerfStats.MissedPerSecond} (10 s: {PerfStats.MissedLast10s})   {Engine.FrameModeName} (F3)", // Amedo 2026-11-14
            $"CPU  game {Pct(m?.CpuGame ?? -1)}   all apps {Pct(m?.CpuTotal ?? -1)}   {m?.CpuName}",
            $"GPU  game {Pct(m?.GpuGame ?? -1)}   all apps {Pct(m?.GpuTotal ?? -1)}   {_gpuName}",
            $"     {vram}",
            m is null || m.RamTotal == 0 ? "RAM  --" : $"RAM  game {Mb(m.RamGame)}   all apps {Gb(m.RamUsed)} of {Gb(m.RamTotal)} ({100.0 * m.RamUsed / m.RamTotal:0}%)",
            $"work: update {PerfStats.Ms(PerfSection.Update):0.00} ms   render {PerfStats.Ms(PerfSection.Render):0.00} ms   draws {PerfLog.LastDraws}   (F1 hides)",
            MemoryReport.Line(_sim),
            MemoryReport.LastCheck,
        };
    }

    private bool _shotTaken;

    private static unsafe void SaveShot(string path)
    {
        var gl = Engine.Instance.GL;
        int w = Engine.Instance.Width, h = Engine.Instance.Height;
        var px = new byte[w * h * 4];
        gl.BindFramebuffer(Silk.NET.OpenGL.FramebufferTarget.Framebuffer, Engine.Instance.TargetFramebuffer);
        gl.PixelStore(Silk.NET.OpenGL.PixelStoreParameter.PackAlignment, 1);
        fixed (byte* p = px) gl.ReadPixels(0, 0, (uint)w, (uint)h, Silk.NET.OpenGL.PixelFormat.Rgba, Silk.NET.OpenGL.PixelType.UnsignedByte, p);
        var rows = new byte[px.Length];
        for (int y = 0; y < h; y++) Buffer.BlockCopy(px, (h - 1 - y) * w * 4, rows, y * w * 4, w * 4);
        for (int i = 3; i < rows.Length; i += 4) rows[i] = 255;
        using var fs = File.Create(path);
        new StbImageWriteSharp.ImageWriter().WritePng(rows, w, h, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, fs);
        Console.WriteLine($"[CrashGame] screenshot {path}");
    }

    private void Shutdown()
    {
        if (_shutDown) return;
        _shutDown = true;
        GraphicsSettings.Changed -= OnGraphicsChanged; // Amedo
        RefreshOverlay();
        foreach (var line in _overlay) Console.WriteLine("[overlay] " + line);
        if (!_switching) _perf.Write(_cfg.Level, Engine.Instance.Width, Engine.Instance.Height, _monitor, _gpuName); // Amedo
        _monitor?.Dispose();
        _sim?.Stop();
        _levelGpu.DisposeAll();
        _preloaded?.Uploads.DisposeAll(); // Amedo 2026-11-09
        if (_host is { } host) SceneSimulation.DropPrepared(host); // Amedo 2026-11-09
        Input.SetVibration(0f, 0f); // Amedo
        _sim = null;
    }

    private readonly GpuUploadQueue _levelGpu = new();

    private T OnLevelGpu<T>(Func<T> build)
    {
        if (GpuUploadQueue.Recording) return build();
        T made;
        using (_levelGpu.Begin()) made = build();
        _levelGpu.Upload(double.PositiveInfinity);
        return made;
    }

    private void OnLevelGpu(Action build) => OnLevelGpu(() => { build(); return 0; });

    private sealed class ShutdownHook : Component
    {
        public Action? OnClose;
        public override void OnDestroy() => OnClose?.Invoke();
    }
}
