using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Layout
{
    public class PS2AnyPositionsSection : BaseTwinSection
    {
        public PS2AnyPositionsSection() : base()
        {
            defaultType = typeof(PS2AnyPosition);
        }
    }
}
