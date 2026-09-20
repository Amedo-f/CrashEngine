using Silk.NET.Input;
using System.Numerics;

namespace CrashEngine.Core;

public static class Input
{
    private static IKeyboard? _kb;
    private static IMouse?    _mouse;

    private static readonly HashSet<Key> _pressed  = new();
    private static readonly HashSet<Key> _held     = new();
    private static readonly HashSet<Key> _released = new();

    public static Vector2 MouseDelta    { get; private set; }
    public static Vector2 MousePosition { get; private set; }
    public static float   ScrollDelta   { get; private set; }
    public static bool    CursorLocked  { get; private set; }

    private static Vector2 _lastMouse;
    private static bool    _firstFrame = true;

    internal static void Initialize(IInputContext ctx)
    {
        _kb    = ctx.Keyboards.FirstOrDefault();
        _mouse = ctx.Mice.FirstOrDefault();

        if (_kb != null)
        {
            _kb.KeyDown += (_, k, _) => _pressed.Add(k);
            _kb.KeyUp   += (_, k, _) => _released.Add(k);
        }
        if (_mouse != null)
            _mouse.Scroll += (_, w) => ScrollDelta += w.Y;
    }

    internal static void EndFrame()
    {
        _held.UnionWith(_pressed);
        _pressed.Clear();
        foreach (var k in _released) _held.Remove(k);
        _released.Clear();
        ScrollDelta = 0f;

        if (_mouse != null)
        {
            var pos = new Vector2(_mouse.Position.X, _mouse.Position.Y);
            MousePosition = pos;
            MouseDelta    = _firstFrame ? Vector2.Zero : pos - _lastMouse;
            _lastMouse    = pos;
            _firstFrame   = false;
        }
    }

    public static bool KeyDown(Key k)  => _pressed.Contains(k);
    public static bool KeyHeld(Key k)  => _held.Contains(k);
    public static bool KeyUp(Key k)    => _released.Contains(k);
    public static bool Mouse(MouseButton b) => _mouse?.IsButtonPressed(b) ?? false;

    public static void LockCursor(bool locked)
    {
        if (_mouse == null) return;
        CursorLocked         = locked;
        _mouse.Cursor.CursorMode = locked ? CursorMode.Raw : CursorMode.Normal;
    }
}
