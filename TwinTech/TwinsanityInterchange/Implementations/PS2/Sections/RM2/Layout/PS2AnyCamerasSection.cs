using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Layout
{
    public class PS2AnyCamerasSection : BaseTwinSection
    {
        public PS2AnyCamerasSection() : base()
        {
            defaultType = typeof(PS2AnyCamera);
        }
    }
}
