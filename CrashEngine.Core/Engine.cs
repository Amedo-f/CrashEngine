using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace CrashEngine.Core;

public sealed class Engine
{
    public static Engine Instance { get; private set; } = null!;
    public static Action? UiStateReset { get; set; } // Amedo
    public static int MainThreadId { get; private set; } = -1; // Amedo 2026-09-29
    
    private readonly IView _win; // Amedo 2026-10-26
    private IWindow? Desktop => _win as IWindow; // Amedo 2026-10-26
    private GL      _gl    = null!;
    private ImGuiController? _imgui;
    private ImGuiRenderer? _imguiRenderer; // Amedo
    private IInputContext?   _input;
   
    public GL  GL   => _gl;
    public int    Width  { get; private set; }
    public int    Height { get; private set; }
    
    private Scene? _activeScene;
    private Scene? _pendingScene;
    
    public Scene? ActiveScene
    {
        get => _activeScene;
        set
        {
           if (_gl is not null) _pendingScene = value;
          else                 _activeScene  = value;
        }
    }
    
    private double _fixedAccum;
    public const double FixedStep = 1.0 / 200.0;
    
    public static Action<ImFontAtlasPtr>? CustomFontSetup;
    public static Action? CustomStyleSetup; // Amedo
    
    public Engine(string title = "CrashEngine", int w = 1280, int h = 720, bool visible = true)
    {
        Instance = this;
        _baseTitle = title; // Amedo
        MainThreadId = Environment.CurrentManagedThreadId; // Amedo 2026-09-29
        if (KeepFullSpeedInBackground) DisablePowerThrottling(); // Amedo 2026-09-30
        Width = w; Height = h;

        var api = GraphicsApi ?? DefaultGraphicsApi; // Amedo 2026-10-26
        Gles = api.API == ContextAPI.OpenGLES;
        if (Window.IsViewOnly) // Amedo 2026-10-26
        {
            _win = Window.GetView(ViewOptions.Default with
            {
                PreferredDepthBufferBits   = 24,
                PreferredStencilBufferBits = 8,
                API = api,
            });
        }
        else
        {
        var opts = WindowOptions.Default with
        {
            Title = title,
            Size  = new Silk.NET.Maths.Vector2D<int>(w, h),
            IsVisible = visible, // Amedo 2026-09-28
            PreferredDepthBufferBits   = 24, // Amedo 2026-09-26
            PreferredStencilBufferBits = 8, // Amedo 2026-09-26
            API   = api // Amedo 2026-10-26
        };

        _win = Window.Create(opts);
        }
        _win.Load              += OnLoad;
        _win.Update            += OnUpdate;
        _win.Render            += OnRender;
        _win.FramebufferResize += OnResize;
        _win.Closing           += OnClose;
    }
    
    // Amedo 2026-09-29
    public void Run()
    {
        _win.Initialize();
        _win.Run(Frame);
        _win.DoEvents();
        _imguiRenderer?.Dispose(); // Amedo
        _imgui?.Dispose(); // Amedo
        _input?.Dispose();
        _win.Reset();
        _limiter.Dispose();
    }

    // Amedo 2026-09-29
    private void Frame()
    {
        using (PerfStats.Measure(PerfSection.FrameWait)) _limiter.Wait(FrameCap); // Amedo
        using (PerfStats.Measure(PerfSection.WindowEvents)) _win.DoEvents(); // Amedo
        if (!_win.IsClosing) _win.DoUpdate();
        if (!_win.IsClosing) _win.DoRender();
        PerfStats.AddSinceFrameEnd(PerfSection.Present); // Amedo
    }

    public void Quit() => _win.Close();

    // Amedo 2026-10-26
    public static GraphicsAPI? GraphicsApi { get; set; }
    public static bool AlwaysVSync { get; set; } // Amedo 2026-10-26
    public bool Gles { get; }

    // Amedo 2026-10-26
    private static GraphicsAPI DefaultGraphicsApi => OperatingSystem.IsAndroid() || OperatingSystem.IsIOS()
        ? new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Compatability, ContextFlags.Default, new APIVersion(3, 2))
        : new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(4, 6));

    // Amedo
    private string _baseTitle = "CrashEngine";
    private string _titleDetail = "";
    public void SetTitleDetail(string detail)
    {
        if (detail == _titleDetail) return;
        _titleDetail = detail;
        if (Desktop is { } win) win.Title = detail.Length == 0 ? _baseTitle : $"{_baseTitle}  —  {detail}"; // Amedo 2026-10-26
    }

    public uint TargetFramebuffer { get; set; } // Amedo 2026-09-30

    public int ScreenWidth => ScreenMode.Resolution is { X: > 0 } r ? r.X : Width; // Amedo
    public int ScreenHeight => ScreenMode.Resolution is { Y: > 0 } r ? r.Y : Height; // Amedo
    private VideoMode ScreenMode => Desktop is { } win ? win.Monitor?.VideoMode ?? default : _win.VideoMode; // Amedo 2026-10-26

    public uint SceneFramebuffer { get; set; } // Amedo
    public int SceneWidth { get; set; }
    public int SceneHeight { get; set; }
    public int SceneSamples { get; set; } = 1;
    public static Func<GL, bool>? BeginScene { get; set; } // Amedo
    public static Action<GL>? EndScene { get; set; }

    public static Func<bool>? SceneHeld { get; set; }
    public static Func<GL, bool>? ReuseScene { get; set; }

    public static event Action? GpuContextStarted;

    // Amedo
    public (int X, int Y, int W, int H)? SceneViewport { get; set; }
    public int ViewWidth => SceneViewport?.W ?? Width;
    public int ViewHeight => SceneViewport?.H ?? Height;
    private (int W, int H) _viewSize;

    // Amedo 2026-09-30
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct PowerThrottlingState { public uint Version, ControlMask, StateMask; }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr process, int infoClass, ref PowerThrottlingState info, uint size);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    public static bool PowerThrottlingOff { get; private set; }
    public static bool KeepFullSpeedInBackground { get; set; } // Amedo 2026-09-30

    // Amedo 2026-09-30
    private static void DisablePowerThrottling()
    {
        if (!OperatingSystem.IsWindows()) return;
        var state = new PowerThrottlingState { Version = 1, ControlMask = 0x1 | 0x4, StateMask = 0 };
        try
        {
            PowerThrottlingOff = SetProcessInformation(GetCurrentProcess(), 4, ref state,
                (uint)System.Runtime.InteropServices.Marshal.SizeOf<PowerThrottlingState>());
        }
        catch { PowerThrottlingOff = false; }
        Console.WriteLine($"[Engine] Windows power throttling {(PowerThrottlingOff ? "disabled" : "unchanged")}");
    }

    // Amedo 2026-09-29
    private readonly FrameLimiter _limiter = new();
    private double _frameToast;
    private static readonly FrameMode[] FrameCycle = { FrameMode.VSync, FrameMode.Fps60, FrameMode.Fps144, FrameMode.Fps240, FrameMode.Fps360, FrameMode.Unlimited }; // Amedo
    public static FrameMode FrameMode => Array.Find(FrameCycle, m => SettingsFor(m) == GraphicsSettings.Current); // Amedo
    public static int FrameCap => GraphicsSettings.Current.FrameLimit; // Amedo
    public static string FrameModeName => GraphicsSettings.Current.FrameRateName; // Amedo
    public float FrameBudgetMs => 1000f / (FrameCap > 0 ? FrameCap : ScreenMode.RefreshRate is > 0 and var hz ? hz : 60); // Amedo 2026-10-26

    // Amedo
    private static GraphicsSettings SettingsFor(FrameMode mode) => GraphicsSettings.Current with
    {
        VSync = mode == FrameMode.VSync,
        FrameLimit = mode switch { FrameMode.Fps60 => 60, FrameMode.Fps144 => 144, FrameMode.Fps240 => 240, FrameMode.Fps360 => 360, _ => 0 },
    };

    // Amedo
    public void SetFrameMode(FrameMode mode, bool save = true) => GraphicsSettings.Set(SettingsFor(mode), save);

    // Amedo
    private void ApplyFrameSettings(GraphicsSettings old, GraphicsSettings now)
    {
        _win.VSync = now.VSync || AlwaysVSync; // Amedo 2026-10-26
        if (old.VSync != now.VSync || old.FrameLimit != now.FrameLimit) _frameToast = 1.5;
    }

    // Amedo 2026-09-29
    private void DrawFrameToast(double dt)
    {
        if (_frameToast <= 0) return;
        _frameToast -= dt;
        var text = $"Frame rate: {FrameModeName}  (F3)";
        var size = ImGui.CalcTextSize(text);
        var pos = new System.Numerics.Vector2((ImGui.GetIO().DisplaySize.X - size.X) * 0.5f, 48f);
        var draw = ImGui.GetForegroundDrawList();
        draw.AddRectFilled(pos - new System.Numerics.Vector2(10f, 6f), pos + size + new System.Numerics.Vector2(10f, 6f), 0xC0000000, 6f);
        draw.AddText(pos, 0xFFFFFFFF, text);
    }

    // Amedo 2026-09-22
    private (int W, int H, byte[] Rgba)[]? _pendingIcons;
    public void SetWindowIcon(params (int W, int H, byte[] Rgba)[] icons) => _pendingIcons = icons;

    private void OnLoad()
    {
        _gl    = GL.GetApi(_win);
        Console.WriteLine($"[Engine] OpenGL {_gl.GetStringS(StringName.Version)} on {_gl.GetStringS(StringName.Renderer)} ({_gl.GetStringS(StringName.Vendor)})"); // Amedo 2026-09-30
        GlCaps.Detect(_gl, Gles); // Amedo 2026-10-26
        GpuContextStarted?.Invoke();
        if (Desktop is null && _win.FramebufferSize is { X: > 0, Y: > 0 } fb) (Width, Height) = (fb.X, fb.Y); // Amedo 2026-10-26

        // Amedo 2026-09-22
        if (_pendingIcons is { Length: > 0 } && Desktop is { } win) // Amedo 2026-10-26
        {
            var raws = new Silk.NET.Core.RawImage[_pendingIcons.Length];
            for (int i = 0; i < raws.Length; i++)
                raws[i] = new Silk.NET.Core.RawImage(_pendingIcons[i].W, _pendingIcons[i].H, _pendingIcons[i].Rgba);
            try { win.SetWindowIcon(raws); } catch { }
        }

        _input = _win.CreateInput();
        Input.Initialize(_input);

        if (UseImGui) SetupImGui(); // Amedo 2026-10-26

        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(TriangleFace.Back);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _win.VSync = GraphicsSettings.Current.VSync || AlwaysVSync; // Amedo
        GraphicsSettings.Changed += ApplyFrameSettings;
        _activeScene?.Load(_gl);
    }

    // Amedo 2026-10-26
    public static bool UseImGui { get; set; } = true;

    // Amedo 2026-10-26
    private void SetupImGui()
    {
        _imgui = new ImGuiController(_gl, _win, _input, null,
            () => CustomFontSetup?.Invoke(ImGui.GetIO().Fonts));
        _imguiRenderer = new ImGuiRenderer(_gl); // Amedo
        var io = ImGui.GetIO();
        Win32Clipboard.Hook(io);

        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        // Amedo 2026-09-21
        io.ConfigWindowsResizeFromEdges = true;
        io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;

        ImGui.StyleColorsDark();
        var style = ImGui.GetStyle();
        style.WindowRounding   = 6f;
        style.FrameRounding    = 4f;
        style.ScrollbarRounding = 4f;
        style.WindowBorderSize = 1f;

        unsafe
        {
            var colors = style.Colors;
            colors[(int)ImGuiCol.WindowBg].W = 1f;
            colors[(int)ImGuiCol.ChildBg].W  = 1f;
            colors[(int)ImGuiCol.PopupBg].W  = 1f;
        }
        CustomStyleSetup?.Invoke(); // Amedo
        if (Gles) UploadImGuiFontGles(); // Amedo 2026-10-26
    }

    // Amedo 2026-10-26
    private unsafe void UploadImGuiFontGles()
    {
        var fonts = ImGui.GetIO().Fonts;
        fonts.GetTexDataAsRGBA32(out byte* pixels, out int w, out int h);
        uint tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)w, (uint)h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        fonts.SetTexID((nint)tex);
    }

    private void OnUpdate(double dt)
    {
        if (_pendingScene is not null)
        {
            _activeScene?.Destroy();
            _activeScene  = _pendingScene;
            _pendingScene = null;
            SceneViewport = null; // Amedo
            _viewSize = default;
            SetTitleDetail("");
            _activeScene.Load(_gl);
        }

        dt = EngineTime.FrameOverride ?? dt; // Amedo 2026-09-27
        EngineTime.Delta  = (float)(dt * EngineTime.TimeScale);
        EngineTime.Total += EngineTime.Delta;

        _fixedAccum += dt * EngineTime.TimeScale;
        while (_fixedAccum >= FixedStep)
        {
            EngineTime.FixedDelta = (float)FixedStep;
            _activeScene?.FixedUpdate();
            _fixedAccum -= FixedStep;
        }

        if (Input.KeyDown(Key.F3)) SetFrameMode(FrameCycle[(Array.IndexOf(FrameCycle, FrameMode) + 1) % FrameCycle.Length]); // Amedo
        using (PerfStats.Measure(PerfSection.Update)) _activeScene?.Update(); // Amedo 2026-09-28
        Input.EndFrame();
    }

    // Amedo 2026-09-28
    private readonly uint[] _gpuQueries = new uint[4];
    private readonly bool[] _gpuPending = new bool[4];
    private int _gpuSlot;
    private const int GpuDisjoint = 0x8FBB;

    // Amedo 2026-09-28
    private void BeginGpuTimer()
    {
        if (_gpuQueries[0] == 0) for (int i = 0; i < _gpuQueries.Length; i++) _gpuQueries[i] = _gl.GenQuery();
        int slot = _gpuSlot;
        if (Gles)
        {
            _gl.GetInteger((GetPName)GpuDisjoint, out int disjoint);
            if (disjoint != 0) Array.Clear(_gpuPending);
        }
        if (_gpuPending[slot])
        {
            _gl.GetQueryObject(_gpuQueries[slot], QueryObjectParameterName.ResultAvailable, out uint ready); // Amedo 2026-10-26
            if (ready != 0)
            {
                ulong ns = QueryNanoseconds(_gpuQueries[slot]); // Amedo 2026-10-26
                PerfStats.ReportGpu(ns / 1_000_000f);
            }
            _gpuPending[slot] = false;
        }
        _gl.BeginQuery(QueryTarget.TimeElapsed, _gpuQueries[slot]);
    }

    // Amedo 2026-10-26
    private unsafe ulong QueryNanoseconds(uint query)
    {
        if (!Gles)
        {
            _gl.GetQueryObject(query, QueryObjectParameterName.Result, out ulong ns);
            return ns;
        }
        ulong value = 0;
        GlesExtensions.GetQueryObjectui64(query, (uint)QueryObjectParameterName.Result, &value);
        return value;
    }

    // Amedo 2026-09-28
    private void EndGpuTimer()
    {
        _gl.EndQuery(QueryTarget.TimeElapsed);
        _gpuPending[_gpuSlot] = true;
        _gpuSlot = (_gpuSlot + 1) % _gpuQueries.Length;
    }

    private void OnRender(double dt)
    {
        if (Desktop is null && !ScreenSizeKnown()) return; // Amedo 2026-10-27
        if (GlCaps.GpuTimer) BeginGpuTimer(); // Amedo 2026-10-26
        if (TargetFramebuffer != 0) // Amedo 2026-09-30
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, TargetFramebuffer);
            _gl.Viewport(0, 0, (uint)Width, (uint)Height);
        }
        // Amedo 2026-09-20
        _gl.ClearColor(0.10f, 0.10f, 0.12f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        if (_imgui is not null) BeginImGuiFrame((float)dt); // Amedo 2026-10-26

        if ((ViewWidth, ViewHeight) != _viewSize) // Amedo
        {
            _viewSize = (ViewWidth, ViewHeight);
            _activeScene?.Resize(_viewSize.W, _viewSize.H);
        }
        SceneFramebuffer = TargetFramebuffer; // Amedo
        SceneWidth = ViewWidth;
        SceneHeight = ViewHeight;
        SceneSamples = 1;
        if (SceneHeld?.Invoke() != true || ReuseScene?.Invoke(_gl) != true)
        {
            bool offscreen = BeginScene?.Invoke(_gl) == true;
            if (!offscreen && SceneViewport is { } view) // Amedo
                _gl.Viewport(view.X, Height - view.Y - view.H, (uint)view.W, (uint)view.H);
            using (PerfStats.Measure(PerfSection.Render)) _activeScene?.Render(_gl); // Amedo 2026-09-28
            if (offscreen) EndScene?.Invoke(_gl); // Amedo
            else if (SceneViewport is not null) _gl.Viewport(0, 0, (uint)Width, (uint)Height); // Amedo
        }

        if (!Gles) _gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill); // Amedo 2026-10-26

        if (_imgui is not null) // Amedo 2026-10-26
        using (PerfStats.Measure(PerfSection.Ui)) // Amedo 2026-09-28
        {
            _activeScene?.OnImGuiRender();
            DrawFrameToast(dt); // Amedo 2026-09-29
            RenderImGui(); // Amedo
        }
        if (GlCaps.GpuTimer) EndGpuTimer(); // Amedo 2026-10-26
        PerfStats.VSync = _win.VSync;
        PerfStats.FrameMode = FrameModeName; // Amedo 2026-09-29
        PerfStats.FrameBudgetMs = FrameBudgetMs;
        PerfStats.EndFrame();
    }

    // Amedo 2026-10-27
    public static Action? ClearScreenSizeError { get; set; }

    private bool ScreenSizeKnown()
    {
        ClearScreenSizeError?.Invoke();
        try { return _win.FramebufferSize is { X: > 0, Y: > 0 }; }
        catch (Exception) { return false; }
    }

    // Amedo 2026-10-26
    private void BeginImGuiFrame(float dt)
    {
        _imgui!.Update(dt);

        // Amedo 2026-09-22
        unsafe
        {
            var colors = ImGui.GetStyle().Colors;
            colors[(int)ImGuiCol.WindowBg].W = 1f;
            colors[(int)ImGuiCol.ChildBg].W  = 1f;
            colors[(int)ImGuiCol.PopupBg].W  = 1f;
        }

        var io = ImGui.GetIO();

        bool ctrlHeld  = Input.KeyHeld(Key.ControlLeft) || Input.KeyHeld(Key.ControlRight);
        bool shiftHeld = Input.KeyHeld(Key.ShiftLeft)   || Input.KeyHeld(Key.ShiftRight);
        bool altHeld   = Input.KeyHeld(Key.AltLeft)     || Input.KeyHeld(Key.AltRight);
        io.KeyCtrl  = ctrlHeld;
        io.KeyShift = shiftHeld;
        io.KeyAlt   = altHeld;
        io.AddKeyEvent(ImGuiKey.ModCtrl,  ctrlHeld);
        io.AddKeyEvent(ImGuiKey.ModShift, shiftHeld);
        io.AddKeyEvent(ImGuiKey.ModAlt,   altHeld);
    }

    [System.Runtime.CompilerServices.UnsafeAccessor(System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "_frameBegun")]
    private static extern ref bool FrameBegun(ImGuiController controller); // Amedo

    // Amedo
    private void RenderImGui()
    {
        if (_imgui is null) return;
        if (_imguiRenderer is null)
        {
            _imgui.Render();
            return;
        }
        ref bool begun = ref FrameBegun(_imgui);
        if (!begun) return;
        begun = false;
        ImGui.Render();
        _imguiRenderer.Render(ImGui.GetDrawData());
    }

    private void OnResize(Silk.NET.Maths.Vector2D<int> s)
    {
        Width  = s.X;
        Height = s.Y;
        _gl.Viewport(0, 0, (uint)s.X, (uint)s.Y);
        _viewSize = (ViewWidth, ViewHeight); // Amedo
        _activeScene?.Resize(_viewSize.W, _viewSize.H);
    }

    private void OnClose()
    {
        _activeScene?.Destroy();
        GraphicsSettings.Changed -= ApplyFrameSettings;
    }
}
