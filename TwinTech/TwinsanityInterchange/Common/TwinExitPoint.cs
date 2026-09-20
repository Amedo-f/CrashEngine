using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class TwinExitPoint : ITwinSerializable
    {
        public UInt32 ParentJointIndex { get; set; }
        public UInt32 ID { get; set; }
        public Matrix4 Matrix { get; set; }
        public TwinExitPoint()
        {
            Matrix = new Matrix4();
        }
        public int GetLength()
        {
            return Constants.SIZE_EXIT_POINT;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, int length)
        {
            ParentJointIndex = reader.ReadUInt32();
            ID = reader.ReadUInt32();

            Matrix.Read(reader, Constants.SIZE_MATRIX4);
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(ParentJointIndex);
            writer.Write(ID);
            Matrix.Write(writer);
        }
    }
}
