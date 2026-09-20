using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class TwinJoint : ITwinSerializable
    {
        public Int32 ReactId { get; set; }
        public Int32 Index { get; set; }
        public Int32 ParentIndex { get; set; }
        public Int32 ChildrenAmt1 { get; set; }
        public Int32 ChildrenAmt2 { get; set; }
        public Vector4 LocalTranslation { get; set; }
        public Vector4 WorldTranslation { get; set; }
        public Vector4 LocalRotation { get; set; }
        public Vector4 UnusedRotation { get; set; }
        public Vector4 AdditionalAnimationRotation { get; set; }

        public TwinJoint()
        {
            LocalTranslation = new Vector4();
            WorldTranslation = new Vector4();
            LocalRotation = new Vector4();
            UnusedRotation = new Vector4();
            AdditionalAnimationRotation = new Vector4();
        }
        public int GetLength()
        {
            return Constants.SIZE_JOINT;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, int length)
        {
            ReactId = (Int32)(reader.ReadUInt32() & 0xFF);
            Index = (Int32)(reader.ReadUInt32() & 0xFF);
            ParentIndex = (Int32)(reader.ReadUInt32() & 0xFF);
            ChildrenAmt1 = (Int32)(reader.ReadUInt32() & 0xFF);
            ChildrenAmt2 = (Int32)(reader.ReadUInt32() & 0xFF);
            LocalTranslation.Read(reader, Constants.SIZE_VECTOR4);
            WorldTranslation.Read(reader, Constants.SIZE_VECTOR4);
            LocalRotation.Read(reader, Constants.SIZE_VECTOR4);
            UnusedRotation.Read(reader, Constants.SIZE_VECTOR4);
            AdditionalAnimationRotation.Read(reader, Constants.SIZE_VECTOR4);
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(ReactId);
            writer.Write(Index);
            writer.Write(ParentIndex);
            writer.Write(ChildrenAmt1);
            writer.Write(ChildrenAmt2);
            LocalTranslation.Write(writer);
            WorldTranslation.Write(writer);
            LocalRotation.Write(writer);
            UnusedRotation.Write(writer);
            AdditionalAnimationRotation.Write(writer);
        }
    }
}
