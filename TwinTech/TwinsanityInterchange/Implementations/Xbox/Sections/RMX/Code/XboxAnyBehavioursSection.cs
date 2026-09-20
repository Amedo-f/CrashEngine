using System;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.RMX.Code
{
    public class XboxAnyBehavioursSection : BaseTwinSection
    {
        public XboxAnyBehavioursSection() : base()
        {
            defaultType = typeof(TwinBehaviourWrapper);
            idToClassDictionary[0] = typeof(TwinBehaviourStarter);
            idToClassDictionary[1] = typeof(XboxBehaviourGraph);
        }
        protected override UInt32 ProcessId(UInt32 id)
        {
            return id % 2;
        }
    }
}
