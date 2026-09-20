using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.ShaderAnimation
{
    public class TwinShaderAnimation : ITwinSerializable
    {
        UInt32 dataPacker;
        public UInt32 Header { get; set; }
        public UInt16 TotalFrames { get; set; }
        public List<AnimationSettings> AnimationSettings { get; set; } = new();
        public List<Transformation> StaticTransformations { get; set; } = new();
        public List<AnimatedTransformation> AnimatedTransformations { get; set; } = new();

        public Int32 GetLength()
        {
            return 10 + AnimationSettings.Sum(a => a.GetLength()) + StaticTransformations.Count * 2 + AnimatedTransformations.Sum(a => a.GetLength());
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            Header = reader.ReadUInt32();
            dataPacker = reader.ReadUInt32();
            TotalFrames = reader.ReadUInt16();
            var animationSettings = (dataPacker & 0x7F);
            var staticTransformations = ((dataPacker >> 0xA) & 0xFFE) / 2;
            var animatedTransformations = (dataPacker >> 0x16);

            AnimationSettings.Clear();
            AnimationSettings.Capacity = (Int32)animationSettings;
            for (Int32 i = 0; i < animationSettings; i++)
            {
                var setting = new AnimationSettings();
                setting.Read(reader, length);
                AnimationSettings.Add(setting);
            }

            StaticTransformations.Clear();
            StaticTransformations.Capacity = (Int32)staticTransformations;
            for (Int32 i = 0; i < staticTransformations; i++)
            {
                var staticTransformation = new Transformation();
                staticTransformation.Read(reader, length);
                StaticTransformations.Add(staticTransformation);
            }

            AnimatedTransformations.Clear();
            AnimatedTransformations.Capacity = TotalFrames;
            for (Int32 i = 0; i < TotalFrames; i++)
            {
                var animatedTransformation = new AnimatedTransformation((UInt16)animatedTransformations);
                animatedTransformation.Read(reader, length);
                AnimatedTransformations.Add(animatedTransformation);
            }
        }

        public void Compile()
        {
            return;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Header);

            UInt32 packer = (UInt32)AnimationSettings.Count & 0x7F;
            packer |= (UInt32)(((StaticTransformations.Count * 2) & 0xFFE) << 0xA);
            if (AnimatedTransformations.Count > 0)
            {
                packer |= (UInt32)(AnimatedTransformations[0].Count << 0x16);
            }
            dataPacker = packer;
            writer.Write(dataPacker);

            writer.Write(TotalFrames);

            foreach (var setting in AnimationSettings)
            {
                setting.Write(writer);
            }

            foreach (var staticTransformation in StaticTransformations)
            {
                staticTransformation.Write(writer);
            }

            foreach (var animatedTransformation in AnimatedTransformations)
            {
                animatedTransformation.Write(writer);
            }
        }
    }
}
