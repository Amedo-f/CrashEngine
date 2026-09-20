using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    internal class MHRecord : ITwinSerializable
    {
        public PS2MB.RecordType Type;
        public Int32 Size;
        public UInt32 Offset;
        public Int32 SampleRate;
        public Int32 UnkInt;

        public Int32 GetLength()
        {
            return 20;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            Type = (PS2MB.RecordType)reader.ReadInt32();
            Size = reader.ReadInt32();
            Offset = reader.ReadUInt32();
            SampleRate = reader.ReadInt32();
            UnkInt = reader.ReadInt32();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write((Int32)Type);
            writer.Write(Size);
            writer.Write(Offset);
            writer.Write(SampleRate);
            writer.Write(UnkInt);
        }

    }
}
