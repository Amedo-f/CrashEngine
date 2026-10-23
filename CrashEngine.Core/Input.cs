using Silk.NET.Input;
using System.Numerics;
using System.Runtime.InteropServices;

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

    private static IInputContext? _ctx; // Amedo 2026-09-27

    public static IGamepad? Gamepad => _ctx?.Gamepads.FirstOrDefault(g => g.IsConnected);

    private static float _large = -1f, _small = -1f; // Amedo

    // Amedo
    public static void SetVibration(float large, float small)
    {
        if (large == _large && small == _small) return;
        if (XInput.Set(large, small))
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

    // Amedo
    private static class XInput
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Vibration
        {
            public ushort Left, Right;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct State
        {
            public uint Packet;
            public ushort Buttons;
            public byte LeftTrigger, RightTrigger;
            public short LeftX, LeftY, RightX, RightY;
        }

        private const int Users = 4;

        [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
        private static extern uint SetState(uint user, ref Vibration vibration);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint GetState(uint user, out State state);

        private static bool _missing;
        private static int _user = -1;

        public static bool Set(float large, float small)
        {
            if (_missing || !OperatingSystem.IsWindows()) return false;
            try
            {
                if (_user < 0 || GetState((uint)_user, out _) != 0) _user = FirstConnected();
                if (_user < 0) return false;
                var v = new Vibration { Left = Speed(large), Right = Speed(small) };
                return SetState((uint)_user, ref v) == 0;
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                _missing = true;
                return false;
            }
        }

        private static int FirstConnected()
        {
            for (int i = 0; i < Users; i++)
                if (GetState((uint)i, out _) == 0) return i;
            return -1;
        }

        private static ushort Speed(float s) => (ushort)MathF.Round(Math.Clamp(s, 0f, 1f) * ushort.MaxValue);
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
    public static bool KeyUp(Key k)    => _released.Contains(k);
    public static bool Mouse(MouseButton b) => _mouse?.IsButtonPressed(b) ?? false;

    public static void LockCursor(bool locked)
    {
        if (_mouse == null) return;
        CursorLocked         = locked;
        _mouse.Cursor.CursorMode = locked ? CursorMode.Raw : CursorMode.Normal;
    }
}
