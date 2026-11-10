using CrashEngine.Core;
using CrashEngine.Runtime.Character;
using Silk.NET.Input;

namespace CrashGame;

// Amedo 2026-09-30
static class PadInput
{
    public static Action<CharacterPad>? Extra; // Amedo 2026-10-26

    public static void Read(CharacterPad pad)
    {
        pad.Clear();
        static byte On(bool held) => held ? (byte)255 : (byte)0;
        pad.Pressure[CharacterPad.Up]       = On(Input.KeyHeld(Key.Up));
        pad.Pressure[CharacterPad.Down]     = On(Input.KeyHeld(Key.Down));
        pad.Pressure[CharacterPad.Left]     = On(Input.KeyHeld(Key.Left));
        pad.Pressure[CharacterPad.Right]    = On(Input.KeyHeld(Key.Right));
        pad.Pressure[CharacterPad.Cross]    = On(Input.KeyHeld(Key.Space));
        pad.Pressure[CharacterPad.Square]   = On(Input.KeyHeld(Key.J));
        pad.Pressure[CharacterPad.Circle]   = On(Input.KeyHeld(Key.K));
        pad.Pressure[CharacterPad.Triangle] = On(Input.KeyHeld(Key.I));
        pad.Pressure[CharacterPad.L1]       = On(Input.KeyHeld(Key.U));
        pad.Pressure[CharacterPad.R1]       = On(Input.KeyHeld(Key.O));
        pad.Start = Input.KeyHeld(Key.Enter);
        if (Input.Gamepad is { } gp) ReadGamepad(gp, pad); // Amedo 2026-10-26
        Extra?.Invoke(pad); // Amedo 2026-10-26
    }

    private static void ReadGamepad(IGamepad gp, CharacterPad pad) // Amedo 2026-10-26
    {
        foreach (var b in gp.Buttons)
        {
            if (!b.Pressed) continue;
            switch (b.Name)
            {
                case ButtonName.A:           pad.Pressure[CharacterPad.Cross] = 255; break;
                case ButtonName.X:           pad.Pressure[CharacterPad.Square] = 255; break;
                case ButtonName.B:           pad.Pressure[CharacterPad.Circle] = 255; break;
                case ButtonName.Y:           pad.Pressure[CharacterPad.Triangle] = 255; break;
                case ButtonName.LeftBumper:  pad.Pressure[CharacterPad.L1] = 255; break;
                case ButtonName.RightBumper: pad.Pressure[CharacterPad.R1] = 255; break;
                case ButtonName.DPadUp:      pad.Pressure[CharacterPad.Up] = 255; break;
                case ButtonName.DPadDown:    pad.Pressure[CharacterPad.Down] = 255; break;
                case ButtonName.DPadLeft:    pad.Pressure[CharacterPad.Left] = 255; break;
                case ButtonName.DPadRight:   pad.Pressure[CharacterPad.Right] = 255; break;
                case ButtonName.Start:       pad.Start = true; break;
                case ButtonName.Back:        pad.Select = true; break;
                case ButtonName.LeftStick:   pad.L3 = true; break;
                case ButtonName.RightStick:  pad.R3 = true; break;
            }
        }
        if (gp.Triggers.Count > 0 && gp.Triggers[0].Position > 0.5f) pad.Pressure[CharacterPad.L2] = 255;
        if (gp.Triggers.Count > 1 && gp.Triggers[1].Position > 0.5f) pad.Pressure[CharacterPad.R2] = 255;
        if (gp.Thumbsticks.Count > 0) pad.SetHostStick(gp.Thumbsticks[0].X, -gp.Thumbsticks[0].Y);
        if (gp.Thumbsticks.Count > 1) pad.SetHostStick(gp.Thumbsticks[1].X, -gp.Thumbsticks[1].Y, right: true);
    }
}
