using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives
{
    internal class PS2BH : ITwinSerializable
    {
        private Int32 Header;
        internal List<BHRecord> Records;

        public PS2BH()
        {
            Header = 0x501;
            Records = new List<BHRecord>();
        }

        public Int32 GetLength()
        {
            return 4 + Records.Sum(r => r.GetLength());
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            Header = reader.ReadInt32();
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var record = new BHRecord();
                record.Read(reader, 0);
                Records.Add(record);
            }
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Header);
            foreach (var r in Records)
            {
                r.Write(writer);
            }
        }
    }
}
