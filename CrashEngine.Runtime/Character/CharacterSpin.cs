using CrashEngine.Core;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-09-27
public sealed class CharacterSpin
{
    public const uint SlideJumpSpinBit = 0x40000;

    private const float TickRate = 9000f;

    private readonly CharacterController _c;

    public uint Flags = 0x40;
    public int PhaseTime;
    public int Duration;

    public CharacterSpin(CharacterController c) => _c = c;

    public int Phase => (int)(Flags & 0xF);

    public void Update(float button, float len, int now)
    {
        int state = _c.MotionState;
        bool stateOk = state is 3 or 6 or 10;
        int next = -1;
        if ((Flags & 0xF0) != 0x40)
        {
            PhaseTime = now;
            Flags = (Flags & ~0xFFu) | ((Flags >> 4) & 0xF) | 0x40;
        }
        uint high = _c.MotionFlagsHigh;
        bool slideJump = (high & (1u << 3)) != 0 && (_c.JumpRules & SlideJumpSpinBit) == 0;
        bool slam = _c.Link is { Phase: 2 }; // Amedo 2026-10-05
        if (slideJump || (high & (1u << 4)) != 0 || slam || !stateOk) next = Stop();
        else
            switch (Phase)
            {
                case 0:
                    if (0f < button && (Flags & 0x100) == 0) next = Begin();
                    break;
                case 1:
                case 2:
                    if (Duration <= now - PhaseTime) next = Stop();
                    if (0f < len)
                    {
                        _c.SetOverride(CharacterController.ForwardOverride, ref _c.Forward, _c.Param(9));
                        _c.SetTurn(_c.IntParam(3));
                    }
                    else
                    {
                        _c.SetOverride(CharacterController.ForwardOverride, ref _c.Forward, 0f);
                        _c.SetTurn(0);
                    }
                    if (Phase == 1) Sweep_14E590(1.4f, -0.8f); // Amedo
                    else Sweep_14E590(1f, -1.3f);
                    break;
                case 3:
                    if (Duration <= now - PhaseTime) next = 0;
                    break;
            }
        Flags = (Flags & ~0x100u) | (0f < button ? 0x100u : 0u);
        if (next != -1) Flags = (Flags & ~0xF0u) | (uint)((next & 0xF) << 4);
    }

    public int SweepAngle; // Amedo

    // Amedo
    private void Sweep_14E590(float reach, float push)
    {
        SweepAngle += 0x21D1;
        if (0x10000 < SweepAngle) SweepAngle -= 0x10000;
        for (int i = 0; i < 4; i++)
        {
            float a = (SweepAngle + (int)(i * 1.5707964f * 10430.378f)) * GameMath.UnitToRad;
            _c.SweepRay_14E590(new System.Numerics.Vector3(MathF.Cos(a) * reach, 0f, MathF.Sin(a) * reach), push);
        }
    }

    private int Begin()
    {
        if ((_c.MotionFlagsHigh & 1u) != 0) return 0;
        var link = _c.Link; // Amedo 2026-10-05
        if (link is not null && (_c.MotionFlagsHigh & 2u) != 0) return 0;
        _c.Trigger(0x12);
        Duration = GameMath.Trunc(_c.Param(10) * TickRate);
        if (link is null) return 2; // Amedo 2026-10-05
        link.Follower_15FE98()?.Trigger(0x12);
        return 1;
    }

    private int Stop()
    {
        if (Phase is not (1 or 2)) return 0;
        if (_c.Link is { } link) // Amedo 2026-10-05
        {
            var follower = link.Follower_15FE98();
            if (follower is not null)
            {
                follower.Trigger(0x13);
                follower.Bones.SpinEnd_15F860(follower.GameMatrix);
            }
            var cleared = follower?.Link ?? link;
            cleared.Flags &= 0xFFFFFF0FFFFFFFFFUL; // Amedo
        }
        Duration = GameMath.Trunc(_c.Param(11) * TickRate);
        if (Duration == 0) return 0;
        _c.Trigger(0x13);
        return 3;
    }
}
