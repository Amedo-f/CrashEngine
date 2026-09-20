using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.Graphics;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.Graphics
{
    public class XboxAnySkydomesSection : BaseTwinSection
    {
        public XboxAnySkydomesSection() : base()
        {
            defaultType = typeof(XboxAnySkydome);
        }

        protected override System.UInt32 GetMagicNumber()
        {
            return 0x3;
        }
    }
}
