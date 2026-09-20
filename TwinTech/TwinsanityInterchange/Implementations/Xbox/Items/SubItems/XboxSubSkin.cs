using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.SubItems
{
    public class XboxSubSkin : ITwinSubSkin
    {
        List<UInt32> groupList = new();
        List<List<UInt32>> groupJoints = new();
        List<UInt32> packedNormals = new();

        public UInt32 Material { get; set; }
        public List<Vector4> Vertexes { get; set; }
        public List<Vector4> UVW { get; set; }
        public List<Vector4> Colors { get; set; }
        public List<VertexJointInfo> SkinJoints { get; set; }
        public List<Int32> GroupSizes { get; set; }

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
            var length = 20;
            length += groupList.Count * 8;
            for (Int32 i = 0; i < groupList.Count; i++)
            {
                length += groupJoints[i].Count * 4;
            }
            length += Vertexes.Count * 0x30;

            return length;
        }

        public UInt32 GetMinSkinCoord()
        {
            return 0;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            Material = reader.ReadUInt32();
            reader.ReadUInt32(); // Vertex amount * 0x30
            var vertexAmount = reader.ReadInt32();
            reader.ReadUInt32(); // Total amount of group joints
            var groupAmount = reader.ReadUInt32();

            GroupSizes = new();
            for (Int32 i = 0; i < groupAmount; i++)
            {
                groupList.Add(reader.ReadUInt32());
                GroupSizes.Add((Int32)groupList[^1]);
            }
            var jointAmountList = new List<UInt32>((Int32)groupAmount);
            for (Int32 i = 0; i < groupAmount; i++)
            {
                jointAmountList.Add(reader.ReadUInt32());
            }
            for (Int32 i = 0; i < groupAmount; i++)
            {
                var groupJointsList = new List<UInt32>();
                for (Int32 j = 0; j < jointAmountList[i]; j++)
                {
                    groupJointsList.Add(reader.ReadUInt32());
                }
                groupJoints.Add(groupJointsList);
            }

            Vertexes = new();
            Colors = new();
            UVW = new();
            SkinJoints = new();
            packedNormals = new();
            for (Int32 i = 0; i < vertexAmount; i++)
            {
                Vertexes.Add(new Vector4
                {
                    X = reader.ReadSingle(),
                    Y = reader.ReadSingle(),
                    Z = reader.ReadSingle(),
                    W = 1.0f
                });
                SkinJoints.Add(new VertexJointInfo
                {
                    Weight1 = reader.ReadSingle(),
                    Weight2 = reader.ReadSingle(),
                    Weight3 = reader.ReadSingle(),
                    JointIndex1 = reader.ReadUInt16(),
                    JointIndex2 = reader.ReadUInt16(),
                    JointIndex3 = reader.ReadUInt16(),
                    Connection = reader.ReadUInt16() == 0
                });
                packedNormals.Add(reader.ReadUInt32());
                Colors.Add(Vector4.FromColor(new Color
                {
                    R = reader.ReadByte(),
                    G = reader.ReadByte(),
                    B = reader.ReadByte(),
                    A = reader.ReadByte()
                }));
                UVW.Add(new Vector4
                {
                    X = reader.ReadSingle(),
                    Y = reader.ReadSingle(),
                    Z = 0.0f,
                    W = 1.0f,
                });
            }
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Material);
            writer.Write(Vertexes.Count * 0x30);
            writer.Write(Vertexes.Count);

            var groupJointCount = 0;
            for (Int32 i = 0; i < groupJoints.Count; i++)
            {
                groupJointCount += groupJoints[i].Count;
            }
            writer.Write(groupJointCount);

            writer.Write(groupList.Count);
            foreach (var group in groupList)
            {
                writer.Write(group);
            }
            for (Int32 i = 0; i < groupList.Count; i++)
            {
                writer.Write(groupJoints[i].Count);
            }
            for (Int32 i = 0; i < groupList.Count; i++)
            {
                for (Int32 j = 0; j < groupJoints[i].Count; j++)
                {
                    writer.Write(groupJoints[i][j]);
                }
            }

            for (Int32 i = 0; i < Vertexes.Count; i++)
            {
                writer.Write(Vertexes[i].X);
                writer.Write(Vertexes[i].Y);
                writer.Write(Vertexes[i].Z);
                writer.Write(SkinJoints[i].Weight1);
                writer.Write(SkinJoints[i].Weight2);
                writer.Write(SkinJoints[i].Weight3);
                writer.Write((UInt16)SkinJoints[i].JointIndex1);
                writer.Write((UInt16)SkinJoints[i].JointIndex2);
                writer.Write((UInt16)SkinJoints[i].JointIndex3);
                writer.Write(SkinJoints[i].Connection ? (UInt16)0 : (UInt16)1);
                writer.Write(packedNormals[i]);
                Colors[i].GetColor().Write(writer);
                writer.Write(UVW[i].X);
                writer.Write(UVW[i].Y);
            }
        }
    }
}
