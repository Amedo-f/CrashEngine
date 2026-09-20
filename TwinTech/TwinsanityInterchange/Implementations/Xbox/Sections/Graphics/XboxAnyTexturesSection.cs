using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.Graphics;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.Graphics
{
    public class XboxAnyTexturesSection : BaseTwinSection
    {
        public XboxAnyTexturesSection() : base()
        {
            defaultType = typeof(XboxAnyTexture);
        }

        protected override System.UInt32 GetMagicNumber()
        {
            return 0x3;
        }
    }
}
