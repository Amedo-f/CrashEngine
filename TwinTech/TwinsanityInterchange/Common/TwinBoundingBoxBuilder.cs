using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class TwinBoundingBoxBuilder : ITwinSerializable
    {
        public List<Vector4> BoundingBoxPoints { get; set; }
        public List<Vector4> UnkVectors1 { get; set; }
        public List<Vector4> UnkVectors2 { get; set; }
        public List<Vector4> UnkVectors3 { get; set; }
        public List<UInt16> UnkShorts { get; set; }
        public List<Byte> UnkBytes1 { get; set; }
        public List<Byte> UnkBytes2 { get; set; }

        public TwinBoundingBoxBuilder()
        {
            BoundingBoxPoints = new List<Vector4>();
            UnkVectors1 = new List<Vector4>();
            UnkVectors2 = new List<Vector4>();
            UnkVectors3 = new List<Vector4>();
            UnkShorts = new List<UInt16>();
            UnkBytes1 = new List<Byte>();
            UnkBytes2 = new List<Byte>();
        }
        public int GetLength()
        {
            return 4 + 11 * 2 + BoundingBoxPoints.Count * Constants.SIZE_VECTOR4 + UnkVectors1.Count * Constants.SIZE_VECTOR4
                + UnkVectors2.Count * Constants.SIZE_VECTOR4 + UnkVectors3.Count * Constants.SIZE_VECTOR4 + UnkShorts.Count * 2
                + UnkBytes1.Count + UnkBytes2.Count;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, int length)
        {
            var header = new UInt16[11];
            for (var i = 0; i < 11; ++i)
            {
                header[i] = reader.ReadUInt16();
            }

            reader.ReadInt32(); // Packed binary data size size

            var vecs = header[0];
            for (var i = 0; i < vecs; ++i)
            {
                var vec = new Vector4();
                vec.Read(reader, Constants.SIZE_VECTOR4);
                BoundingBoxPoints.Add(vec);
            }

            var vecs2 = header[2];
            for (Int32 i = 0; i < vecs2; i++)
            {
                var vec = new Vector4();
                vec.Read(reader, Constants.SIZE_VECTOR4);
                UnkVectors1.Add(vec);
            }

            var vecs3 = header[3];
            for (Int32 i = 0; i < vecs3; i++)
            {
                var vec = new Vector4();
                vec.Read(reader, Constants.SIZE_VECTOR4);
                UnkVectors2.Add(vec);
            }

            var vecs4 = header[4];
            for (Int32 i = 0; i < vecs4; i++)
            {
                var vec = new Vector4();
                vec.Read(reader, Constants.SIZE_VECTOR4);
                UnkVectors3.Add(vec);
            }

            UnkBytes1 = reader.ReadBytes(header[9] - header[8]).ToList();
            UnkBytes2 = reader.ReadBytes(header[10] - header[9]).ToList();

            var shorts = header[1];
            for (Int32 i = 0; i < shorts; i++)
            {
                UnkShorts.Add(reader.ReadUInt16());
            }
        }

        public void Write(BinaryWriter writer)
        {
            var header = new UInt16[11];
            header[0] = (UInt16)BoundingBoxPoints.Count;
            header[1] = (UInt16)UnkShorts.Count;
            header[2] = (UInt16)UnkVectors1.Count;
            header[3] = (UInt16)UnkVectors2.Count;
            header[4] = (UInt16)UnkVectors3.Count;
            header[5] = (UInt16)(BoundingBoxPoints.Count * Constants.SIZE_VECTOR4);
            header[6] = (UInt16)(header[5] + UnkVectors1.Count * Constants.SIZE_VECTOR4);
            header[7] = (UInt16)(header[6] + UnkVectors2.Count * Constants.SIZE_VECTOR4);
            header[8] = (UInt16)(header[7] + UnkVectors3.Count * Constants.SIZE_VECTOR4);
            header[9] = (UInt16)(header[8] + UnkBytes1.Count);
            header[10] = (UInt16)(header[9] + UnkBytes2.Count);
            for (var i = 0; i < 11; ++i)
            {
                writer.Write(header[i]);
            }

            var blobSize = header[10] + UnkShorts.Count * 2;
            writer.Write(blobSize);

            foreach (var v in BoundingBoxPoints)
            {
                v.Write(writer);
            }

            foreach (var v in UnkVectors1)
            {
                v.Write(writer);
            }

            foreach (var v in UnkVectors2)
            {
                v.Write(writer);
            }

            foreach (var v in UnkVectors3)
            {
                v.Write(writer);
            }

            writer.Write(UnkBytes1.ToArray());
            writer.Write(UnkBytes2.ToArray());

            foreach (var s in UnkShorts)
            {
                writer.Write(s);
            }
        }
    }
}
