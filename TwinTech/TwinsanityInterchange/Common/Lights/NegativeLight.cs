using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace Twinsanity.TwinsanityInterchange.Common.Lights
{
    public class NegativeLight : Light
    {
        public Vector4 UnkVec3;
        public Single UnkFloat1;
        public Single UnkFloat2;
        public UInt32 UnkUInt1;
        public UInt32 UnkUInt2;
        public UInt16 UnkUShort1;
        public UInt16 UnkUShort2;

        public NegativeLight() : base()
        {
            UnkVec3 = new Vector4();
        }

        public override Int32 GetLength()
        {
            return base.GetLength() + Constants.SIZE_VECTOR4 + 2 * 4 + 2 * 4 + 2 * 2;
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            base.Read(reader, length);
            UnkVec3.Read(reader, Constants.SIZE_VECTOR4);
            UnkFloat1 = reader.ReadSingle();
            UnkFloat2 = reader.ReadSingle();
            UnkUInt1 = reader.ReadUInt32();
            UnkUInt2 = reader.ReadUInt32();
            UnkUShort1 = reader.ReadUInt16();
            UnkUShort2 = reader.ReadUInt16();
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            UnkVec3.Write(writer);
            writer.Write(UnkFloat1);
            writer.Write(UnkFloat2);
            writer.Write(UnkUInt1);
            writer.Write(UnkUInt2);
            writer.Write(UnkUShort1);
            writer.Write(UnkUShort2);
        }
    }
}
