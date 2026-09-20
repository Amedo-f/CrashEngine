using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.RMX.Layout
{
    public class XboxAnyInstancesSection : BaseTwinSection
    {
        public XboxAnyInstancesSection() : base()
        {
            defaultType = typeof(XboxAnyInstance);
        }
    }
}
