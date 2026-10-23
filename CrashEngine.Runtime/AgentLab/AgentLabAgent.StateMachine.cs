using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-26
public sealed partial class AgentLabAgent
{
    private PS2BehaviourStateBody? _body;

    private static PS2BehaviourState? State(Level ctx, int index) =>
        ctx.Graph is not null && index >= 0 && index < ctx.Graph.ScriptStates.Count
            ? (PS2BehaviourState)ctx.Graph.ScriptStates[index]
            : null;

    private static PS2BehaviourStateBody Body(PS2BehaviourState state, int index) => (PS2BehaviourStateBody)state.Bodies[index];

    private static int NextOf(PS2BehaviourStateBody body) => body.HasStateJump ? body.JumpToState : -1;

    private static bool HasSub(PS2BehaviourState? s) => s is not null && s.BehaviourIndexOrSlot != -1;

    private int _lockDepth = 0xFF; // Amedo 2026-09-30

    private void RunStateStack(int depth, TwinBehaviourControlPacket? t0)
    {
        var ctx = Levels[depth];
        if (_lockDepth != 0xFF && depth < _lockDepth) // Amedo 2026-09-30
        {
            if (!HasSub(State(ctx, ctx.Current)) || depth + 1 >= Levels.Count) return;
            RunStateStack(depth + 1, t0);
            return;
        }
        if (ctx.Stopped) return;

        int s1 = EvaluateState(ctx, t0);
        if (s1 < 0) Stop(ctx, depth);
        else if (State(ctx, s1) is { } st && !HasSub(st) && st.ControlPacket is null && st.Bodies.Count == 0)
        {
            Stop(ctx, depth);
            s1 = -1;
        }

        if (!ctx.Stopped && HasSub(State(ctx, s1)))
        {
            int d = depth + 1;
            if (s1 != ctx.Entered)
            {
                OpenLevel(d, State(ctx, s1)!);
                ctx.Current = s1;
            }
            if (d < Levels.Count)
            {
                RunStateStack(d, t0);
                if (Levels[d].Stopped)
                {
                    var st = State(ctx, s1)!;
                    s1 = st.Bodies.Count > 0 ? RunBody(Body(st, 0), ctx) : -1;
                    if (s1 < 0) Stop(ctx, depth);
                    else if (State(ctx, s1)?.ControlPacket is { } cp) ApplyPacket(cp);
                }
            }
        }

        if (s1 != ctx.Current) Trace($"level {depth}: state {ctx.Current} -> {s1}{(ctx.Stopped ? " (stopped)" : "")}");
        int prev = ctx.Reenter ? -1 : ctx.Current;
        ctx.Current = s1;
        ctx.Entered = prev;
    }

    private void Stop(Level ctx, int depth)
    {
        ctx.Stopped = true;
        ActiveDepth = depth - 1;
    }

    private void OpenLevel(int depth, PS2BehaviourState state)
    {
        var parent = Levels[depth - 1];
        var graph  = state.UsesObjectSlot ? RootController().Slot(state.BehaviourIndexOrSlot).Graph : World.Behaviour((ushort)state.BehaviourIndexOrSlot); // Amedo: GetGameObjectAddress_FromInstance_ 0x23C0E0
        var level  = new Level
        {
            Graph     = graph,
            Current   = graph?.StartState ?? -1,
            EnterTime = parent.EnterTime,
            Depth     = depth,
        };
        if (depth < Levels.Count) Levels[depth] = level;
        else Levels.Add(level);
        ActiveDepth = depth;
    }

    private void StopDeeper(Level ctx)
    {
        for (int d = ctx.Depth + 1; d < ActiveDepth; d++)
        {
            Levels[d].Stopped = true;
            Levels[d].Current = -1;
        }
        ActiveDepth = ctx.Depth;
    }

    private int EvaluateState(Level ctx, TwinBehaviourControlPacket? t0)
    {
        var state = State(ctx, ctx.Current);
        if (state is null) return -1;
        int self = ctx.Current;
        bool skipFirst = state.SkipsFirstStateBody;
        _body = null;

        if (state.ControlPacket is { } own && ReferenceEquals(own, t0))
        {
            if (!skipFirst) return -1;
            ctx.Entered = self;
            return AfterBody(RunBody(Body(state, 0), ctx), ctx);
        }

        int first = skipFirst ? 1 : 0;
        if (first < state.Bodies.Count)
        {
            float best = -1e30f;
            PS2BehaviourStateBody? winner = null;
            for (int i = first; i < state.Bodies.Count; i++)
            {
                var body = Body(state, i);
                _body = body;
                var cond = body.Condition;
                if (cond is null) continue;
                float v = Evaluate(cond, ctx);
                bool ok = cond.NotGate ? v < cond.ReturnCheck : cond.ReturnCheck < v;
                if (!ok) continue;
                float score = (cond.NotGate ? cond.ReturnCheck - v : v - cond.ReturnCheck) * cond.ConditionPowerMultiplier;
                if (best < score) { best = score; winner = body; }
            }

            if (winner is not null)
            {
                if (HasSub(state)) StopDeeper(ctx);
                else EndPacket();
                return AfterBody(RunBody(winner, ctx), ctx);
            }

            if (ctx.Else is { } elseBody)
                return AfterBody(RunBody(elseBody, ctx), ctx);
        }

        bool firstFrame = ctx.Entered < 0 || ctx.Current != ctx.Entered;
        if (!firstFrame) return self;

        int entryNext = skipFirst ? NextOf(Body(state, 0)) : -1;
        ctx.Reenter = skipFirst && entryNext == self;
        if (ctx.Stopped) return -1;

        if (state.ControlPacket is { } cpHere) { ApplyPacket(cpHere); return self; }
        EndPacket();
        PacketEnded_23D8B0(); // Amedo

        if (HasSub(state) || state.NoneBlocking) return self;
        if (!skipFirst) return -1;

        int n = RunBody(Body(state, 0), ctx);
        if (n >= 0 && State(ctx, n)?.ControlPacket is { } ncp) ApplyPacket(ncp);
        ctx.Entered = ctx.Current;
        return n;
    }

    private bool NonBlockingPass()
    {
        for (int d = _lockDepth != 0xFF ? _lockDepth : 0; d <= ActiveDepth && d < Levels.Count; d++) // Amedo 2026-09-30
        {
            var ctx = Levels[d];
            if (State(ctx, ctx.Current) is { NoneBlocking: true } state && NonBlockingEvaluate(state, ctx)) return true;
        }
        return false;
    }

    private bool NonBlockingEvaluate(PS2BehaviourState state, Level ctx)
    {
        float best = -1e30f;
        PS2BehaviourStateBody? winner = null;
        for (int i = state.SkipsFirstStateBody ? 1 : 0; i < state.Bodies.Count; i++)
        {
            var body = Body(state, i);
            _body = body;
            var cond = body.Condition;
            if (cond is null) continue;
            float v = Evaluate(cond, ctx);
            bool ok = cond.NotGate ? v < cond.ReturnCheck : cond.ReturnCheck < v;
            if (Tracing) Trace($"  cond {cond.ConditionIndex}:{AgentLabWorld.ConditionName(cond.ConditionIndex)}({cond.Parameter}) = {v} {(cond.NotGate ? "<" : ">")} {cond.ReturnCheck} {(ok ? "ok" : "no")} -> {NextOf(body)}"); // Amedo
            if (!ok) continue;
            float score = (cond.NotGate ? cond.ReturnCheck - v : v - cond.ReturnCheck) * cond.ConditionPowerMultiplier;
            if (best < score) { best = score; winner = body; }
        }
        if (winner is null) return false;

        if (HasSub(state)) StopDeeper(ctx);
        EndPacket();
        int old  = ctx.Current;
        int next = RunBody(winner, ctx);
        if (next < 0) next = old;
        if (State(ctx, next)?.ControlPacket is { } cp) ApplyPacket(cp);
        Trace($"level {ctx.Depth}: state {old} -> {next} (non-blocking)");
        ctx.Current = next;
        ctx.Reenter = true;
        ctx.Entered = next != old ? old : -1;
        return true;
    }

    private int RunBody(PS2BehaviourStateBody body, Level ctx)
    {
        if (Tracing) Trace($"  body [{string.Join(",", body.Commands.OfType<PS2BehaviourCommand>().Select(c => $"{c.CommandIndex}:{AgentLabWorld.CommandName(c.CommandIndex)}"))}] -> {NextOf(body)}");
        ctx.Else = null;
        foreach (var c in body.Commands)
            if (c is PS2BehaviourCommand cmd) Execute(cmd, ctx);
        if ((body.Bitfield & 0x100) != 0) ctx.Reenter = true;
        ctx.EnterTime = World.Now;
        return NextOf(body);
    }

    private int AfterBody(int next, Level ctx)
    {
        if (next < 0) return -1;
        if (State(ctx, next)?.ControlPacket is { } cp) ApplyPacket(cp);
        return next;
    }
}
