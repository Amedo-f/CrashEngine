using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout
{
    public class PS2AnyAIPath : BaseTwinItem, ITwinAIPath
    {
        public UInt16[] Args { get; set; }

        public PS2AnyAIPath()
        {
            Args = new ushort[5];
        }

        public override int GetLength()
        {
            return 10;
        }

        public override void Read(BinaryReader reader, int length)
        {
            for (int i = 0; i < Args.Length; ++i)
            {
                Args[i] = reader.ReadUInt16();
            }
        }

        public override void Write(BinaryWriter writer)
        {
            for (int i = 0; i < Args.Length; ++i)
            {
                writer.Write(Args[i]);
            }
        }

        public override String GetName()
        {
            return $"AI Navigation Path {id:X}";
        }
    }
}
