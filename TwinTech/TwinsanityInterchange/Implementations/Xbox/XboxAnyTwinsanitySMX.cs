using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.SMX;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox
{
    public class XboxAnyTwinsanitySMX : BaseTwinSection
    {
        public XboxAnyTwinsanitySMX() : base()
        {
            idToClassDictionary.Add(Constants.SCENERY_GRAPHICS_SECTION, typeof(XboxAnyGraphicsSection));
            idToClassDictionary.Add(Constants.SCENERY_SECENERY_ITEM, typeof(XboxAnyScenery));
            idToClassDictionary.Add(Constants.SCENERY_UNK_1_ITEM, typeof(BaseTwinItem));
            idToClassDictionary.Add(Constants.SCENERY_UNK_2_ITEM, typeof(BaseTwinItem));
            idToClassDictionary.Add(Constants.SCENERY_UNK_3_ITEM, typeof(BaseTwinItem));
            idToClassDictionary.Add(Constants.SCENERY_DYNAMIC_SECENERY_ITEM, typeof(XboxAnyDynamicScenery));
            idToClassDictionary.Add(Constants.SCENERY_LINK_ITEM, typeof(XboxAnyLink));
        }
    }
}
