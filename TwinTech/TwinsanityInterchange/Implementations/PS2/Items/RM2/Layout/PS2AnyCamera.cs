using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout
{
    public class PS2AnyCamera : BaseTwinItem, ITwinCamera
    {
        public static readonly Dictionary<ITwinCamera.CameraType, Type> subCamIdToCamera = new();

        public TwinTrigger CamTrigger { get; set; }
        public UInt32 CameraHeader { get; set; }
        public UInt16 UnkShort { get; set; }
        public Single UnkFloat1 { get; set; } // 10
        public Vector4 UnkVector1 { get; set; }
        public Vector4 UnkVector2 { get; set; } // 42
        public Single UnkFloat2 { get; set; }
        public Single UnkFloat3 { get; set; } // 50
        public UInt32 UnkInt1 { get; set; }
        public UInt32 UnkInt2 { get; set; }
        public UInt32 UnkInt3 { get; set; }
        public UInt32 UnkInt4 { get; set; } // 66
        public UInt32 UnkInt5 { get; set; }
        public UInt32 UnkInt6 { get; set; } // 74
        public Single UnkFloat4 { get; set; }
        public Single UnkFloat5 { get; set; }
        public Single UnkFloat6 { get; set; }
        public Single UnkFloat7 { get; set; } // 90
        public UInt32 UnkInt7 { get; set; }
        public UInt32 UnkInt8 { get; set; } // 98
        public UInt32 UnkInt9 { get; set; }
        public Single UnkFloat8 { get; set; } // 106
        public ITwinCamera.CameraType TypeIndex1 { get; set; }
        public ITwinCamera.CameraType TypeIndex2 { get; set; } // 114
        public Byte UnkByte { get; set; } // 115
        public CameraSubBase MainCamera1 { get; set; }
        public CameraSubBase MainCamera2 { get; set; }
        public PS2AnyCamera()
        {
            CamTrigger = new TwinTrigger();
            UnkVector1 = new Vector4();
            UnkVector2 = new Vector4();
        }

        static PS2AnyCamera()
        {
            subCamIdToCamera.Add(ITwinCamera.CameraType.BossCamera, typeof(BossCamera));
            subCamIdToCamera.Add(ITwinCamera.CameraType.CameraPoint, typeof(CameraPoint));
            subCamIdToCamera.Add(ITwinCamera.CameraType.CameraLine, typeof(CameraLine));
            subCamIdToCamera.Add(ITwinCamera.CameraType.CameraPath, typeof(CameraPath));
            subCamIdToCamera.Add(ITwinCamera.CameraType.CameraSpline, typeof(CameraSpline));
            subCamIdToCamera.Add(ITwinCamera.CameraType.CameraSub1C09, typeof(CameraSub1C09));
            subCamIdToCamera.Add(ITwinCamera.CameraType.CameraPoint2, typeof(CameraPoint2));
            subCamIdToCamera.Add(ITwinCamera.CameraType.CameraSub1C0C, typeof(CameraSub1C0C));
            subCamIdToCamera.Add(ITwinCamera.CameraType.CameraLine2, typeof(CameraLine2));
            subCamIdToCamera.Add(ITwinCamera.CameraType.CameraZone, typeof(CameraZone));
        }

        public override int GetLength()
        {
            var mainCam1Len = MainCamera1 == null ? 0 : MainCamera1.GetLength();
            var mainCam2Len = MainCamera2 == null ? 0 : MainCamera2.GetLength();
            return CamTrigger.GetLength() + 115 + mainCam1Len + mainCam2Len;
        }

        public override void Read(BinaryReader reader, int length)
        {
            CamTrigger.Read(reader, length);
            // Camera
            CameraHeader = reader.ReadUInt32();
            UnkShort = reader.ReadUInt16();
            UnkFloat1 = reader.ReadSingle();
            UnkVector1.Read(reader, Constants.SIZE_VECTOR4);
            UnkVector2.Read(reader, Constants.SIZE_VECTOR4);
            UnkFloat2 = reader.ReadSingle();
            UnkFloat3 = reader.ReadSingle();
            UnkInt1 = reader.ReadUInt32();
            UnkInt2 = reader.ReadUInt32();
            UnkInt3 = reader.ReadUInt32();
            UnkInt4 = reader.ReadUInt32();
            UnkInt5 = reader.ReadUInt32();
            UnkInt6 = reader.ReadUInt32();
            UnkFloat4 = reader.ReadSingle();
            UnkFloat5 = reader.ReadSingle();
            UnkFloat6 = reader.ReadSingle();
            UnkFloat7 = reader.ReadSingle();
            UnkInt7 = reader.ReadUInt32();
            UnkInt8 = reader.ReadUInt32();
            UnkInt9 = reader.ReadUInt32();
            UnkFloat8 = reader.ReadSingle();
            TypeIndex1 = (ITwinCamera.CameraType)reader.ReadUInt32();
            TypeIndex2 = (ITwinCamera.CameraType)reader.ReadUInt32();
            UnkByte = reader.ReadByte();
            if (TypeIndex1 != ITwinCamera.CameraType.Null && subCamIdToCamera.ContainsKey(TypeIndex1))
            {
                MainCamera1 = (CameraSubBase)Activator.CreateInstance(subCamIdToCamera[TypeIndex1]);
                MainCamera1.Read(reader, length);
            }
            if (TypeIndex2 != ITwinCamera.CameraType.Null && subCamIdToCamera.ContainsKey(TypeIndex2))
            {
                MainCamera2 = (CameraSubBase)Activator.CreateInstance(subCamIdToCamera[TypeIndex2]);
                MainCamera2.Read(reader, length);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            CamTrigger.Write(writer);
            //
            writer.Write(CameraHeader);
            writer.Write(UnkShort);
            writer.Write(UnkFloat1);
            UnkVector1.Write(writer);
            UnkVector2.Write(writer);
            writer.Write(UnkFloat2);
            writer.Write(UnkFloat3);
            writer.Write(UnkInt1);
            writer.Write(UnkInt2);
            writer.Write(UnkInt3);
            writer.Write(UnkInt4);
            writer.Write(UnkInt5);
            writer.Write(UnkInt6);
            writer.Write(UnkFloat4);
            writer.Write(UnkFloat5);
            writer.Write(UnkFloat6);
            writer.Write(UnkFloat7);
            writer.Write(UnkInt7);
            writer.Write(UnkInt8);
            writer.Write(UnkInt9);
            writer.Write(UnkFloat8);
            writer.Write((UInt32)TypeIndex1);
            writer.Write((UInt32)TypeIndex2);
            writer.Write(UnkByte);
            MainCamera1?.Write(writer);
            MainCamera2?.Write(writer);
        }

        public override String GetName()
        {
            return $"Camera {id:X}";
        }
    }
}
