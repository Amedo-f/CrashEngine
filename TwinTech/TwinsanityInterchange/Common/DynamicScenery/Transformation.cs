using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.DynamicScenery
{
    public struct Transformation : ITwinSerializable // Amedo
    {
        public Single Value { get; set; }

        public Int32 GetLength()
        {
            return 4;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            Value = reader.ReadSingle();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Value);
        }
    }
}
