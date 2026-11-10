using Silk.NET.Input;
using System.Numerics;
using CrashEngine.Platform.Windows;

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

    private static Vector2 _lastMouse;
    private static bool    _firstFrame = true;

    private static IInputContext? _ctx; // Amedo 2026-09-27

    public static IGamepad? Gamepad => _ctx?.Gamepads.FirstOrDefault(g => g.IsConnected);

    private static float _large = -1f, _small = -1f; // Amedo

    public static Func<float, float, bool>? Rumble;

    // Amedo
    public static void SetVibration(float large, float small)
    {
        if (large == _large && small == _small) return;
        if (XInput.Set(large, small) || Rumble?.Invoke(large, small) == true)
        {
            _large = large;
            _small = small;
            return;
        }
        if (Gamepad is not { } gp) return;
        var motors = gp.VibrationMotors;
        if (motors.Count == 0) return;
        _large = large;
        _small = small;
        motors[0].Speed = large;
        if (motors.Count > 1) motors[1].Speed = small;
    }

    internal static void Initialize(IInputContext ctx)
    {
        _ctx   = ctx;
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
    public static bool Mouse(MouseButton b) => _mouse?.IsButtonPressed(b) ?? false;
}
