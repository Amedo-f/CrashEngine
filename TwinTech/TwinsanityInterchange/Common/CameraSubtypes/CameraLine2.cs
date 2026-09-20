using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    public class CameraLine2 : CameraSubBase
    {
        public Vector4 LineStart { get; set; }
        public Vector4 LineEnd { get; set; }
        public Single UnkFloat3 { get; set; }
        public Single UnkFloat4 { get; set; }
        public CameraLine2()
        {
            LineStart = new Vector4();
            LineEnd = new Vector4();
        }
        public override int GetLength()
        {
            return base.GetLength() + 8 + LineStart.GetLength() + LineEnd.GetLength();
        }

        public override void Read(BinaryReader reader, int length)
        {
            base.Read(reader, base.GetLength());
            LineStart.Read(reader, Constants.SIZE_VECTOR4);
            LineEnd.Read(reader, Constants.SIZE_VECTOR4);
            UnkFloat3 = reader.ReadSingle();
            UnkFloat4 = reader.ReadSingle();
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            LineStart.Write(writer);
            LineEnd.Write(writer);
            writer.Write(UnkFloat3);
            writer.Write(UnkFloat4);
        }

        public override ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.CameraLine2;
        }
    }
}
