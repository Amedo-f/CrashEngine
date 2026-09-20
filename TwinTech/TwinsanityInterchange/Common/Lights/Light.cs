using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.Lights
{
    public class Light : ITwinSerializable
    {
        public UInt32 UnkData;
        public Single Radius;
        public Vector4 Color;
        public Vector4 Position;
        public Vector4 UnkVec1;
        public Vector4 UnkVec2;

        public Light()
        {
            Color = new Vector4();
            Position = new Vector4();
            UnkVec1 = new Vector4();
            UnkVec2 = new Vector4();
        }

        public virtual Int32 GetLength()
        {
            return 8 + 4 * Constants.SIZE_VECTOR4;
        }

        public void Compile()
        {
            return;
        }

        public virtual void Read(BinaryReader reader, Int32 length)
        {
            UnkData = reader.ReadUInt32();
            Radius = reader.ReadSingle();
            Color.Read(reader, Constants.SIZE_VECTOR4);
            Position.Read(reader, Constants.SIZE_VECTOR4);
            UnkVec1.Read(reader, Constants.SIZE_VECTOR4);
            UnkVec2.Read(reader, Constants.SIZE_VECTOR4);
        }

        public virtual void Write(BinaryWriter writer)
        {
            writer.Write(UnkData);
            writer.Write(Radius);
            Color.Write(writer);
            Position.Write(writer);
            UnkVec1.Write(writer);
            UnkVec2.Write(writer);
        }
    }
}
