using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace CrashEngine.Core;

public sealed class Engine
{
    public static Engine Instance { get; private set; } = null!;
    
    private readonly IWindow _win;
    private GL      _gl    = null!;
    private ImGuiController? _imgui;
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
    
    public Engine(string title = "CrashEngine", int w = 1280, int h = 720)
    {
        Instance = this;
        Width = w; Height = h;
        
        var opts = WindowOptions.Default with
        {
            Title = title,
            Size  = new Silk.NET.Maths.Vector2D<int>(w, h),
            API   = new GraphicsAPI(
                       ContextAPI.OpenGL,
                      ContextProfile.Core,
                        ContextFlags.ForwardCompatible,
                     new APIVersion(4, 6))
        };

        _win = Window.Create(opts);
        _win.Load              += OnLoad;
        _win.Update            += OnUpdate;
        _win.Render            += OnRender;
        _win.FramebufferResize += OnResize;
        _win.Closing           += OnClose;
    }
    
    public void Run()  => _win.Run();
    public void Quit() => _win.Close();

    private void OnLoad()
    {
        _gl    = GL.GetApi(_win);
        _input = _win.CreateInput();
        Input.Initialize(_input);

        _imgui = new ImGuiController(_gl, _win, _input, null,
            () => CustomFontSetup?.Invoke(ImGui.GetIO().Fonts));
        var io = ImGui.GetIO();
        Win32Clipboard.Hook(io);

        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        // Amedo 2026-09-21 -- edge-resize needs BOTH of these
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

        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(TriangleFace.Back);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _activeScene?.Load(_gl);
    }

    private void OnUpdate(double dt)
    {
        if (_pendingScene is not null)
        {
            _activeScene?.Destroy();
            _activeScene  = _pendingScene;
            _pendingScene = null;
            _activeScene.Load(_gl);
        }

        EngineTime.Delta  = (float)(dt * EngineTime.TimeScale);
        EngineTime.Total += EngineTime.Delta;

        _fixedAccum += dt * EngineTime.TimeScale;
        while (_fixedAccum >= FixedStep)
        {
            EngineTime.FixedDelta = (float)FixedStep;
            _activeScene?.FixedUpdate();
            _fixedAccum -= FixedStep;
        }

        _activeScene?.Update();
        Input.EndFrame();
    }

    private void OnRender(double dt)
    {
        // Amedo 2026-09-20
        _gl.ClearColor(0.10f, 0.10f, 0.12f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _imgui?.Update((float)dt);
        var io = ImGui.GetIO();
        Win32Clipboard.Hook(io);

        bool ctrlHeld  = Input.KeyHeld(Key.ControlLeft) || Input.KeyHeld(Key.ControlRight);
        bool shiftHeld = Input.KeyHeld(Key.ShiftLeft)   || Input.KeyHeld(Key.ShiftRight);
        bool altHeld   = Input.KeyHeld(Key.AltLeft)     || Input.KeyHeld(Key.AltRight);
        io.KeyCtrl  = ctrlHeld;
        io.KeyShift = shiftHeld;
        io.KeyAlt   = altHeld;
        io.AddKeyEvent(ImGuiKey.ModCtrl,  ctrlHeld);
        io.AddKeyEvent(ImGuiKey.ModShift, shiftHeld);
        io.AddKeyEvent(ImGuiKey.ModAlt,   altHeld);

        _activeScene?.Render(_gl);

        _gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill);

        _activeScene?.OnImGuiRender();
        _imgui?.Render();
    }

    private void OnResize(Silk.NET.Maths.Vector2D<int> s)
    {
        Width  = s.X;
        Height = s.Y;
        _gl.Viewport(0, 0, (uint)s.X, (uint)s.Y);
        _activeScene?.Resize(s.X, s.Y);
    }

    private void OnClose()
    {
        _activeScene?.Destroy();
        _imgui?.Dispose();
    }
}
