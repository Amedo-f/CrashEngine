using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2
{
    public class PS2AnyLink : BaseTwinItem, ITwinLink
    {
        public List<TwinChunkLink> LinksList { get; set; }

        public PS2AnyLink()
        {
            LinksList = new List<TwinChunkLink>();
        }

        public override int GetLength()
        {
            return 4 + LinksList.Sum(l => l.GetLength());
        }

        public override void Read(BinaryReader reader, int length)
        {
            int links = reader.ReadInt32();
            LinksList.Clear();
            for (int i = 0; i < links; ++i)
            {
                TwinChunkLink link = new();
                link.Read(reader, length);
                LinksList.Add(link);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(LinksList.Count);
            foreach (ITwinSerializable e in LinksList)
            {
                e.Write(writer);
            }
        }

        public override String GetName()
        {
            return $"Chunk link {id:X}";
        }
    }
}
