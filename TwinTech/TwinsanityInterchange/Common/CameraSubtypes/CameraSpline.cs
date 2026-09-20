using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    public class CameraSpline : CameraSubBase
    {
        public Single UnkFloat3 { get; set; }
        public List<Vector4> PathPoints { get; set; }
        public List<Vector4> InterpolationPoints { get; set; }
        public List<Vector2> UnkData { get; set; }
        public UInt16 UnkShort { get; set; }
        public CameraSpline()
        {
            PathPoints = new List<Vector4>();
            InterpolationPoints = new List<Vector4>();
            UnkData = new List<Vector2>();
        }
        public override int GetLength()
        {
            return base.GetLength() + 4 + 4 + PathPoints.Count * Constants.SIZE_VECTOR4 + InterpolationPoints.Count * Constants.SIZE_VECTOR4 + UnkData.Count * Constants.SIZE_VECTOR2 + 2;
        }

        public override void Read(BinaryReader reader, int length)
        {
            base.Read(reader, base.GetLength());
            int cnt1 = reader.ReadInt32();
            UnkFloat3 = reader.ReadSingle();
            PathPoints.Clear();
            InterpolationPoints.Clear();
            for (int i = 0; i < cnt1 + 1; ++i)
            {
                Vector4 vec = new();
                vec.Read(reader, Constants.SIZE_VECTOR4);
                PathPoints.Add(vec);
                Vector4 vec2 = new();
                vec2.Read(reader, Constants.SIZE_VECTOR4);
                InterpolationPoints.Add(vec2);
            }
            UnkData.Clear();
            for (var i = 0; i < cnt1; ++i)
            {
                Vector2 vec = new();
                vec.Read(reader, Constants.SIZE_VECTOR2);
                UnkData.Add(vec);
            }
            UnkShort = reader.ReadUInt16();
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            writer.Write(UnkData.Count);
            writer.Write(UnkFloat3);
            for (var i = 0; i < PathPoints.Count; ++i)
            {
                PathPoints[i].Write(writer);
                InterpolationPoints[i].Write(writer);
            }
            foreach (var d in UnkData)
            {
                d.Write(writer);
            }
            writer.Write(UnkShort);
        }

        public override ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.CameraSpline;
        }
    }
}
