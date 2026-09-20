using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics
{
    public class PS2AnySkydome : BaseTwinItem, ITwinSkydome
    {

        public Int32 Header { get; set; } // Unused by the game
        public List<UInt32> Meshes { get; set; }

        public PS2AnySkydome()
        {
            Meshes = new List<UInt32>();
        }

        public override int GetLength()
        {
            return 8 + Meshes.Count * Constants.SIZE_UINT32;
        }

        public override void Read(BinaryReader reader, int length)
        {
            Header = reader.ReadInt32();
            var meshes = reader.ReadInt32();
            for (int i = 0; i < meshes; ++i)
            {
                Meshes.Add(reader.ReadUInt32());
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(Header);
            writer.Write(Meshes.Count);
            for (int i = 0; i < Meshes.Count; ++i)
            {
                writer.Write(Meshes[i]);
            }
        }

        public override String GetName()
        {
            return $"Skydome {id:X}";
        }
    }
}
