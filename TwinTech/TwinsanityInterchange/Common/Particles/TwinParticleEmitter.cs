using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.Particles
{
    public class TwinParticleEmitter : ITwinSerializable
    {
        public UInt32 Version;
        public Vector3 Position;
        public Int16 UnkShort1;
        public Int16 UnkShort2;
        public Int16 UnkShort3;
        public Int16 UnkShort4;
        public Int16 UnkShort5;
        public Int32 UnkInt1;
        public Char[] Name;
        public Int32 UnkInt2;
        public Int32 UnkInt3;
        public Single UnkFloat1;
        public Int16 UnkShort6;
        public Int16 UnkShort7;
        public Single UnkFloat2;
        public Single UnkFloat3;
        public Int16 UnkShort8;

        private Dictionary<UInt32, Int32> versionSizeMap = new Dictionary<UInt32, Int32>();
        public TwinParticleEmitter()
        {
            Position = new Vector3();
            Name = new Char[16];
            Version = 0x1E;
        }
        public TwinParticleEmitter(UInt32 ver) : this()
        {
            Version = ver;
        }

        public Int32 GetLength()
        {
            return versionSizeMap[Version];
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            var basePos = reader.BaseStream.Position;
            Position.Read(reader, Constants.SIZE_VECTOR3);
            if (Version >= 0x7)
            {
                UnkShort1 = reader.ReadInt16();
                UnkShort2 = reader.ReadInt16();
                UnkShort3 = reader.ReadInt16();
                UnkShort4 = reader.ReadInt16();
            }
            else
            {
                UnkShort1 = 0;
                UnkShort2 = 0;
                UnkShort3 = (Int16)reader.ReadInt32();
                UnkShort4 = (Int16)reader.ReadInt32();
            }
            if (Version >= 0x16)
            {
                UnkShort5 = reader.ReadInt16();
            }
            if (Version >= 0x8)
            {
                UnkInt1 = reader.ReadInt32();
            }
            Name = reader.ReadChars(16);
            if (Version >= 0x9)
            {
                UnkInt2 = reader.ReadInt32();
                UnkInt3 = reader.ReadInt32();
                UnkFloat1 = reader.ReadSingle();
            }
            if (Version >= 0xC)
            {
                UnkShort6 = reader.ReadInt16();
                UnkShort7 = reader.ReadInt16();
                UnkFloat2 = reader.ReadSingle();
            }
            UnkFloat3 = 0.89999998f;
            if (Version >= 0xD)
            {
                UnkFloat3 = reader.ReadSingle();
            }
            if (Version >= 0xF)
            {
                UnkShort8 = reader.ReadInt16();
            }
            var sizePos = reader.BaseStream.Position;
            versionSizeMap.Add(Version, (Int32)(sizePos - basePos));
        }

        public void Write(BinaryWriter writer)
        {
            Position.Write(writer);
            if (Version >= 0x7)
            {
                writer.Write(UnkShort1);
                writer.Write(UnkShort2);
                writer.Write(UnkShort3);
                writer.Write(UnkShort4);
            }
            else
            {
                writer.Write((Int32)UnkShort3);
                writer.Write((Int32)UnkShort4);
            }
            if (Version >= 0x16)
            {
                writer.Write(UnkShort5);
            }
            if (Version >= 0x8)
            {
                writer.Write(UnkInt1);
            }
            writer.Write(Name, 0, 16);
            if (Version >= 0x9)
            {
                writer.Write(UnkInt2);
                writer.Write(UnkInt3);
                writer.Write(UnkFloat1);
            }
            if (Version >= 0xC)
            {
                writer.Write(UnkShort6);
                writer.Write(UnkShort7);
                writer.Write(UnkFloat2);
            }
            if (Version >= 0xD)
            {
                writer.Write(UnkFloat3);
            }
            if (Version >= 0xF)
            {
                writer.Write(UnkShort8);
            }
        }
    }
}
