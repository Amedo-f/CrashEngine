using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2
{
    public class PS2Default : PS2AnyTwinsanityRM2
    {
        public PS2Default() : base()
        {
            idToClassDictionary[Constants.LEVEL_PARTICLES_ITEM] = typeof(PS2DefaultParticleData);
            idToClassDictionary[Constants.LEVEL_COLLISION_ITEM] = typeof(BaseTwinSection);
        }
    }
}
