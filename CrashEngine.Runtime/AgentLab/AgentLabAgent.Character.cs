using System.Numerics;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-29
public sealed partial class AgentLabAgent
{
    private Vector3 _stepStart;

    public Character.CharacterController? OwnCharacter { get; internal set; }

    internal bool EventsHeld_18 { get; set; } // Amedo

    // Amedo
    internal void CharacterRestart_17A950()
    {
        if (OwnCharacter is not { } c) return;
        c.ApplyState_131B58();
        EventsHeld_18 = false;
        c.LastDamage = default;
    }

    public bool IsCharacterObject => GameObject?.Type == ITwinObject.ObjectType.Character;

    public Vector3 CharacterDisplacement { get; private set; }

    internal bool MotionHold { get; private set; }

    internal bool CharacterPaused => (AgentFlags & 0x10) == 0 || MotionHold;

    private void BeginCharacterStep_22F758()
    {
        _stepStart = GamePosition;
        MotionHold = false;
    }

    private void EndCharacterStep_22F758()
    {
        var now = GamePosition;
        CharacterDisplacement = now - _stepStart;
        if (!MotionHold && now != _stepStart) SetLocalPosition(_stepStart);
    }

    private void HoldMotion() => MotionHold = true;

    private void KeepWarp(Vector3 position)
    {
        _stepStart = position;
        WarpedThisStep = true; // Amedo 2026-11-11
    }

    // Amedo 2026-11-11
    internal bool WarpedThisStep { get; private set; }

    internal void ClearWarp() => WarpedThisStep = false;

    // Amedo 2026-10-05
    public uint RenderFlags18 { get; private set; }

    internal void ClearAgentFlags(uint bits) => AgentFlags &= ~bits; // Amedo 2026-10-05
    internal void SetAgentFlags(uint bits) => AgentFlags |= bits; // Amedo 2026-10-05

    internal AgentLabAgent? Held11C => Alive(ref _slot11C); // Amedo 2026-10-05

    // Amedo 2026-10-05
    private void LinkToFocusCharacter_11AD48(uint arg0)
    {
        if (FocusObject is not { } target) return;
        if (OwnCharacter is not { } self || target.OwnCharacter is not { } other) return;
        if ((arg0 & 1) == 0) Character.CharacterController.Link_132D38(self, other);
        else Character.CharacterController.Link_132D38(other, self);
        Trace($"LinkToFocusCharacter {target.Entity.Name} ({((arg0 & 1) == 0 ? "I lead" : "it leads")})");
    }

    // Amedo 2026-10-05
    private void SetFocusProperties_1142E8(uint arg0)
    {
        if (FocusObject is not { } target) return;
        if ((arg0 & 3) == 1) target.Wake_1974C8(); // Amedo
        else if ((arg0 & 3) == 2) target.Sleep_197508();
        if (((arg0 >> 2) & 3) != 0)
        {
            target.AgentFlags = Toggle(target.AgentFlags, (arg0 >> 2) & 3, 0x400);
            target.ApplyRendered();
        }
        target.AgentFlags = Toggle(target.AgentFlags, (arg0 >> 4) & 3, 0x10);
        target.AgentFlags = Toggle(target.AgentFlags, (arg0 >> 6) & 3, 0x8);
        uint mode = (arg0 >> 8) & 3;
        if (mode == 0) return;
        if (target.OwnCharacter is { } own) own.MotionWord = (int)Toggle((uint)own.MotionWord, mode, 0x100);
        else target.Motion10 = Toggle(target.Motion10, mode, 0x100);
    }

    // Amedo 2026-10-05
    private void ForceAnimationUpdate_2215F0(uint arg0)
    {
        if (Anim is null) return;
        RenderFlags18 = (arg0 & 0xF) switch
        {
            1 => RenderFlags18 | 0x1000000,
            2 => RenderFlags18 | 0x2000000,
            _ => RenderFlags18 & ~0x2000000u,
        };
    }
}
