namespace CrashEngine.Runtime.Character;

// Amedo 2026-09-27
public sealed class CharacterPad
{
    public const int Right = 0, Left = 1, Up = 2, Down = 3, Triangle = 4, Circle = 5, Cross = 6, Square = 7, L1 = 8, R1 = 9, L2 = 10, R2 = 11;

    public readonly byte[] Pressure = new byte[12];
    public byte LeftX = 0x80, LeftY = 0x80, RightX = 0x80, RightY = 0x80;
    public bool L3, R3, Start, Select;
    public uint Buttons_00, Previous_04; // Amedo
    public const int ButtonCount = 24;

    private static readonly int[][] AxisSources = [[0, 4], [1, 5]];
    private static readonly int[][] ButtonSources = [[5], [7], [6], [16, 17], [19, 20], [17], [22]];

    public float DeadZone { get; set; } = 0.3f;

    public void Clear()
    {
        Array.Clear(Pressure);
        LeftX = LeftY = RightX = RightY = 0x80;
        L3 = R3 = Start = Select = false;
    }

    public void SetStick(float x, float y, bool right = false)
    {
        byte bx = ToByte(x), by = ToByte(-y);
        if (right) { RightX = bx; RightY = by; }
        else { LeftX = bx; LeftY = by; }
    }

    private static byte ToByte(float v) => (byte)Math.Clamp((int)MathF.Round(127.5f + v * 127.5f), 0, 255);

    // Amedo 2026-09-27
    public void SetHostStick(float x, float y, bool right = false, float axisScale = 1.33f)
    {
        byte bx = HostByte(x, axisScale), by = HostByte(-y, axisScale);
        if (right) { RightX = bx; RightY = by; }
        else { LeftX = bx; LeftY = by; }
    }

    private static byte HostByte(float v, float scale)
    {
        int pos = (int)Math.Clamp(v * scale * 255f, 0f, 255f), neg = (int)Math.Clamp(-v * scale * 255f, 0f, 255f);
        return (byte)(pos != 0 ? 127 + (pos + 1) / 2 : 127 - neg / 2);
    }

    private float P(int i) => Pressure[i] * (1f / 255f);

    // Amedo
    private static float StickButton(byte raw, bool negative)
    {
        float f = (raw - 127.5f) * 0.007843138f;
        if (negative) f = -f;
        if (0.75f < f) return 1f;
        if (0.25f < f) return (f - 0.25f) + (f - 0.25f);
        return 0f;
    }

    // Amedo
    public float GetButtonPressure_2B19D0(int i) => i switch
    {
        0 => P(Up),
        1 => P(Down),
        2 => P(Left),
        3 => P(Right),
        4 => P(Triangle),
        5 => P(Cross),
        6 => P(Circle),
        7 => P(Square),
        8 => StickButton(LeftY, true),
        9 => StickButton(LeftY, false),
        10 => StickButton(LeftX, true),
        11 => StickButton(LeftX, false),
        12 => StickButton(RightY, true),
        13 => StickButton(RightY, false),
        14 => StickButton(RightX, true),
        15 => StickButton(RightX, false),
        16 => P(L1),
        17 => P(L2),
        18 => L3 ? 1f : 0f,
        19 => P(R1),
        20 => P(R2),
        21 => R3 ? 1f : 0f,
        22 => Start ? 1f : 0f,
        23 => Select ? 1f : 0f,
        _ => 0f,
    };

    public float Axis(int i) => i switch
    {
        0 => (LeftX - 127.5f) * 0.007843138f,
        1 => -((LeftY - 127.5f) * 0.007843138f),
        2 => (RightX - 127.5f) * 0.007843138f,
        3 => -((RightY - 127.5f) * 0.007843138f),
        4 => Dpad(Pressure[Right], Pressure[Left]),
        5 => Dpad(Pressure[Up], Pressure[Down]),
        _ => 0f,
    };

    private static float Dpad(byte positive, byte negative)
    {
        float p = positive * 0.003921569f, n = negative * 0.003921569f;
        if (0f < p) return p <= 0.25f ? p + p + 0.5f : 1f;
        if (0f < n) return n <= 0.25f ? -0.5f - (n + n) : -1f;
        return 0f;
    }

    // Amedo
    public void UpdateButtons_2B2348(float dt = 0f, uint padFlags = 0x200u)
    {
        Previous_04 = Buttons_00;
        Buttons_00 = 0;
        for (int b = 0; b < ButtonCount; b++)
            if (0.05f < GetButtonPressure_2B19D0(b)) Buttons_00 |= 1u << b;
        StepRumble_2B2348(dt, padFlags); // Amedo
    }

    public uint RumbleWord_3C6B80; // Amedo
    public float RumbleTime_3C6B84;
    public bool SmallMotor_550; // Amedo
    public byte LargeMotor_551;

    // Amedo
    public void RumbleSet_2B36D0(uint word, float seconds)
    {
        RumbleWord_3C6B80 = word | 0x20u;
        RumbleTime_3C6B84 = seconds;
    }

    // Amedo
    private void StepRumble_2B2348(float dt, uint padFlags)
    {
        if ((RumbleWord_3C6B80 & 0x1F) == 0 || (padFlags & 0x100) != 0) return;
        if (0f < RumbleTime_3C6B84) RumbleTime_3C6B84 -= dt;
        else RumbleWord_3C6B80 = (RumbleWord_3C6B80 & 0xFFFFFFE0u & 0xFFFFFFBFu & 0xFFFF807Fu) | 0x20u;
        uint w = RumbleWord_3C6B80;
        if (((w >> 5) & 1) == 0 || (padFlags & 0x200) == 0) return;
        SetActuator_2B3998(((w >> 6) & 1) != 0, (byte)((w >> 7) & 0xFF));
        RumbleWord_3C6B80 = w & 0xFFFFFFDFu;
    }

    // Amedo
    private void SetActuator_2B3998(bool small, byte large)
    {
        SmallMotor_550 = small;
        LargeMotor_551 = large;
    }

    // Amedo
    public void StopRumble()
    {
        RumbleWord_3C6B80 = 0;
        RumbleTime_3C6B84 = 0f;
        SmallMotor_550 = false;
        LargeMotor_551 = 0;
    }

    // Amedo
    public bool GetButtonState_2B3548(int button, bool edge)
    {
        uint bit = 1u << button;
        if (!edge) return (Buttons_00 & bit) != 0;
        if ((Previous_04 & bit) != 0) return false;
        return (Buttons_00 & bit) != 0;
    }

    public float BoundAxis(int slot)
    {
        float dz = DeadZone, scale = 1f / (1f - dz);
        float result = 0f;
        foreach (int s in AxisSources[slot])
        {
            float v = Axis(s);
            float r = v < 0f ? (v < -dz ? (v + dz) * scale : 0f) : (dz < v ? (v - dz) * scale : 0f);
            if (MathF.Abs(r) <= 5e-5f) continue;
            if (MathF.Abs(result) <= 5e-5f) result = r;
        }
        return result;
    }

    public float BoundButton(int slot)
    {
        foreach (int s in ButtonSources[slot])
        {
            float v = GetButtonPressure_2B19D0(s);
            if (5e-5f < MathF.Abs(v)) return v;
        }
        return 0f;
    }
}
