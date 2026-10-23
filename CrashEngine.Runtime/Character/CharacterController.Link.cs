using System.Numerics;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-10-05
public sealed partial class CharacterController
{
    public const int LinkSlot = 0x41, UnlinkSlot = 0x42;
    public const uint LeaderBit = 1u << 21, FollowerBit = 1u << 22;

    public CharacterLink? Link;
    public int LinkTicks;
    private float _circleSeen; // Amedo 2026-10-06
    private static readonly float[] ThrownMarks = [0.1f, 0.25f, 0.5f]; // Amedo 2026-10-06
    private CharacterController? _thrown; // Amedo 2026-10-06
    private int _thrownTicks, _thrownStep; // Amedo 2026-10-06
    private Vector3 _thrownFrom; // Amedo 2026-10-06
    public Func<bool>? Alive;
    public Action<object, uint>? ClearInstanceFlags;
    public Action<object, uint>? SetInstanceFlags;

    private string Name => (Self as CrashEngine.Core.Component)?.Entity?.Name ?? "-";

    public bool IsLinkFollower => (MotionFlagsHigh & FollowerBit) != 0;

    public static bool Link_132D38(CharacterController leader, CharacterController follower)
    {
        leader.Unlink_132E50();
        leader.Link = new CharacterLink(leader, follower, true);
        follower.Link = new CharacterLink(follower, leader, false);
        leader.MotionFlagsHigh |= LeaderBit;
        leader._trigger(LinkSlot);
        follower.MotionFlagsHigh |= FollowerBit;
        follower._trigger(LinkSlot);
        leader.LinkTicks = leader.Ticks;
        leader.Log?.Invoke($"[link] {leader.Name} leads {follower.Name} (0x132D38)");
        return true;
    }

    public void Unlink_132E50()
    {
        if (Link is not { } link) return;
        var follower = link.Follower_15FE98();
        DestroyLink_14DCA8(); // Amedo 2026-10-05
        MotionFlagsHigh &= ~LeaderBit;
        Locomotion.Reset_160600();
        _trigger(UnlinkSlot);
        Log?.Invoke($"[link] {Name} unlinked{(follower is not null ? $" from {follower.Name}" : "")} (0x132E50)");
        if (follower?.Link is null) return;
        follower.Link = null;
        follower.MotionFlagsHigh &= ~FollowerBit;
        follower.Locomotion.Reset_160600();
        follower._trigger(UnlinkSlot);
    }

    // Amedo 2026-10-05
    private void DestroyLink_14DCA8()
    {
        if (Link is not { } link) return;
        Link = null;
        if (!link.IsLeader || link.Follower_15FE98()?.Self is not { } other) return;
        if ((link.Flags & CharacterLink.PartnerFlag20000) != 0) SetInstanceFlags?.Invoke(other, 0x20000);
        else ClearInstanceFlags?.Invoke(other, 0x20000);
    }

    public const int SpinThrowSlot = 0x45, JumpThrowSlot = 0x46, SlamSlot = 0x44;

    private void LinkStep_12EA78(int frameTicks)
    {
        if (_thrown is { } th) // Amedo 2026-10-06
        {
            float since = (Ticks - _thrownTicks) * (1f / 9000f);
            if (_thrownStep < ThrownMarks.Length && since >= ThrownMarks[_thrownStep])
            {
                Log?.Invoke($"[link] {th.Name} thrown +{since:0.00}s pos {th.Position} vel {th.Velocity} state {th.MotionState} moved {Vector3.Distance(th.Position, _thrownFrom):0.00}");
                _thrownStep++;
            }
            if (_thrownStep >= ThrownMarks.Length) _thrown = null;
        }
        if (Link is not { } link)
        {
            if (MotionState is 13 or 14 && Locomotion.Phase != 9) MotionState = 3;
            return;
        }
        if ((MotionFlagsHigh & FollowerBit) != 0) PlaceHeld_12EA78(link); // Amedo 2026-10-05
        float circle = 0f;
        bool spinThrow = false, jumpThrow = false;
        if ((MotionFlagsHigh & LeaderBit) != 0)
        {
            circle = Circle;
            if ((Ticks - LinkTicks) * (1f / 9000f) <= 0.5f) circle = 0f;
            if (circle != 0f)
            {
                if ((MotionFlagsHigh & 2u) != 0) jumpThrow = true;
                else if (MotionState is 6 or 10) spinThrow = true;
            }
            if (Circle != 0f && _circleSeen == 0f) // Amedo 2026-10-06
                Log?.Invoke($"[link] {Name} circle: state {MotionState} spin {Spin.Phase} since link {(Ticks - LinkTicks) * (1f / 9000f):0.00}s air {(MotionFlagsHigh & 2u) != 0} phase {link.Phase} -> {(jumpThrow ? "jump throw" : spinThrow ? "spin throw" : circle == 0f ? "ignored (< 0.5s)" : "slam / none")}");
        }
        _circleSeen = (MotionFlagsHigh & LeaderBit) != 0 ? Circle : 0f; // Amedo 2026-10-06
        LinkUpdate_14DEC8(link, circle, frameTicks);
        int s = MotionState;
        MotionState = link.Phase == 2 ? (s == 3 ? 9 : s) : (s == 9 ? 3 : s);
        if (link.Follower_15FE98() is not { } follower || !(spinThrow || jumpThrow)) return;
        Throw_12EA78(follower, spinThrow);
    }

    private void LinkUpdate_14DEC8(CharacterLink link, float circle, int frameTicks)
    {
        var follower = link.Follower_15FE98();
        float hold = follower?.Link is { } far ? far.Value210 : link.Value210;
        float scale = (hold - 1.1f) * 2.857143f;
        if (1f < scale) scale = 1f;
        if (scale < 0f) scale = 0f;
        link.Scale40 = scale;
        float dt = frameTicks * (1f / 9000f);
        link.Blend214 += dt * 10f;
        if (1f < link.Blend214) link.Blend214 = 1f;
        if (follower is not null)
        {
            int now = Ticks;
            if (link.Phase == 1 && (MotionFlagsHigh & 2u) == 0 && circle != 0f && link.Time1A8 == 0f) StartSlam_14DDA8(link, follower, now);
            if (link.Phase == 2)
            {
                link.Time1A4 = now * (1f / 9000f) - link.Ticks1A0 * (1f / 9000f);
                if (1f < link.Time1A4)
                {
                    link.Flags = (link.Flags & ~CharacterLink.PhaseMask) | 0x100000000UL;
                    if (follower.Link is { } fl) fl.Flags = (fl.Flags & ~CharacterLink.PhaseMask & ~(0xFUL << 36)) | 0x100000000UL;
                    link.Time1A8 = 0.25f;
                }
            }
        }
        link.Time1A8 = link.Time1A8 <= dt ? 0f : link.Time1A8 - dt;
    }

    private bool StartSlam_14DDA8(CharacterLink link, CharacterController follower, int now)
    {
        if (MotionState is 6 or 10) return false;
        link.Flags = (link.Flags & ~CharacterLink.PhaseMask) | 0x200000000UL;
        if (follower.Link is { } fl) fl.Flags = (fl.Flags & ~CharacterLink.PhaseMask) | 0x200000000UL;
        SetVisible?.Invoke(true);
        follower.SetVisible?.Invoke(true);
        _trigger(SlamSlot);
        follower._trigger(SlamSlot);
        link.Ticks1A0 = now;
        Log?.Invoke($"[link] {Name} slam with {follower.Name} (0x14DDA8)");
        return true;
    }

    private void Throw_12EA78(CharacterController follower, bool spin)
    {
        var m = _getMatrix();
        var pos = m.Translation;
        var fwd = new Vector3(m.M31, m.M32, m.M33);
        int slot;
        float gravity;
        Vector3 start, mid, velocity;
        if (spin)
        {
            slot = SpinThrowSlot;
            gravity = 25f;
            start = pos + fwd + new Vector3(0f, 0.4f, 0f);
            mid = pos + fwd * 0.5f + new Vector3(0f, 0.2f, 0f); // Amedo 2026-10-05
            velocity = fwd * 33f + new Vector3(0f, 10.5f, 0f);
        }
        else
        {
            slot = JumpThrowSlot;
            gravity = 40f;
            start = pos + new Vector3(0f, 2.25f, 0f);
            mid = pos + new Vector3(0f, 1.125f, 0f); // Amedo 2026-10-05
            velocity = fwd * 19f + new Vector3(0f, 16f, 0f);
        }
        Locomotion.Stand_160470();
        MotionState = 3;
        if (!follower.FitsAt_138180(0, start) || !follower.FitsAt_138180(0, mid)) // Amedo 2026-10-05
        {
            Log?.Invoke($"[link] {Name} {(spin ? "spin" : "jump")} throw blocked at {start} / {mid} (0x138180)");
            return;
        }
        Unlink_132E50();
        follower.SetPosition(start);
        follower.Launch(velocity, false, gravity, 0x17);
        _thrown = follower; _thrownTicks = Ticks; _thrownFrom = start; _thrownStep = 0; // Amedo 2026-10-06
        follower.MotionFlags &= ~Grounded;
        follower.State70 &= ~0xF00UL;
        follower.StandingOn = null;
        follower.StandingPart = -1;
        _trigger(slot);
        if (follower.TriggerFrom is { } from) from(slot, Self);
        else follower._trigger(slot);
        int fs = follower.MotionState;
        fs = spin ? (fs == 3 ? 13 : fs) : (fs == 13 ? 3 : fs);
        fs = !spin ? (fs == 3 ? 14 : fs) : (fs == 14 ? 3 : fs);
        follower.MotionState = fs;
        follower.MotionWord |= 0x400;
        Log?.Invoke($"[link] {Name} {(spin ? "spin" : "jump")} throw {follower.Name} from {start} velocity {velocity} gravity {gravity} (0x12EA78)");
    }

    public void UnlinkCharacters_17B5C0()
    {
        if (Link is not { } link) return;
        if (!IsLinkFollower) Unlink_132E50();
        else link.Leader_15FE50()?.Unlink_132E50();
    }
}
