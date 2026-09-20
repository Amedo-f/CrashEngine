using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.RMX.Layout
{
    public class XboxAnyAIPathsSection : BaseTwinSection
    {
        public XboxAnyAIPathsSection() : base()
        {
            defaultType = typeof(XboxAnyAIPath);
        }
    }
}
