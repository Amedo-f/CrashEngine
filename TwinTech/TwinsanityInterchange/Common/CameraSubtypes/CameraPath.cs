using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    public class CameraPath : CameraSubBase
    {
        public List<Vector4> PathPoints { get; set; }
        public List<UInt64> UnkData { get; set; }
        public CameraPath()
        {
            PathPoints = new List<Vector4>();
            UnkData = new List<UInt64>();
        }
        public override int GetLength()
        {
            return base.GetLength() + 4 + PathPoints.Count * Constants.SIZE_VECTOR4 + 4 + UnkData.Count * 8;
        }

        public override void Read(BinaryReader reader, int length)
        {
            base.Read(reader, base.GetLength());
            int cnt1 = reader.ReadInt32();
            PathPoints.Clear();
            for (int i = 0; i < cnt1; ++i)
            {
                Vector4 vec = new Vector4();
                vec.Read(reader, Constants.SIZE_VECTOR4);
                PathPoints.Add(vec);
            }
            int cnt2 = reader.ReadInt32();
            UnkData.Clear();
            for (var i = 0; i < cnt2; ++i)
            {
                UnkData.Add(reader.ReadUInt64());
            }
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            writer.Write(PathPoints.Count);
            foreach (ITwinSerializable e in PathPoints)
            {
                e.Write(writer);
            }
            writer.Write(UnkData.Count);
            foreach (var unk in UnkData)
            {
                writer.Write(unk);
            }
        }

        public override ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.CameraPath;
        }
    }
}
