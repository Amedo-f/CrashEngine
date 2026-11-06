using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private const uint IronSpringCrateLandedOnId   = 0x5F;

    private static List<ITwinBehaviourCommand> FindAllSpringCommands(BaseTwinSection? behSec)
    {
        var ironGraph = behSec?.GetItem<PS2BehaviourGraph>(IronSpringCrateLandedOnId);
        return FindApplyVelocityCommands(ironGraph);
    }

    private readonly MultiplierSetting _springCrateBounce = new()
    {
        Name = "Spring Crate Bounce Height",
        Global = true,
        Find = FindAllSpringCommands, Arg1 = SpringVelocityArgIndex1, Arg2 = SpringVelocityArgIndex2,
        Missing = "Couldn't find the Iron Spring Crate bounce behaviour (0x5F) in Startup\\Default.rm2.",
        Header = "Iron Spring Crate Bounce Height — CONFIRMED WORKING LIVE",
        Slider = "Bounce Multiplier##springbounce",
        Row1 = "Launch Power {0}##springpower{0}", Row2 = "Launch Power {0} (secondary)##springpower2_{0}",
        ResetButton = "Reset##springreset",
    };
}
