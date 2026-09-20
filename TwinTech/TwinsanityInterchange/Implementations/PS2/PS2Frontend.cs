using System.IO;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Code;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2
{
    public class PS2Frontend : PS2AnySoundsSection
    {
        public override void Write(BinaryWriter writer)
        {
            PreprocessWrite();
            base.Write(writer);
        }
    }
}
