using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics
{
    public class PS2AnyRigidModelsSection : BaseTwinSection
    {
        public PS2AnyRigidModelsSection() : base()
        {
            defaultType = typeof(PS2AnyRigidModel);
        }

        protected override System.UInt32 GetMagicNumber()
        {
            return 0x3;
        }
    }
}
