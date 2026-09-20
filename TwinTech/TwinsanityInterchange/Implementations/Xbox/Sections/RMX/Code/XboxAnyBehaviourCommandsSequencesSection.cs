using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.RMX.Code
{
    public class XboxAnyBehaviourCommandsSequencesSection : BaseTwinSection
    {
        public XboxAnyBehaviourCommandsSequencesSection() : base()
        {
            defaultType = typeof(XboxBehaviourCommandsSequence);
        }
    }
}
