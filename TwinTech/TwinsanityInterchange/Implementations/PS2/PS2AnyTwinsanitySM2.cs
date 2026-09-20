using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2
{
    public class PS2AnyTwinsanitySM2 : BaseTwinSection
    {
        public PS2AnyTwinsanitySM2() : base()
        {
            idToClassDictionary.Add(Constants.SCENERY_GRAPHICS_SECTION, typeof(PS2AnyGraphicsSection));
            idToClassDictionary.Add(Constants.SCENERY_SECENERY_ITEM, typeof(PS2AnyScenery));
            idToClassDictionary.Add(Constants.SCENERY_UNK_1_ITEM, typeof(BaseTwinItem));
            idToClassDictionary.Add(Constants.SCENERY_UNK_2_ITEM, typeof(BaseTwinItem));
            idToClassDictionary.Add(Constants.SCENERY_UNK_3_ITEM, typeof(BaseTwinItem));
            idToClassDictionary.Add(Constants.SCENERY_DYNAMIC_SECENERY_ITEM, typeof(PS2AnyDynamicScenery));
            idToClassDictionary.Add(Constants.SCENERY_LINK_ITEM, typeof(PS2AnyLink));
        }

        public override void Write(BinaryWriter writer)
        {
            PrepareWrite();
            base.Write(writer);
        }
    }
}
