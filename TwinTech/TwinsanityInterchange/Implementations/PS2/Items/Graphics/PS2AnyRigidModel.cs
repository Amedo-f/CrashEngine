using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics
{
    public class PS2AnyRigidModel : BaseTwinItem, ITwinRigidModel
    {
        public UInt32 Header { get; set; } // Unused by the game
        public List<UInt32> Materials { get; set; }
        public UInt32 Model { get; set; }

        public PS2AnyRigidModel()
        {
            Materials = new List<UInt32>();
        }

        public override Int32 GetLength()
        {
            return 12 + Materials.Count * Constants.SIZE_UINT32;
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            Header = reader.ReadUInt32();
            var matAmt = reader.ReadInt32();
            for (int i = 0; i < matAmt; ++i)
            {
                Materials.Add(reader.ReadUInt32());
            }
            Model = reader.ReadUInt32();
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(Header);
            writer.Write(Materials.Count);
            for (int i = 0; i < Materials.Count; ++i)
            {
                writer.Write(Materials[i]);
            }
            writer.Write(Model);
        }

        public override String GetName()
        {
            return $"Rigid Model {id:X}";
        }
    }
}
