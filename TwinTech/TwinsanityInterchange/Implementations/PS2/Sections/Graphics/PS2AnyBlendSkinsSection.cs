using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics
{
    public class PS2AnyBlendSkinsSection : BaseTwinSection
    {
        public PS2AnyBlendSkinsSection() : base()
        {
            defaultType = typeof(PS2AnyBlendSkin);
        }

        protected override System.UInt32 GetMagicNumber()
        {
            return 0x3;
        }
    }
}
