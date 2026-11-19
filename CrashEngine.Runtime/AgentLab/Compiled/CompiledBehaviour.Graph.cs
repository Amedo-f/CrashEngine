using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;

namespace CrashEngine.Runtime.AgentLab.Compiled;

public abstract partial class CompiledBehaviour
{
    // Amedo 2026-11-18
    public virtual byte GraphPriority => 0;

    // Amedo 2026-11-18
    public PS2BehaviourGraph ToGraph(uint id)
    {
        var table = Tab;
        var graph = new PS2BehaviourGraph { Name = Name, StartState = table.StartIndex, Priority = GraphPriority };
        graph.SetID(id);
        graph.Compile();
        foreach (var state in table.States)
        {
            var ps = new PS2BehaviourState
            {
                Unknown = state.Unused,
                SkipsFirstStateBody = state.SkipFirst,
                NoneBlocking = state.NonBlocking,
                UsesObjectSlot = state.ObjectSlot,
                BehaviourIndexOrSlot = (short)state.Sub,
                ControlPacket = state.Packet,
            };
            foreach (var body in state.Bodies)
            {
                var pb = new PS2BehaviourStateBody
                {
                    HasStateJump = body.Next >= 0,
                    JumpToState = body.Next >= 0 ? body.Next : 0,
                    Unknown = body.Reenter,
                    Condition = body.Condition,
                };
                pb.Commands.AddRange(body.Commands);
                ps.Bodies.Add(pb);
            }
            graph.ScriptStates.Add(ps);
        }
        return graph;
    }
}
