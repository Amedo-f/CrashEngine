using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics
{
    public class PS2AnyMeshesSection : BaseTwinSection
    {
        public PS2AnyMeshesSection() : base()
        {
            defaultType = typeof(PS2AnyMesh);
        }
        protected override System.UInt32 GetMagicNumber()
        {
            return 0x3;
        }
    }
}
