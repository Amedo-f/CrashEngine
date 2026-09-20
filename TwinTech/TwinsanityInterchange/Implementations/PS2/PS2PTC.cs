using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2
{
    public class PS2PTC : BaseTwinItem, ITwinPTC
    {
        public UInt32 TexID { get; set; }
        public UInt32 MatID { get; set; }
        public ITwinTexture Texture { get; set; }
        public ITwinMaterial Material { get; set; }

        public override Int32 GetLength()
        {
            return 8 + Texture.GetLength() + Material.GetLength();
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            TexID = reader.ReadUInt32();
            MatID = reader.ReadUInt32();
            Texture = new PS2AnyTexture();
            Texture.Read(reader, length);
            Material = new PS2AnyMaterial();
            Material.Read(reader, length);
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(TexID);
            writer.Write(MatID);
            Texture.Write(writer);
            Material.Write(writer);
        }

        public override String GetName()
        {
            return "PTC";
        }
    }
}
