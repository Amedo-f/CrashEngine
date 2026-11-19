using CrashEngine.Runtime.AgentLab.Compiled;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-26
public sealed partial class AgentLabAgent
{
    private int _body = -1; // Amedo 2026-11-18

    // Amedo 2026-11-18
    private static bool Valid(Level ctx, int state) => ctx.Script is { } s && state >= 0 && state < s.StateCount;

    // Amedo 2026-11-18
    private static bool HasSub(Level ctx, int state) => Valid(ctx, state) && ctx.Script!.SubIndexOrSlot(state) != -1;

    // Amedo 2026-11-18
    private static TwinBehaviourControlPacket? PacketOf(Level ctx, int state) => Valid(ctx, state) ? ctx.Script!.Packet(state) : null;

    // Amedo 2026-11-04: the script's C# translation (CrashEngine.ScriptGen) runs a body's condition and commands when there is
    // one for this graph; otherwise the interpreter (Evaluate / Execute) as before. Same objects, same order.
    public float RunCondition(TwinBehaviourCondition cond, Level ctx) => Evaluate(cond, ctx);
    public void RunCommand(PS2BehaviourCommand cmd, Level ctx) => Execute(cmd, ctx);

    private int _lockDepth = 0xFF; // Amedo 2026-09-30

    private void RunStateStack(int depth, TwinBehaviourControlPacket? t0)
    {
        var ctx = Levels[depth];
        if (_lockDepth != 0xFF && depth < _lockDepth) // Amedo 2026-09-30
        {
            if (!HasSub(ctx, ctx.Current) || depth + 1 >= Levels.Count) return;
            RunStateStack(depth + 1, t0);
            return;
        }
        if (ctx.Stopped) return;

        int s1 = EvaluateState(ctx, t0);
        if (s1 < 0) Stop(ctx, depth);
        else if (Valid(ctx, s1) && !HasSub(ctx, s1) && ctx.Script!.Packet(s1) is null && ctx.Script.BodyCount(s1) == 0)
        {
            Stop(ctx, depth);
            s1 = -1;
        }

        if (!ctx.Stopped && HasSub(ctx, s1))
        {
            int d = depth + 1;
            if (s1 != ctx.Entered)
            {
                OpenLevel(d, ctx.Script!, s1);
                ctx.Current = s1;
            }
            if (d < Levels.Count)
            {
                RunStateStack(d, t0);
                if (Levels[d].Stopped)
                {
                    s1 = ctx.Script!.BodyCount(s1) > 0 ? RunBody(BehaviourScript.Key(s1, 0), ctx) : -1;
                    if (s1 < 0) Stop(ctx, depth);
                    else if (PacketOf(ctx, s1) is { } cp) ApplyPacket(cp);
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

    private void OpenLevel(int depth, BehaviourScript script, int state)
    {
        var parent = Levels[depth - 1];
        int sub    = script.SubIndexOrSlot(state); // Amedo 2026-11-18
        var graph  = script.UsesObjectSlot(state) ? RootController().Slot(sub).Graph : World.Behaviour((ushort)sub); // Amedo
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
        if (!Valid(ctx, ctx.Current)) return -1;
        var script = ctx.Script!; // Amedo 2026-11-18
        int self = ctx.Current;
        bool skipFirst = script.SkipsFirstBody(self);
        _body = -1;

        if (script.Packet(self) is { } own && ReferenceEquals(own, t0))
        {
            if (!skipFirst) return -1;
            ctx.Entered = self;
            return AfterBody(RunBody(BehaviourScript.Key(self, 0), ctx), ctx);
        }

        int first = skipFirst ? 1 : 0;
        int count = script.BodyCount(self);
        if (first < count)
        {
            float best = -1e30f;
            int winner = -1;
            for (int i = first; i < count; i++)
            {
                int key = BehaviourScript.Key(self, i);
                _body = key;
                if (!script.HasCondition(key)) continue;
                float v = script.Condition(this, key, ctx); // Amedo 2026-11-04
                bool notGate = script.NotGate(key);
                float check = script.ReturnCheck(key);
                bool ok = notGate ? v < check : check < v;
                if (!ok) continue;
                float score = (notGate ? check - v : v - check) * script.Power(key);
                if (best < score) { best = score; winner = key; }
            }

            if (winner >= 0)
            {
                if (script.HasSub(self)) StopDeeper(ctx);
                else EndPacket();
                return AfterBody(RunBody(winner, ctx), ctx);
            }

            if (ctx.Else >= 0)
                return AfterBody(RunBody(ctx.Else, ctx), ctx);
        }

        bool firstFrame = ctx.Entered < 0 || ctx.Current != ctx.Entered;
        if (!firstFrame) return self;

        int entryNext = skipFirst ? script.Next(BehaviourScript.Key(self, 0)) : -1;
        ctx.Reenter = skipFirst && entryNext == self;
        if (ctx.Stopped) return -1;

        if (script.Packet(self) is { } cpHere) { ApplyPacket(cpHere); return self; }
        EndPacket();
        PacketEnded_23D8B0(); // Amedo

        if (script.HasSub(self) || script.NonBlocking(self)) return self;
        if (!skipFirst) return -1;

        int n = RunBody(BehaviourScript.Key(self, 0), ctx);
        if (n >= 0 && PacketOf(ctx, n) is { } ncp) ApplyPacket(ncp);
        ctx.Entered = ctx.Current;
        return n;
    }

    private bool NonBlockingPass()
    {
        for (int d = _lockDepth != 0xFF ? _lockDepth : 0; d <= ActiveDepth && d < Levels.Count; d++) // Amedo 2026-09-30
        {
            var ctx = Levels[d];
            if (Valid(ctx, ctx.Current) && ctx.Script!.NonBlocking(ctx.Current) && NonBlockingEvaluate(ctx.Script, ctx.Current, ctx)) return true;
        }
        return false;
    }

    private bool NonBlockingEvaluate(BehaviourScript script, int state, Level ctx)
    {
        float best = -1e30f;
        int winner = -1;
        int count = script.BodyCount(state);
        for (int i = script.SkipsFirstBody(state) ? 1 : 0; i < count; i++)
        {
            int key = BehaviourScript.Key(state, i);
            _body = key;
            if (!script.HasCondition(key)) continue;
            float v = script.Condition(this, key, ctx); // Amedo 2026-11-04
            bool notGate = script.NotGate(key);
            float check = script.ReturnCheck(key);
            bool ok = notGate ? v < check : check < v;
            if (Tracing) Trace($"  cond {script.ConditionText(key)} = {v} {(notGate ? "<" : ">")} {check} {(ok ? "ok" : "no")} -> {script.Next(key)}"); // Amedo
            if (!ok) continue;
            float score = (notGate ? check - v : v - check) * script.Power(key);
            if (best < score) { best = score; winner = key; }
        }
        if (winner < 0) return false;

        if (script.HasSub(state)) StopDeeper(ctx);
        EndPacket();
        int old  = ctx.Current;
        int next = RunBody(winner, ctx);
        if (next < 0) next = old;
        if (PacketOf(ctx, next) is { } cp) ApplyPacket(cp);
        Trace($"level {ctx.Depth}: state {old} -> {next} (non-blocking)");
        ctx.Current = next;
        ctx.Reenter = true;
        ctx.Entered = next != old ? old : -1;
        return true;
    }

    private int RunBody(int key, Level ctx)
    {
        var script = ctx.Script!; // Amedo 2026-11-18
        if (Tracing) Trace($"  body [{script.CommandsText(key)}] -> {script.Next(key)}");
        ctx.Else = -1;
        script.Commands(this, key, ctx); // Amedo 2026-11-18
        if (script.Reenter(key)) ctx.Reenter = true;
        ctx.EnterTime = World.Now;
        return script.Next(key);
    }

    private int AfterBody(int next, Level ctx)
    {
        if (next < 0) return -1;
        if (PacketOf(ctx, next) is { } cp) ApplyPacket(cp);
        return next;
    }
}
