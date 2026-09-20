using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common.Animation;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code
{
    public class PS2AnyAnimation : BaseTwinItem, ITwinAnimation
    {
        UInt32 bitfield;
        public Boolean HasAnimationData { get; set; }
        public Boolean HasFacialAnimationData { get; set; }
        public UInt16 TotalFrames { get; set; }
        public Byte DefaultFPS { get; set; }
        public TwinAnimation MainAnimation { get; set; }
        public TwinMorphAnimation FacialAnimation { get; set; }

        public PS2AnyAnimation()
        {
            MainAnimation = new TwinAnimation();
            FacialAnimation = new TwinMorphAnimation();
        }

        public override int GetLength()
        {
            return 4 + MainAnimation.GetLength() + FacialAnimation.GetLength();
        }

        public override void Read(BinaryReader reader, int length)
        {
            bitfield = reader.ReadUInt32();
            {
                HasAnimationData = (bitfield & 0x1) != 0;
                HasFacialAnimationData = (bitfield & 0x2) != 0;
                TotalFrames = (UInt16)((bitfield >> 0x2) & 0xFFFF);
                DefaultFPS = (Byte)((bitfield >> 0x12) & 0x1F);
            }
            MainAnimation.Read(reader, length);
            FacialAnimation.Read(reader, length);
        }

        public override void Write(BinaryWriter writer)
        {
            var hasAnimationData = HasAnimationData ? 1 : 0;
            var hasFacialAnimationData = HasFacialAnimationData ? 1 : 0;
            UInt32 newBitfield = (UInt32)hasAnimationData;
            newBitfield |= (UInt32)(hasFacialAnimationData << 1);
            newBitfield |= (UInt32)((TotalFrames & 0xFFFF) << 0x2);
            newBitfield |= (UInt32)((DefaultFPS & 0x1F) << 0x12);
            bitfield = newBitfield;

            writer.Write(bitfield);
            MainAnimation.Write(writer);
            FacialAnimation.Write(writer);
        }

        public override String GetName()
        {
            return $"Animation {id:X}";
        }
    }
}
