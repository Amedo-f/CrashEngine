using System.Runtime.InteropServices;

namespace CrashEngine.Platform.Windows;

public static class XInput
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
