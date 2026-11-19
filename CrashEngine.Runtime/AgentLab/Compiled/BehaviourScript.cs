using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;

namespace CrashEngine.Runtime.AgentLab.Compiled;

// Amedo 2026-11-18
public abstract class BehaviourScript
{
    public static int Key(int state, int body) => state << 16 | body;
    public static int StateOf(int key) => key >> 16;

    public abstract int StartState { get; }
    public abstract int StateCount { get; }
    public abstract int BodyCount(int state);
    public abstract bool SkipsFirstBody(int state);
    public abstract bool NonBlocking(int state);
    public abstract int SubIndexOrSlot(int state);
    public abstract bool UsesObjectSlot(int state);
    public abstract TwinBehaviourControlPacket? Packet(int state);

    public abstract bool HasCondition(int key);
    public abstract bool NotGate(int key);
    public abstract float ReturnCheck(int key);
    public abstract float Power(int key);
    public abstract int Next(int key);
    public abstract bool Reenter(int key);
    public abstract float Condition(AgentLabAgent agent, int key, AgentLabAgent.Level ctx);
    public abstract void Commands(AgentLabAgent agent, int key, AgentLabAgent.Level ctx);

    public abstract string CommandsText(int key);
    public abstract string ConditionText(int key);

    public bool HasSub(int state) => state >= 0 && state < StateCount && SubIndexOrSlot(state) != -1;
}

// Amedo 2026-11-18
public class DataScript(PS2BehaviourGraph graph) : BehaviourScript
{
    public PS2BehaviourGraph Graph { get; } = graph;

    private PS2BehaviourState State(int state) => (PS2BehaviourState)Graph.ScriptStates[state];
    protected PS2BehaviourStateBody Body(int key) => (PS2BehaviourStateBody)State(key >> 16).Bodies[key & 0xFFFF];

    public override int StartState => Graph.StartState;
    public override int StateCount => Graph.ScriptStates.Count;
    public override int BodyCount(int state) => State(state).Bodies.Count;
    public override bool SkipsFirstBody(int state) => State(state).SkipsFirstStateBody;
    public override bool NonBlocking(int state) => State(state).NoneBlocking;
    public override int SubIndexOrSlot(int state) => State(state).BehaviourIndexOrSlot;
    public override bool UsesObjectSlot(int state) => State(state).UsesObjectSlot;
    public override TwinBehaviourControlPacket? Packet(int state) => State(state).ControlPacket;

    public override bool HasCondition(int key) => Body(key).Condition is not null;
    public override bool NotGate(int key) => Body(key).Condition!.NotGate;
    public override float ReturnCheck(int key) => Body(key).Condition!.ReturnCheck;
    public override float Power(int key) => Body(key).Condition!.ConditionPowerMultiplier;
    public override int Next(int key) => Body(key) is { HasStateJump: true } b ? b.JumpToState : -1;
    public override bool Reenter(int key) => (Body(key).Bitfield & 0x100) != 0;

    public override float Condition(AgentLabAgent agent, int key, AgentLabAgent.Level ctx) => agent.RunCondition(Body(key).Condition!, ctx);

    public override void Commands(AgentLabAgent agent, int key, AgentLabAgent.Level ctx)
    {
        foreach (var c in Body(key).Commands)
            if (c is PS2BehaviourCommand cmd) agent.RunCommand(cmd, ctx);
    }

    public override string CommandsText(int key) =>
        string.Join(",", Body(key).Commands.OfType<PS2BehaviourCommand>().Select(c => $"{c.CommandIndex}:{AgentLabWorld.CommandName(c.CommandIndex)}"));

    public override string ConditionText(int key) => Body(key).Condition is { } c ? $"{c.ConditionIndex}:{AgentLabWorld.ConditionName(c.ConditionIndex)}({c.Parameter})" : "-";
}
