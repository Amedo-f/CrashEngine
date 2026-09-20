using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.SubItems
{
    public class XboxBlendSkinFace : ITwinBlendSkinFace
    {
        public UInt32 VertexesAmount { get; set; }
        public List<VertexBlendShape> Vertices { get; set; }

        public XboxBlendSkinFace(UInt32 vertexesAmount)
        {
            VertexesAmount = vertexesAmount;
        }

        public void CalculateData()
        {
            // Data needs no decompression as it is already presented decompressed on read
        }

        public void Compile()
        {
            return;
        }

        public Int32 GetLength()
        {
            return 12 * Vertices.Count;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            Vertices = new();
            for (Int32 i = 0; i < VertexesAmount; i++)
            {
                Vertices.Add(new VertexBlendShape
                {
                    Offset = new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), 1.0f)
                });
            }
        }

        public void Write(BinaryWriter writer)
        {
            foreach (var shape in Vertices)
            {
                writer.Write(shape.Offset.X);
                writer.Write(shape.Offset.Y);
                writer.Write(shape.Offset.Z);
            }
        }
    }
}
