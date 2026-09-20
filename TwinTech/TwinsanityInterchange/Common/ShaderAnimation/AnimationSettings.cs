using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces;
using static Twinsanity.TwinsanityInterchange.Common.Animation.Enums;

namespace Twinsanity.TwinsanityInterchange.Common.ShaderAnimation
{
    public class AnimationSettings : ITwinSerializable
    {
        UInt16 transformationChoice;
        public TransformType TranslateX { get; set; }
        public TransformType TranslateY { get; set; }
        public TransformType ColorR { get; set; }
        public TransformType ColorG { get; set; }
        public TransformType ColorB { get; set; }
        public TransformType ColorA { get; set; }

        public UInt16 StaticTransformationIndex { get; set; }
        public UInt16 AnimationTransformationIndex { get; set; }

        public Int32 GetLength() => 8;

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            reader.ReadUInt16(); // Unused flags
            transformationChoice = reader.ReadUInt16();
            {
                TranslateX = (TransformType)(transformationChoice & 0x1);
                TranslateY = (TransformType)((transformationChoice & 0x2) >> 0x1);
                ColorR = (TransformType)((transformationChoice & 0x4) >> 0x2);
                ColorG = (TransformType)((transformationChoice & 0x8) >> 0x3);
                ColorB = (TransformType)((transformationChoice & 0x10) >> 0x4);
                ColorA = (TransformType)((transformationChoice & 0x20) >> 0x5);
            }
            StaticTransformationIndex = reader.ReadUInt16();
            AnimationTransformationIndex = reader.ReadUInt16();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write((UInt16)0);

            UInt16 newTransformationChoice = (UInt16)TranslateX;
            newTransformationChoice |= (UInt16)((UInt16)TranslateY << 0x1);
            newTransformationChoice |= (UInt16)((UInt16)ColorR << 0x2);
            newTransformationChoice |= (UInt16)((UInt16)ColorG << 0x3);
            newTransformationChoice |= (UInt16)((UInt16)ColorB << 0x4);
            newTransformationChoice |= (UInt16)((UInt16)ColorA << 0x5);
            transformationChoice = newTransformationChoice;
            writer.Write(transformationChoice);

            writer.Write(StaticTransformationIndex);
            writer.Write(AnimationTransformationIndex);
        }
    }
}
