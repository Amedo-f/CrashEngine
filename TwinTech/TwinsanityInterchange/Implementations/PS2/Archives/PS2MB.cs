using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives
{
    public class PS2MB : ITwinSerializable
    {
        private PS2MH Header;
        private List<MBRecord> Items;
        private String headerPath;
        private String headerWritePath;

        // A requirement to provide the header path
        public PS2MB(String headerPath, String headerWritePath)
        {
            this.headerWritePath = headerWritePath;
            this.headerPath = headerPath;
            Header = new PS2MH();
            Items = new List<MBRecord>();
        }

        // Amedo 2026-08-12 -- reads an existing record's raw fields (for "copy audio from another
        // in-game track"); sample rate comes from the MUSIC.MH header (MBRecord.SampleRate is 0 for stereo).
        public (RecordType Type, Byte[] Data, Int32 SampleRate, String Name) GetRecordData(Int32 id)
        {
            if (id < 0 || id >= Items.Count)
                throw new ArgumentOutOfRangeException(nameof(id));
            var item = Items[id];
            return (Header.Records[id].Type, item.TrackData, Header.Records[id].SampleRate, item.Name);
        }

        public void RemoveRecord(Int32 id)
        {
            if (id >= Items.Count) return;
            Items.RemoveAt(id);
            Header.Records.RemoveAt(id);
        }

        // Amedo 2026-08-12 -- replaces a record's audio in place, keeping the same index (so other
        // levels' BeginMusic ids stay valid); appends the new audio at a fresh unused offset.
        public void ReplaceRecord(Int32 id, RecordType type, String name, Byte[] data, Int32 sampleRate)
        {
            if (id < 0 || id >= Items.Count)
                throw new ArgumentOutOfRangeException(nameof(id));
            if (name == "undefined")
                throw new ArgumentException("Argument can not be 'undefined' because it is reserved!", nameof(name));

            var newHeadRec = new MHRecord
            {
                Type = type,
                Offset = Header.GetNewOffset(),
                SampleRate = sampleRate,
                Size = type == RecordType.MONO ? data.Length + 0x30 : data.Length,
                UnkInt = 0,
            };
            Header.Records[id] = newHeadRec;

            var paddedName = name.Length > 0x10 ? name.Substring(0, 0x10) : name;
            while (paddedName.Length < 0x10) paddedName += '\0';

            Items[id] = new MBRecord(newHeadRec)
            {
                TrackData = data,
                SampleRate = sampleRate,
                Name = paddedName,
            };
        }

        // Amedo 2026-08-22 -- returns the new record's id so a caller wanting a fresh slot can
        // repoint references at it.
        public Int32 AddRecord(RecordType type, String Name, Byte[] data, Int32 sampleRate)
        {
            if (Name == "undefined")
            {
                throw new ArgumentException("Argument can not be 'undefined' because it is reserved!", "Name");
            }
            var newHeadRec = new MHRecord
            {
                Type = type,
                Offset = Header.GetNewOffset(),
                SampleRate = sampleRate,
                Size = type == RecordType.MONO ? data.Length + 0x30 : data.Length,
                UnkInt = 0
            };
            Header.Records.Add(newHeadRec);
            var newRec = new MBRecord(newHeadRec)
            {
                TrackData = data,
                SampleRate = sampleRate,
                Name = new String(Name.ToCharArray(0, Math.Min(Name.Length, 0x10)))
            };
            if (newRec.Name.Length != 0x10)
            {
                for (var i = newRec.Name.Length; i < 0x10; ++i)
                {
                    newRec.Name += '\0';
                }
            }
            Items.Add(newRec);
            return Items.Count - 1;
        }

        public Int32 GetLength()
        {
            return Items.Sum(r => r.GetLength());
        }

        public void Compile()
        {
            return;
        }

        // Amedo 2026-08-12 -- read records in index order so Items[i] matches Header.Records[i]
        // (the old offset-sorted order let ReplaceRecord(id) overwrite the wrong track).
        public void Read(BinaryReader reader, Int32 length)
        {
            using (FileStream headerStream = new FileStream(headerPath, FileMode.Open, FileAccess.Read))
            using (BinaryReader headerReader = new BinaryReader(headerStream))
            {
                Header.Read(headerReader, (Int32)headerStream.Length);
            }
            foreach (var record in Header.Records)
            {
                reader.BaseStream.Position = record.Offset;
                var r = new MBRecord(record);
                r.Read(reader, record.Size);
                Items.Add(r);
            }
        }

        // Amedo 2026-08-12 -- recompute every record's real offset/size before writing (two passes);
        // the old code only fixed the one touched record, so any size change shifted the rest.
        public void Write(BinaryWriter writer)
        {
            UInt32 offset = 0;
            for (int i = 0; i < Items.Count; i++)
            {
                // Header.Records[i] and Items[i]'s own RecordHeader are the SAME MHRecord object
                // (see ReplaceRecord/AddRecord/Read) — updating it here through either list is
                // visible to both, including MBRecord.GetLength()'s own RecordHeader.Size read.
                var len = (UInt32)Items[i].GetLength();
                Header.Records[i].Offset = offset;
                offset += len;
                var pad = (0x800 - offset % 0x800) % 0x800;
                offset += pad;
            }

            using (FileStream stream = new FileStream(headerWritePath, FileMode.Create, FileAccess.Write))
            using (BinaryWriter headerWriter = new BinaryWriter(stream))
            {
                Header.Write(headerWriter);
            }
            foreach (var r in Items)
            {
                r.Write(writer);
                // Padding
                while (writer.BaseStream.Position % 0x800 != 0)
                {
                    writer.Write((Byte)0);
                }
            }
        }

        public enum RecordType
        {
            MONO,
            STEREO,
            NULL
        }
    }
}
