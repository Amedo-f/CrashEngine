using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace CrashEngine.Core;

// Amedo 2026-11-12
public sealed unsafe class ToolWindow
{
    private const ImGuiWindowFlags BodyFlags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus;

    private readonly IWindow _window;
    private readonly GL _gl;
    private readonly IInputContext _input;
    private readonly ImGuiRenderer _renderer;
    private readonly nint _context;
    private readonly Action _draw;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private bool _closing, _destroyed;

    public bool IsOpen => !_closing && !_destroyed;

    internal ToolWindow(IWindow owner, (int W, int H, byte[] Rgba)[]? icons, string title, int width, int height, Action draw)
    {
        _draw = draw;
        var ownerContext = ImGui.GetCurrentContext();
        var style = *ImGui.GetStyle().NativePtr;
        var fonts = ImGui.GetIO().Fonts;
        _window = Window.Create(WindowOptions.Default with
        {
            Title = title,
            Size = new Vector2D<int>(width, height),
            Position = owner.Position + (owner.Size - new Vector2D<int>(width, height)) / 2,
            API = owner.API,
            SharedContext = owner.GLContext,
            VSync = false,
            ShouldSwapAutomatically = false,
        });
        try
        {
            _window.Initialize();
            if (icons is { Length: > 0 })
                _window.SetWindowIcon(icons.Select(i => new Silk.NET.Core.RawImage(i.W, i.H, i.Rgba)).ToArray());
            _gl = GL.GetApi(_window);
            _input = _window.CreateInput();
            _renderer = new ImGuiRenderer(_gl);
            _context = ImGui.CreateContext(fonts);
            ImGui.SetCurrentContext(_context);
            var io = ImGui.GetIO();
            io.NativePtr->IniFilename = null;
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
            *ImGui.GetStyle().NativePtr = style;
            ImGuiClipboard.Hook(io);
            HookInput();
        }
        catch
        {
            if (_context != 0) ImGui.DestroyContext(_context);
            _window.Reset();
            throw;
        }
        finally
        {
            ImGui.SetCurrentContext(ownerContext);
            owner.GLContext?.MakeCurrent();
        }
    }

    public void Close() => _closing = true;

    public void Focus()
    {
        if (!IsOpen) return;
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Focus();
    }

    private void HookInput()
    {
        foreach (var keyboard in _input.Keyboards)
        {
            keyboard.KeyDown += (_, key, _) => Feed(io => io.AddKeyEvent(TranslateKey(null, key), true));
            keyboard.KeyUp += (_, key, _) => Feed(io => io.AddKeyEvent(TranslateKey(null, key), false));
            keyboard.KeyChar += (_, c) => Feed(io => io.AddInputCharacter(c));
        }
        foreach (var mouse in _input.Mice)
        {
            mouse.MouseMove += (_, p) => Feed(io => io.AddMousePosEvent(p.X, p.Y));
            mouse.MouseDown += (_, b) => Feed(io => io.AddMouseButtonEvent(ButtonIndex(b), true));
            mouse.MouseUp += (_, b) => Feed(io => io.AddMouseButtonEvent(ButtonIndex(b), false));
            mouse.Scroll += (_, w) => Feed(io => io.AddMouseWheelEvent(w.X, w.Y));
        }
        _window.FocusChanged += focused => Feed(io => io.AddFocusEvent(focused));
    }

    private static int ButtonIndex(MouseButton button) => button switch { MouseButton.Right => 1, MouseButton.Middle => 2, _ => 0 };

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "TranslateInputKeyToImGuiKey")]
    private static extern ImGuiKey TranslateKey(ImGuiController? controller, Key key);

    private void Feed(Action<ImGuiIOPtr> apply)
    {
        if (_destroyed) return;
        var previous = ImGui.GetCurrentContext();
        ImGui.SetCurrentContext(_context);
        apply(ImGui.GetIO());
        ImGui.SetCurrentContext(previous);
    }

    internal void Render()
    {
        if (_destroyed) return;
        if (_window.IsClosing) _closing = true;
        float dt = (float)_clock.Elapsed.TotalSeconds;
        _clock.Restart();
        var size = _window.Size;
        var fb = _window.FramebufferSize;
        if (_closing || size.X <= 0 || size.Y <= 0 || fb.X <= 0 || fb.Y <= 0) return;

        var previous = ImGui.GetCurrentContext();
        _window.GLContext!.MakeCurrent();
        ImGui.SetCurrentContext(_context);
        try
        {
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2(size.X, size.Y);
            io.DisplayFramebufferScale = new Vector2(fb.X / (float)size.X, fb.Y / (float)size.Y);
            io.DeltaTime = Math.Max(dt, 1e-4f);
            AddModifiers(io);
            ImGui.NewFrame();
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(io.DisplaySize);
            ImGui.Begin("##toolwindow", BodyFlags);
            _draw();
            ImGui.End();
            ImGui.Render();

            _gl.Viewport(0, 0, (uint)fb.X, (uint)fb.Y);
            var bg = ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg];
            _gl.ClearColor(bg.X, bg.Y, bg.Z, 1f);
            _gl.Clear(ClearBufferMask.ColorBufferBit);
            _renderer.Render(ImGui.GetDrawData());
            _window.GLContext.SwapBuffers();
        }
        finally
        {
            ImGui.SetCurrentContext(previous);
        }
    }

    private void AddModifiers(ImGuiIOPtr io)
    {
        if (_input.Keyboards.Count == 0) return;
        var k = _input.Keyboards[0];
        io.AddKeyEvent(ImGuiKey.ModCtrl, k.IsKeyPressed(Key.ControlLeft) || k.IsKeyPressed(Key.ControlRight));
        io.AddKeyEvent(ImGuiKey.ModShift, k.IsKeyPressed(Key.ShiftLeft) || k.IsKeyPressed(Key.ShiftRight));
        io.AddKeyEvent(ImGuiKey.ModAlt, k.IsKeyPressed(Key.AltLeft) || k.IsKeyPressed(Key.AltRight));
        io.AddKeyEvent(ImGuiKey.ModSuper, k.IsKeyPressed(Key.SuperLeft) || k.IsKeyPressed(Key.SuperRight));
    }

    internal void Destroy()
    {
        if (_destroyed) return;
        _destroyed = _closing = true;
        var previous = ImGui.GetCurrentContext();
        _window.GLContext?.MakeCurrent();
        _renderer.Dispose();
        _input.Dispose();
        ImGui.DestroyContext(_context);
        ImGui.SetCurrentContext(previous == _context ? 0 : previous);
        _window.Reset();
        _window.Dispose();
    }
}
