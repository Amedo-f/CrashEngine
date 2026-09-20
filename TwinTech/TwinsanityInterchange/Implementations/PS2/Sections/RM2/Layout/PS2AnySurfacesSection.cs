using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Layout
{
    public class PS2AnySurfacesSection : BaseTwinSection
    {
        public PS2AnySurfacesSection() : base()
        {
            defaultType = typeof(PS2AnyCollisionSurface);
        }
    }
}
