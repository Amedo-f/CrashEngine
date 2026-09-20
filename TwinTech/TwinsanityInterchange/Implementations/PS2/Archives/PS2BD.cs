using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives
{
    public class PS2BD : ITwinSerializable
    {
        private PS2BH Header;
        private String headerPath;
        private String headerWritePath;
        public List<BDRecord> Items;

        // A requirement to provide the header path
        public PS2BD(String headerPath, String headerWritePath)
        {
            this.headerWritePath = headerWritePath;
            this.headerPath = headerPath;
            Header = new PS2BH();
            Items = new List<BDRecord>();
        }

        public void BuildRecords(string folderSource)
        {
            var files = Directory.GetFiles(folderSource, "*.*", SearchOption.AllDirectories);
            var offset = 0;
            if (!folderSource.EndsWith("\\"))
            {
                folderSource += "\\";
            }
            
            foreach (var file in files)
            {
                var head = new BHRecord
                {
                    Length = (int)(new FileInfo(file)).Length,
                    Offset = offset,
                    Path = file.Replace(folderSource, "")
                };
                Header.Records.Add(head);
                using FileStream fs = new(file, FileMode.Open, FileAccess.Read);
                using BinaryReader br = new(fs);
                var last = new BDRecord(head, br.ReadBytes(head.Length));
                offset += head.Length;
                Items.Add(last);
            }
        }

        // Amedo 2026-09-01 -- for "New Game" mode: excludePath (optional) leaves selected original
        // records out of the built archive so a new project can reclaim their level slots.
        public void BuildRecordsWithFallback(string folderSource, string fallbackBhPath, string fallbackBdPath,
                                              Func<string, bool>? excludePath = null)
        {
            if (!File.Exists(fallbackBhPath) || !File.Exists(fallbackBdPath))
            {
                BuildRecords(folderSource);
                return;
            }

            var fallbackBh = new PS2BH();
            using (var bhStream = new FileStream(fallbackBhPath, FileMode.Open, FileAccess.Read))
            using (var bhReader = new BinaryReader(bhStream))
            {
                fallbackBh.Read(bhReader, (int)bhStream.Length);
            }

            if (!folderSource.EndsWith("\\") && !folderSource.EndsWith("/"))
                folderSource += "\\";

            using var bdStream = new FileStream(fallbackBdPath, FileMode.Open, FileAccess.Read);
            using var bdReader = new BinaryReader(bdStream);

            var offset = 0;
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in fallbackBh.Records)
            {
                var normalizedPath = record.Path.Replace('/', Path.DirectorySeparatorChar)
                                                .TrimStart(Path.DirectorySeparatorChar);
                seenPaths.Add(normalizedPath); // still "seen" even if excluded -- the 2nd pass below must never re-add it as a "new" file

                if (excludePath is not null && excludePath(normalizedPath))
                    continue; // genuinely dropped from the built archive -- not staged, not carried over from the fallback

                var localPath = Path.Combine(folderSource, normalizedPath);

                byte[] data;
                if (File.Exists(localPath))
                {
                    data = File.ReadAllBytes(localPath);
                }
                else
                {
                    bdStream.Position = record.Offset;
                    data = bdReader.ReadBytes(record.Length);
                }

                var newRecord = new BHRecord
                {
                    Path = record.Path,
                    Offset = offset,
                    Length = data.Length
                };
                Header.Records.Add(newRecord);
                Items.Add(new BDRecord(newRecord, data));
                offset += data.Length;
            }

            // Amedo 2026-08-13 -- second pass: append any staged file whose path wasn't in an original
            // record as a NEW record (the first loop only iterates original records, so New Scene files
            // under Levels\Custom\ were dropped).
            foreach (var file in Directory.GetFiles(folderSource, "*.*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(folderSource, file);
                if (seenPaths.Contains(rel)) continue;
                seenPaths.Add(rel);

                var data = File.ReadAllBytes(file);
                // Amedo 2026-09-15
                var newRecord = new BHRecord
                {
                    Path = rel.Replace('/', '\\'),
                    Offset = offset,
                    Length = data.Length
                };
                Header.Records.Add(newRecord);
                Items.Add(new BDRecord(newRecord, data));
                offset += data.Length;
            }
        }

        public Int32 GetLength()
        {
            return Items.Sum(i => i.Data.Length);
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            using FileStream headerStream = new(headerPath, FileMode.Open, FileAccess.Read);
            using BinaryReader headerReader = new(headerStream);
            Header.Read(headerReader, (Int32)headerStream.Length);

            foreach (var record in Header.Records)
            {
                reader.BaseStream.Position = record.Offset;
                var r = new BDRecord(record, reader.ReadBytes(record.Length));
                Items.Add(r);
            }
        }

        public void Write(BinaryWriter writer)
        {
            using FileStream stream = new(headerWritePath, FileMode.Create, FileAccess.Write);
            using BinaryWriter headerWriter = new(stream);
            Header.Write(headerWriter);

            foreach (var item in Items)
            {
                writer.Write(item.Data);
            }
        }
    }
}
