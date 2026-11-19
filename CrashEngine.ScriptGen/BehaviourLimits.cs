using CrashEngine.Runtime.AgentLab.Compiled;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;

namespace CrashEngine.ScriptGen;

// Amedo 2026-11-18
public static class BehaviourLimits
{
    public const int MaxBodies = 0x1F, MaxCommands = 0xFF;
    private const ushort UnusedStateBits = 0x3E0;

    public readonly record struct Problem(int State, string Message);

    public static List<Problem> Check(PS2BehaviourGraph graph)
    {
        var problems = new List<Problem>();
        int states = graph.ScriptStates.Count;
        if (states == 0) problems.Add(new(-1, "the script has no states"));
        else if (graph.StartState < 0 || graph.StartState >= states) problems.Add(new(-1, $"it starts at state {graph.StartState}, but there are only {states} states (0-{states - 1})"));
        for (int s = 0; s < states; s++)
        {
            var state = (PS2BehaviourState)graph.ScriptStates[s];
            if (state.Bodies.Count > MaxBodies) problems.Add(new(s, $"has {state.Bodies.Count} choices (if blocks); a PS2 state holds at most {MaxBodies}"));
            if ((state.Unknown & ~UnusedStateBits) != 0) problems.Add(new(s, $"unused 0x{state.Unknown:X} has bits outside 0x{UnusedStateBits:X}"));
            for (int b = 0; b < state.Bodies.Count; b++)
            {
                var body = (PS2BehaviourStateBody)state.Bodies[b];
                if (body.Commands.Count > MaxCommands) problems.Add(new(s, $"choice {b + 1} has {body.Commands.Count} commands; a PS2 choice holds at most {MaxCommands}"));
                if (body.HasStateJump && (body.JumpToState < 0 || body.JumpToState >= states))
                    problems.Add(new(s, $"choice {b + 1} goes to state {body.JumpToState}, but there are only {states} states (0-{states - 1})"));
                foreach (var command in body.Commands.Cast<PS2BehaviourCommand>())
                {
                    uint size = PS2BehaviourCommand.GetCommandSize(command.CommandIndex);
                    if (size < 0xC) continue;
                    int words = (int)((size - 0xC) / 4);
                    if (command.Arguments.Count != words)
                        problems.Add(new(s, $"choice {b + 1}: {CommandName(command.CommandIndex)} has {command.Arguments.Count} words; the game reads {words}"));
                }
            }
        }
        return problems;
    }

    private static string CommandName(ushort index) => Enum.IsDefined(typeof(CommandId), index) ? ((CommandId)index).ToString() : $"command {index}";
}
