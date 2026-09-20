using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2
{
    public class PS2AnyTwinsanityRM2 : BaseTwinSection
    {
        public PS2AnyTwinsanityRM2() : base()
        {
            idToClassDictionary.Add(Constants.LEVEL_GRAPHICS_SECTION, typeof(PS2AnyGraphicsSection));
            idToClassDictionary.Add(Constants.LEVEL_CODE_SECTION, typeof(PS2AnyCodeSection));
            idToClassDictionary.Add(Constants.LEVEL_LAYOUT_1_SECTION, typeof(PS2AnyLayoutSection));
            idToClassDictionary.Add(Constants.LEVEL_LAYOUT_2_SECTION, typeof(PS2AnyLayoutSection));
            idToClassDictionary.Add(Constants.LEVEL_LAYOUT_3_SECTION, typeof(PS2AnyLayoutSection));
            idToClassDictionary.Add(Constants.LEVEL_LAYOUT_4_SECTION, typeof(PS2AnyLayoutSection));
            idToClassDictionary.Add(Constants.LEVEL_LAYOUT_5_SECTION, typeof(PS2AnyLayoutSection));
            idToClassDictionary.Add(Constants.LEVEL_LAYOUT_6_SECTION, typeof(PS2AnyLayoutSection));
            idToClassDictionary.Add(Constants.LEVEL_LAYOUT_7_SECTION, typeof(PS2AnyLayoutSection));
            idToClassDictionary.Add(Constants.LEVEL_LAYOUT_8_SECTION, typeof(PS2AnyLayoutSection));
            idToClassDictionary.Add(Constants.LEVEL_PARTICLES_ITEM, typeof(PS2AnyParticleData));
            idToClassDictionary.Add(Constants.LEVEL_COLLISION_ITEM, typeof(PS2AnyCollisionData));
        }

        public override void Write(BinaryWriter writer)
        {
            PrepareWrite();
            base.Write(writer);
        }
    }
}
