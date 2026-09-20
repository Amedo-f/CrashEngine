using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Layout
{
    public class PS2AnyAIPositionsSection : BaseTwinSection
    {
        public PS2AnyAIPositionsSection() : base()
        {
            defaultType = typeof(PS2AnyAIPosition);
        }
    }
}
