using CrashEngine.Runtime.Character;

namespace CrashEngine.Runtime.Game;

// Amedo
public sealed class ButtonBindings
{
    public const uint CountMask = 0xF, HasModifier = 0x10, NeedsModifier = 0x20;

    // Amedo
    public sealed class Digital
    {
        public readonly byte[] Buttons_00 = new byte[4];
        public uint Word_04;

        public int Count => (int)(Word_04 & CountMask);
        public int Modifier => (int)((Word_04 >> 6) & 0xFF);
    }

    // Amedo
    public sealed class Analog
    {
        public uint Word_04;
    }

    public readonly Digital[] Digital_04;
    public readonly Analog[] Analog_08;

    // Amedo
    public ButtonBindings(int analog, int digital)
    {
        Digital_04 = new Digital[digital];
        for (int i = 0; i < digital; i++) Digital_04[i] = new Digital();
        Analog_08 = new Analog[analog];
        for (int i = 0; i < analog; i++) Analog_08[i] = new Analog();
        Clear_2B3A78();
    }

    // Amedo
    public void Clear_2B3A78()
    {
        foreach (var d in Digital_04) d.Word_04 &= ~CountMask;
        foreach (var a in Analog_08) a.Word_04 &= ~CountMask;
    }

    // Amedo
    public void SetDigitalBind_2B3B00(int index, int button)
    {
        var d = Digital_04[index];
        int n = d.Count;
        d.Word_04 = (d.Word_04 & ~HasModifier & ~CountMask) | (((uint)n + 1) & CountMask);
        d.Buttons_00[n] = (byte)button;
    }

    // Amedo
    public bool Has_2B2FE8(CharacterPad pad, int index, bool edge)
    {
        var d = Digital_04[index];
        bool found = false;
        for (int i = 0; i < d.Count; i++)
            if (pad.GetButtonState_2B3548(d.Buttons_00[i], edge))
            {
                found = true;
                break;
            }
        uint word = d.Word_04;
        if ((word & HasModifier) == 0) return found;
        var modifier = Digital_04[d.Modifier];
        bool held = false;
        for (int i = 0; i < modifier.Count; i++)
            if ((pad.Buttons_00 & (1u << modifier.Buttons_00[i])) != 0)
            {
                held = true;
                break;
            }
        if (!found) return false;
        return (word & NeedsModifier) != 0 ? held : !held;
    }
}
