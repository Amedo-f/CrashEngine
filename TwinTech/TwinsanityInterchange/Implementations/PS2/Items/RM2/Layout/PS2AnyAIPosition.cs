using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout
{
    public class PS2AnyAIPosition : BaseTwinItem, ITwinAIPosition
    {
        public Vector4 Position { get; set; }
        public UInt16 UnkShort { get; set; }

        public PS2AnyAIPosition()
        {
            Position = new Vector4();
        }

        public override int GetLength()
        {
            return Constants.SIZE_VECTOR4 + 2;
        }

        public override void Read(BinaryReader reader, int length)
        {
            Position.Read(reader, Constants.SIZE_VECTOR4);
            UnkShort = reader.ReadUInt16();
        }
        public override void Write(BinaryWriter writer)
        {
            Position.Write(writer);
            writer.Write(UnkShort);
        }

        public override String GetName()
        {
            return $"AI Navigation Position {id:X}";
        }
    }
}
