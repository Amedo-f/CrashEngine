using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Code
{
    public class PS2AnyBehaviourCommandsSequencesSection : BaseTwinSection
    {
        public PS2AnyBehaviourCommandsSequencesSection() : base()
        {
            defaultType = typeof(PS2BehaviourCommandsSequence);
        }
    }
}
