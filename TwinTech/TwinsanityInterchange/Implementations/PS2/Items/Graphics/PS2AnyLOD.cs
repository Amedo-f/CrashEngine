using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics
{
    public class PS2AnyLOD : BaseTwinItem, ITwinLOD
    {
        /// <summary>
        /// LOD type. Prefer using <code>LodType.COMPRESSED</code>
        /// </summary>
        public LodType Type { get; set; }
        /// <summary>
        /// Minimum draw distance of the LOD
        /// </summary>
        public Int32 MinDrawDistance { get; set; }
        /// <summary>
        /// Maximum draw distance of the LOD
        /// </summary>
        public Int32 MaxDrawDistance { get; set; }
        /// <summary>
        /// Draw distances for each LOD level. Must be 3 levels
        /// </summary>
        public Int32[] ModelsDrawDistances { get; set; }
        /// <summary>
        /// Mesh for each of the LOD level
        /// </summary>
        public List<UInt32> Meshes { get; set; }

        public PS2AnyLOD()
        {
            ModelsDrawDistances = new Int32[3];
            Meshes = new List<UInt32>();
        }

        public override int GetLength()
        {
            if (Type == LodType.FULL)
            {
                return 28 + Meshes.Count * Constants.SIZE_UINT32;
            }
            return 25 + Meshes.Count * Constants.SIZE_UINT32;
        }

        public override void Read(BinaryReader reader, int length)
        {
            Type = (LodType)reader.ReadInt32();
            if (Type == LodType.FULL)
            {
                var meshAmt = reader.ReadInt32() & 0xFF;
                MinDrawDistance = reader.ReadInt32();
                MaxDrawDistance = reader.ReadInt32();
                for (int i = 0; i < 3; ++i)
                {
                    ModelsDrawDistances[i] = reader.ReadInt32();
                }
                reader.ReadInt32(); // Unused by the game
                for (int i = 0; i < meshAmt; ++i)
                {
                    Meshes.Add(reader.ReadUInt32());
                }
            }
            else
            {
                var meshAmt = reader.ReadByte();
                MinDrawDistance = reader.ReadInt32();
                MaxDrawDistance = reader.ReadInt32();
                for (int i = 0; i < 3; ++i)
                {
                    ModelsDrawDistances[i] = reader.ReadInt32();
                }
                for (int i = 0; i < meshAmt; ++i)
                {
                    Meshes.Add(reader.ReadUInt32());
                }
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write((Int32)Type);
            if (Type == LodType.FULL)
            {
                writer.Write(Meshes.Count);
                writer.Write(MinDrawDistance);
                writer.Write(MaxDrawDistance);
                for (int i = 0; i < 3; ++i)
                {
                    writer.Write(ModelsDrawDistances[i]);
                }
                writer.Write(0);
                for (int i = 0; i < Meshes.Count; ++i)
                {
                    writer.Write(Meshes[i]);
                }
            }
            else
            {
                writer.Write((byte)Meshes.Count);
                writer.Write(MinDrawDistance);
                writer.Write(MaxDrawDistance);
                for (int i = 0; i < 3; ++i)
                {
                    writer.Write(ModelsDrawDistances[i]);
                }
                for (int i = 0; i < Meshes.Count; ++i)
                {
                    writer.Write(Meshes[i]);
                }
            }
        }

        public override String GetName()
        {
            return $"LOD {id:X}";
        }
    }
}
