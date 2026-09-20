using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox
{
    public class XboxDefault : XboxAnyTwinsanityRMX
    {
        public XboxDefault() : base()
        {
            idToClassDictionary[Constants.LEVEL_PARTICLES_ITEM] = typeof(XboxDefaultParticleData);
            idToClassDictionary[Constants.LEVEL_COLLISION_ITEM] = typeof(BaseTwinSection);
        }
    }
}
