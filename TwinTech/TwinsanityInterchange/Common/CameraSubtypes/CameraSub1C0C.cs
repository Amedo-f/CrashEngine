using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    public class CameraSub1C0C : CameraSubBase
    {
        public Byte[] UnkData { get; set; }
        public CameraSub1C0C()
        {
            UnkData = new byte[4];
        }
        public override int GetLength()
        {
            return 4;
        }

        public override void Read(BinaryReader reader, int length)
        {
            UnkData = reader.ReadBytes(4);
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(UnkData);
        }

        public override ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.CameraSub1C0C;
        }
    }
}
