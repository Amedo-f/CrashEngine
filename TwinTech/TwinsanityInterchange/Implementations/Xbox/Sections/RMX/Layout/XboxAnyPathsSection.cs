using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.RMX.Layout
{
    public class XboxAnyPathsSection : BaseTwinSection
    {
        public XboxAnyPathsSection() : base()
        {
            defaultType = typeof(XboxAnyPath);
        }
    }
}
