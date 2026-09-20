using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class TwinIntegerRotation : ITwinSerializable
    {
        public UInt16 Angle { get; set; }
        public UInt16 Fract { get; set; }
        public int GetLength()
        {
            return Constants.SIZE_UINT32;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, int length)
        {
            Angle = reader.ReadUInt16();
            Fract = reader.ReadUInt16();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Angle);
            writer.Write(Fract);
        }

        public Single GetRotation()
        {
            // Amedo 2026-08-21 -- Math.Round (not Floor): Angle is truncated on encode, so Floor
            // dropped a whole extra degree ~half the time, compounding every Save.
            var result = (Single)Math.Round(Angle / (Single)UInt16.MaxValue * 360);
            result += (Fract / (Single)UInt16.MaxValue);
            return result;
        }

        public void SetRotation(Single angle)
        {
            // Amedo 2026-08-12 -- Fract via Floor (not Truncate): they disagree for negative angles,
            // wrapping Fract to ~65535 and drifting rotation ~1 degree per save.
            Angle = (UInt16)(Math.Floor(angle) / 360 * UInt16.MaxValue);
            Fract = (UInt16)((angle - Math.Floor(angle)) * UInt16.MaxValue);
        }
    }
}
