using System.Numerics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed partial class AgentLabAgent
{
    public AgentLabAgent? Requester { get; private set; }

    private AgentLabAgent? CommandTarget(uint selector) => selector >= 0xFF ? Requester : Participant((int)(selector & 0xFF));

    private void ApplyVelocity(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        if ((Arg(7) & 0xC) != 0)
        {
            VelocityBox(a); // Amedo 2026-09-28
            return;
        }
        if (CommandTarget(Arg(11)) is { } target) VelocityTo(target, a);
    }

    // Amedo 2026-09-28
    private void VelocityBox(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        var pos = GamePosition;
        var vars = RootController(); // Amedo
        var end = pos + new Vector3(vars.FloatValueOf(Arg(4)), vars.FloatValueOf(Arg(5)), vars.FloatValueOf(Arg(6)));
        var hits = new List<AgentLabAgent>();
        float t = SegmentQuery(pos, end, 0x10B000, 0, 1, hits, 0x20);
        Trace($"ApplyVelocity segment {pos}..{end} -> {hits.Count} hit(s), t {t}");
        if (t == 0f) return;
        foreach (var hit in hits) VelocityTo(hit, a);
    }

    private void VelocityTo(AgentLabAgent target, List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float g = FloatValueOf(Arg(0));
        var d = new Vector3(FloatValueOf(Arg(4)), FloatValueOf(Arg(5)), FloatValueOf(Arg(6)));
        Vector3 v;
        if (MathF.Abs(d.X) <= 5e-5f && MathF.Abs(d.Y) <= 5e-5f && MathF.Abs(d.Z) <= 5e-5f)
            v = new Vector3(FloatValueOf(Arg(1)), FloatValueOf(Arg(2)), FloatValueOf(Arg(3)));
        else
        {
            float vy = MathF.Sqrt(MathF.Abs((g + g) * d.Y));
            float t = vy / g;
            v = new Vector3(d.X / t, vy, d.Z / t);
        }
        Trace($"ApplyVelocity {v} g {g} -> {target.Entity.Name}");
        target.ReceiveVelocity(v, this, g);
    }

    // Amedo
    private void NowGoBackCollidable_11F270(uint scaleWord)
    {
        if (OwnCharacter is not { } c) return;
        float gravity = SoundVariable((1u << 3) | 5u);
        c.Launch(_velocity * FloatValueOf(scaleWord), false, gravity, 0x6F);
    }

    private void ReceiveVelocity(Vector3 v, AgentLabAgent? source, float g)
    {
        if (OwnCharacter is { } c) // Amedo 2026-09-29
        {
            c.Launch(v, source is not null, g, 0x17, source?.GameMatrix); // Amedo
            return;
        }
        if (ControllerType == 13) // Amedo
        {
            CrateLaunch_140790(v);
            return;
        }
        if (ControllerType is >= 14 and <= 20) // Amedo
        {
            if (Component5 is { } body) body.SetVelocity_2915D0(v);
            Trace($"BasicAgent::Launch {v} -> {(Component5 is null ? "no rigid body" : "rigid body")}");
            return;
        }
        World.Unimplemented("velocity", ControllerType, $"controller vt+0xAC (type {ControllerType})");
    }
}
