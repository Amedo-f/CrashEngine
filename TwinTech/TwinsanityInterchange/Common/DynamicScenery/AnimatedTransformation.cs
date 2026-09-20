using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.DynamicScenery
{
    public class AnimatedTransformation : ITwinSerializable
    {
        public List<Single> TransformationValues { get; set; }

        public AnimatedTransformation(UInt16 amount)
        {
            TransformationValues = new List<Single>(amount);
        }

        public Int32 GetLength()
        {
            return TransformationValues.Count * 4;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            for (Int32 i = 0; i < TransformationValues.Capacity; i++)
            {
                TransformationValues.Add(reader.ReadSingle());
            }
        }

        public void Write(BinaryWriter writer)
        {
            foreach (var value in TransformationValues)
            {
                writer.Write(value);
            }
        }
    }
}
