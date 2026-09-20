using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Layout
{
    public class PS2AnyInstancesSection : BaseTwinSection
    {
        public PS2AnyInstancesSection() : base()
        {
            defaultType = typeof(PS2AnyInstance);
        }
    }
}
