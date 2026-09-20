using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class TwinTrigger : ITwinSerializable
    {
        public UInt32 Header { get; set; }
        public TriggerActivatorObjects ObjectActivatorMask { get; set; }
        public Single UnkFloat { get; set; }
        public UInt32 InstanceExtensionValue { get; set; }
        public Vector4 Rotation { get; set; }
        public Vector4 Position { get; set; }
        public Vector4 Scale { get; set; }
        public List<UInt16> Instances { get; }
        public TwinTrigger()
        {
            Rotation = new Vector4();
            Position = new Vector4();
            Scale = new Vector4();
            Instances = new List<UInt16>();
        }

        public int GetLength()
        {
            return 12 + Position.GetLength() + Rotation.GetLength() + Scale.GetLength() + 12 + Instances.Count * Constants.SIZE_UINT16;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, int length)
        {
            Header = reader.ReadUInt32();
            ObjectActivatorMask = (TriggerActivatorObjects)reader.ReadUInt32();
            UnkFloat = reader.ReadSingle();
            Rotation.Read(reader, Constants.SIZE_VECTOR4);
            Position.Read(reader, Constants.SIZE_VECTOR4);
            Scale.Read(reader, Constants.SIZE_VECTOR4);
            reader.ReadUInt32(); // instances amount
            UInt32 instances_cnt = reader.ReadUInt32();
            InstanceExtensionValue = reader.ReadUInt32();
            Instances.Clear();
            for (int i = 0; i < instances_cnt; ++i)
            {
                Instances.Add(reader.ReadUInt16());
            }
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Header);
            writer.Write((UInt32)ObjectActivatorMask);
            writer.Write(UnkFloat);
            Rotation.Write(writer);
            Position.Write(writer);
            Scale.Write(writer);
            writer.Write(Instances.Count);
            writer.Write(Instances.Count);
            writer.Write(InstanceExtensionValue);
            for (int i = 0; i < Instances.Count; ++i)
            {
                writer.Write(Instances[i]);
            }
        }

        // Amedo 2026-09-04 -- Rotation is a real axis-angle quaternion (angle=2*acos(W),
        // axis=(X,Y,Z)/sin(angle/2)); guards the near-zero degenerate case like the reference tool.
        public void GetAxisAngle(out Vector3 axis, out float angleRadians) => DecodeAxisAngle(Rotation, out axis, out angleRadians);

        // Static form -- lets callers decode a raw Rotation Vector4 (e.g. read straight off the
        // file, or a value not yet attached to a live TwinTrigger instance) without needing a
        // whole TwinTrigger object around it.
        public static void DecodeAxisAngle(Vector4 rotation, out Vector3 axis, out float angleRadians)
        {
            float w = Math.Clamp(rotation.W, -1f, 1f);
            float half = MathF.Acos(w);
            float s = MathF.Sin(half);
            if (MathF.Abs(s) < 1e-6f)
            {
                axis = new Vector3(0f, 0f, 1f);
                angleRadians = 0f;
                return;
            }
            axis = new Vector3(rotation.X / s, rotation.Y / s, rotation.Z / s);
            angleRadians = half * 2f;
        }

        // Inverse of GetAxisAngle above -- writes a normalized axis-angle rotation back into
        // Rotation as a quaternion (W = cos(angle/2), XYZ = axis*sin(angle/2)). This is the
        // standard mathematical inverse of the confirmed decode above, NOT a copy of the
        // reference tool's own encoder (its numericUpDown18/19/20/21 handlers multiply by
        // sin(angle*2) instead of sin(angle/2) and don't actually round-trip its own decode --
        // a bug in their tool, not something worth reproducing here).
        public void SetAxisAngle(Vector3 axis, float angleRadians)
        {
            float len = MathF.Sqrt(axis.X * axis.X + axis.Y * axis.Y + axis.Z * axis.Z);
            Vector3 n = len < 1e-6f ? new Vector3(0f, 0f, 1f) : new Vector3(axis.X / len, axis.Y / len, axis.Z / len);
            float half = angleRadians * 0.5f;
            float s = MathF.Sin(half);
            Rotation.W = MathF.Cos(half);
            Rotation.X = n.X * s;
            Rotation.Y = n.Y * s;
            Rotation.Z = n.Z * s;
        }
    }
}
