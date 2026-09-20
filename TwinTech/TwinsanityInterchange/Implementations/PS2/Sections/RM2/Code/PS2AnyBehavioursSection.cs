using System;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Code
{
    public class PS2AnyBehavioursSection : BaseTwinSection
    {
        public PS2AnyBehavioursSection() : base()
        {
            defaultType = typeof(TwinBehaviourWrapper);
            idToClassDictionary[0] = typeof(TwinBehaviourStarter);
            idToClassDictionary[1] = typeof(PS2BehaviourGraph);
        }
        protected override UInt32 ProcessId(UInt32 id)
        {
            return id % 2;
        }
    }
}
