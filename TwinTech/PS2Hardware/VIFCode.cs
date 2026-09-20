using System;
using System.Diagnostics;
using System.IO;

namespace Twinsanity.PS2Hardware
{
    public class VIFCode
    {
        public Boolean Interrupt { get; set; }
        public VIFCodeEnum OP { get; set; }
        public Byte Amount { get; set; }
        public UInt16 Immediate { get; set; }

        public void Read(BinaryReader reader)
        {
            SetVIF(reader.ReadUInt32());
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(GetVIF());
        }

        public void SetVIF(UInt32 cmd)
        {
            Byte CMD = (Byte)((cmd & 0xFF000000) >> 24);
            Amount = (Byte)((cmd & 0x00FF0000) >> 16);
            Immediate = (UInt16)((cmd & 0x0000FFFF) >> 0);
            OP = (VIFCodeEnum)(CMD & 0b01111111);
            Interrupt = ((CMD & 0b10000000) != 0);
        }

        public UInt32 GetVIF()
        {
            Byte CMD = (Byte)OP;
            if (Interrupt)
            {
                CMD |= 0b10000000;
            }
            return (UInt32)CMD << 24 | (UInt32)Amount << 16 | (UInt32)Immediate << 0;
        }

        public void SetUnpackAddressMode(bool set)
        {
            Debug.Assert(IsUnpack(), "VIF Code must be in unpack mode for this operation");
            if (set)
            {
                Immediate |= (1 << 15);
                return;
            }

            Immediate &= unchecked((UInt16)(~(1U << 15)));
        }

        public void SetUnsignedDecompressMode(bool set)
        {
            Debug.Assert(IsUnpack(), "VIF Code must be in unpack mode for this operation");
            if (set)
            {
                Immediate |= (1 << 14);
                return;
            }

            Immediate &= unchecked((UInt16)(~(1U << 14)));
        }

        public bool IsUnpack()
        {
            return (OP & VIFCodeEnum.UNPACK) == VIFCodeEnum.UNPACK;
        }

        public UInt32 GetLength()
        {
            UInt32 packet_length;
            if (IsUnpack())
            {
                Byte cmd = (Byte)OP;
                Byte vn = (Byte)((cmd & 0b1100) >> 2);
                Byte vl = (Byte)((cmd & 0b0011) >> 0);
                Byte amount = Amount;
                UInt32 dimensions = (UInt32)(vn + 1);

                // Twinsanity always uses non filling mode so we don't need to know the state of the VIF and know which mode we are in
                UInt32 a = (UInt32)(32 >> vl);
                UInt32 b = dimensions;
                Single c = (Single)(a * b * amount);
                Single d = c / 32.0f;
                Single e = (Single)Math.Ceiling(d);
                UInt32 f = (UInt32)e;
                packet_length = (1 + f) * 4;
            }
            else
            {
                packet_length = OP switch
                {
                    VIFCodeEnum.STMASK => 8,
                    VIFCodeEnum.STROW or VIFCodeEnum.STCOL => 20,
                    _ => 4,
                };
            }

            return packet_length;
        }

        public void SetUnpackFormat(PackFormat format)
        {
            Debug.Assert(IsUnpack(), "Can not set unpack format for OP that isn't UNPACK");
            var newOp = (UInt32)OP | (UInt32)format;
            OP = (VIFCodeEnum)newOp;
        }
    }
    public enum VIFCodeEnum
    {
        NOP = 0b0000000,
        STCYCL = 0b0000001,
        OFFSET = 0b0000010,
        BASE = 0b0000011,
        ITOP = 0b0000100,
        STMOD = 0b0000101,
        MSKPATH3 = 0b0000110,
        MARK = 0b0000111,
        FLUSHE = 0b0010000,
        FLUSH = 0b0010001,
        FLUSHA = 0b0010011,
        MSCAL = 0b0010100,
        MSCNT = 0b0010111,
        MSCALF = 0b0010101,
        STMASK = 0b0100000,
        STROW = 0b0110000,
        STCOL = 0b0110001,
        MPG = 0b1001010,
        DIRECT = 0b1010000,
        DIRECTHL = 0b1010001,
        UNPACK = 0b1100000,
    }
}
