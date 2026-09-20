using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    public class CameraLine : CameraSubBase
    {
        public Vector4 LineStart { get; set; }
        public Vector4 LineEnd { get; set; }
        public CameraLine()
        {
            LineStart = new Vector4();
            LineEnd = new Vector4();
        }
        public override int GetLength()
        {
            return base.GetLength() + LineStart.GetLength() + LineEnd.GetLength();
        }

        public override void Read(BinaryReader reader, int length)
        {
            base.Read(reader, base.GetLength());
            LineStart.Read(reader, Constants.SIZE_VECTOR4);
            LineEnd.Read(reader, Constants.SIZE_VECTOR4);
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            LineStart.Write(writer);
            LineEnd.Write(writer);
        }

        public override ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.CameraLine;
        }
    }
}
