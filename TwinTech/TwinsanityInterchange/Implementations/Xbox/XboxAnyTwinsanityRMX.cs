using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.RMX;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox
{
    public class XboxAnyTwinsanityRMX : BaseTwinSection
    {
        public XboxAnyTwinsanityRMX() : base()
        {
            idToClassDictionary.Add(Constants.LEVEL_GRAPHICS_SECTION, typeof(XboxAnyGraphicsSection));
            idToClassDictionary.Add(Constants.LEVEL_CODE_SECTION, typeof(XboxAnyCodeSection));
            idToClassDictionary.Add(Constants.LEVEL_LAYOUT_1_SECTION, typeof(XboxAnyLayoutSection));
            idToClassDictionary.Add(Constants.LEVEL_LAYOUT_2_SECTION, typeof(XboxAnyLayoutSection));
            idToClassDictionary.Add(Constants.LEVEL_LAYOUT_3_SECTION, typeof(XboxAnyLayoutSection));
            idToClassDictionary.Add(Constants.LEVEL_LAYOUT_4_SECTION, typeof(XboxAnyLayoutSection));
            idToClassDictionary.Add(Constants.LEVEL_LAYOUT_5_SECTION, typeof(XboxAnyLayoutSection));
            idToClassDictionary.Add(Constants.LEVEL_LAYOUT_6_SECTION, typeof(XboxAnyLayoutSection));
            idToClassDictionary.Add(Constants.LEVEL_LAYOUT_7_SECTION, typeof(XboxAnyLayoutSection));
            idToClassDictionary.Add(Constants.LEVEL_LAYOUT_8_SECTION, typeof(XboxAnyLayoutSection));
            idToClassDictionary.Add(Constants.LEVEL_PARTICLES_ITEM, typeof(XboxAnyParticleData));
            idToClassDictionary.Add(Constants.LEVEL_COLLISION_ITEM, typeof(XboxAnyCollisionData));
        }
    }
}
