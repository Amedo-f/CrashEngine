using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives
{
    internal class PS2MH : ITwinSerializable
    {
        private Int32 Interleave;
        internal List<MHRecord> Records;

        public PS2MH()
        {
            Records = new List<MHRecord>();
        }

        public Int32 GetLength()
        {
            return 8 + Records.Sum(r => r.GetLength());
        }

        public void Compile()
        {
            return;
        }

        internal List<MHRecord> GetSortedRecords()
        {
            var resList = Records.ToList();
            resList.Sort(delegate (MHRecord r1, MHRecord r2)
            {
                return (Int32)r1.Offset - (Int32)r2.Offset;
            });
            return resList;
        }

        public UInt32 GetNewOffset()
        {
            var sortedList = GetSortedRecords();
            var lastElem = sortedList[sortedList.Count - 1];
            UInt32 newOffset = lastElem.Offset + (UInt32)lastElem.Size;
            newOffset += (0x800 - newOffset % 0x800); // Align to 2048
            return newOffset;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            var recs = reader.ReadInt32();
            Interleave = reader.ReadInt32();
            for (var i = 0; i < recs; ++i)
            {
                var r = new MHRecord();
                r.Read(reader, 20);
                Records.Add(r);
            }
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Records.Count);
            writer.Write(Interleave);
            foreach (var r in Records)
            {
                r.Write(writer);
            }
        }
    }
}
