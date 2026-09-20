using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.Collision
{
    public class TwinGroupInformation : ITwinSerializable
    {
        public UInt32 Size;
        public UInt32 Offset;

        public Int32 GetLength()
        {
            return 8;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            Size = reader.ReadUInt32();
            Offset = reader.ReadUInt32();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Size);
            writer.Write(Offset);
        }
    }
}
