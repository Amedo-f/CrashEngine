using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2
{
    public class PS2PSF : BaseTwinItem, ITwinPSF
    {
        public List<ITwinPTC> FontPages { get; set; } = new();
        public List<Vector4> UnkVecs { get; set; } = new();
        public Int32 UnkInt { get; set; }

        public override Int32 GetLength()
        {
            return 12 + FontPages.Sum(f => f.GetLength()) + UnkVecs.Count * Constants.SIZE_VECTOR4;
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            var pages = reader.ReadInt32();
            for (var i = 0; i < pages; ++i)
            {
                var page = new PS2PTC();
                page.Read(reader, 0);
                FontPages.Add(page);
            }
            var vecAmt = reader.ReadInt32();
            UnkInt = reader.ReadInt32();
            for (var i = 0; i < vecAmt; ++i)
            {
                var vec = new Vector4();
                vec.Read(reader, Constants.SIZE_VECTOR4);
                UnkVecs.Add(vec);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(FontPages.Count);
            foreach (var page in FontPages)
            {
                page.Write(writer);
            }
            writer.Write(UnkVecs.Count);
            writer.Write(UnkInt);
            foreach (var v in UnkVecs)
            {
                v.Write(writer);
            }
        }

        public override String GetName()
        {
            return "Playstation font";
        }
    }
}
