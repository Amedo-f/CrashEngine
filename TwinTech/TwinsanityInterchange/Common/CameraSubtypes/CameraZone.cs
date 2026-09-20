using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    public class CameraZone : CameraSubBase
    {
        public Vector4[] UnkData1 { get; set; }
        public Vector4[] UnkData2 { get; set; }
        public CameraZone()
        {
            UnkData1 = new Vector4[5];
            UnkData2 = new Vector4[5];
        }
        public override int GetLength()
        {
            return 160;
        }

        public override void Read(BinaryReader reader, int length)
        {
            for (var i = 0; i < 5; ++i)
            {
                UnkData1[i] = new Vector4();
                UnkData1[i].Read(reader, Constants.SIZE_VECTOR4);
            }
            for (var i = 0; i < 5; ++i)
            {
                UnkData2[i] = new Vector4();
                UnkData2[i].Read(reader, Constants.SIZE_VECTOR4);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            for (var i = 0; i < 5; ++i)
            {
                UnkData1[i].Write(writer);
            }
            for (var i = 0; i < 5; ++i)
            {
                UnkData2[i].Write(writer);
            }
        }

        public override ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.CameraZone;
        }
    }
}
