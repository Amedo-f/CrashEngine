using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private const uint EarthWormSquashLaunchId = 0x5E9;

    private static List<ITwinBehaviourCommand> FindAllWormCommands(BaseTwinSection? behSec)
    {
        var graph = behSec?.GetItem<PS2BehaviourGraph>(EarthWormSquashLaunchId);
        return FindApplyVelocityCommands(graph);
    }

    private readonly MultiplierSetting _wormLaunchPower = new()
    {
        Name = "Earth Worm Squash Launch Power",
        Find = FindAllWormCommands, Arg1 = SpringVelocityArgIndex1, Arg2 = SpringVelocityArgIndex2,
        Missing = "Couldn't find the Earth Worm squash-launch behaviour (0x5E9) in this level.",
        Header = "Earth Worm Squash Launch Power (not yet live-tested)",
        Slider = "Launch Multiplier##wormlaunch",
        Row1 = "Launch Power {0}##wormpower{0}", Row2 = "Launch Power {0} (secondary)##wormpower2_{0}",
        ResetButton = "Reset##wormreset",
    };
}
