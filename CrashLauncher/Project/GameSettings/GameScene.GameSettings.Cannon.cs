using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private const uint RigidCannonActivatedId = 0xF33;
    private const uint AUnknownCannonCommandIndex = 96;
    private const int  CannonPowerArgIndex1 = 41;
    private const int  CannonPowerArgIndex2 = 43;

    private static List<ITwinBehaviourCommand> FindAllCannonCommands(BaseTwinSection? behSec)
    {
        var found = new List<ITwinBehaviourCommand>();
        var graph = behSec?.GetItem<PS2BehaviourGraph>(RigidCannonActivatedId);
        if (graph is null) return found;
        foreach (var state in graph.ScriptStates)
            foreach (var body in state.Bodies)
                foreach (var cmd in body.Commands)
                    if (cmd.CommandIndex == AUnknownCannonCommandIndex && cmd.Arguments.Count > CannonPowerArgIndex2)
                        found.Add(cmd);
        return found;
    }

    private readonly MultiplierSetting _cannonShotPower = new()
    {
        Name = "Cannon Shot Power",
        Find = FindAllCannonCommands, Arg1 = CannonPowerArgIndex1, Arg2 = CannonPowerArgIndex2,
        Missing = "Couldn't find the Rigid Cannon fire behaviour (0xF33) in this level.",
        Header = "Cannon Shot Power — CONFIRMED WORKING LIVE",
        Note = "Higher = fires farther. Too high and the shot comes out jammed/stuck instead.",
        Slider = "Power Multiplier##cannonpower",
        Row1 = "Shot {0} Power##cannonpower1_{0}", Row2 = "Shot {0} Power (secondary)##cannonpower2_{0}",
        ResetButton = "Reset##cannonreset",
    };
}
