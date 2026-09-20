using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    public class CameraPoint2 : CameraSubBase
    {
        public Vector4 Point { get; set; }
        public Single UnkFloat3 { get; set; }
        public Byte UnkByte { get; set; }
        public CameraPoint2()
        {
            Point = new Vector4();
        }
        public override int GetLength()
        {
            return base.GetLength() + 5 + Constants.SIZE_VECTOR4;
        }

        public override void Read(BinaryReader reader, int length)
        {
            base.Read(reader, base.GetLength());
            Point.Read(reader, Constants.SIZE_VECTOR4);
            UnkFloat3 = reader.ReadSingle();
            UnkByte = reader.ReadByte();
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            Point.Write(writer);
            writer.Write(UnkFloat3);
            writer.Write(UnkByte);
        }

        public override ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.CameraPoint2;
        }
    }
}
