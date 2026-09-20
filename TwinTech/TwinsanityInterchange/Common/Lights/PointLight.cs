using System;
using System.IO;

namespace Twinsanity.TwinsanityInterchange.Common.Lights
{
    public class PointLight : Light
    {
        public Int16 UnkShort;

        public override Int32 GetLength()
        {
            return base.GetLength() + 2;
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            base.Read(reader, length);
            UnkShort = reader.ReadInt16();
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            writer.Write(UnkShort);
        }
    }
}
