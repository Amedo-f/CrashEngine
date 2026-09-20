using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.RMX.Layout
{
    public class XboxAnyTemplatesSection : BaseTwinSection
    {
        public XboxAnyTemplatesSection() : base()
        {
            defaultType = typeof(XboxAnyTemplate);
        }
    }
}
