using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.Graphics;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.Graphics
{
    public class XboxAnyMaterialsSection : BaseTwinSection
    {
        public XboxAnyMaterialsSection() : base()
        {
            defaultType = typeof(XboxAnyMaterial);
        }

        protected override System.UInt32 GetMagicNumber()
        {
            return 0x3;
        }
    }
}
