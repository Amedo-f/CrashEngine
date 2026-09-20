using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.RMX.Code
{
    public class XboxAnyAnimationsSection : BaseTwinSection
    {
        public XboxAnyAnimationsSection() : base()
        {
            defaultType = typeof(XboxAnyAnimation);
        }
    }
}
