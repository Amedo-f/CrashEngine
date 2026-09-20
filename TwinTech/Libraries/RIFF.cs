using System;
using System.IO;

namespace Twinsanity.Libraries
{
    public static class RIFF
    {
        public static void SaveRiff(BinaryWriter writer, byte[] pcm, ref short channels, ref UInt32 samplerate)
        {
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + pcm.Length);
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);
            writer.Write((ushort)1);
            writer.Write(channels);
            writer.Write(samplerate);
            writer.Write((uint)(samplerate * channels * 2));
            writer.Write((short)(channels * 2));
            writer.Write((ushort)16);
            writer.Write("data".ToCharArray());
            writer.Write(pcm.Length);
            writer.Write(pcm);
        }
        public static byte[] LoadRiff(BinaryReader reader, ref byte[] pcm, ref short channels, ref UInt32 samplerate)
        {
            reader.BaseStream.Position = 22;
            channels = reader.ReadInt16();
            samplerate = reader.ReadUInt32();
            reader.BaseStream.Position += 12;
            var len = reader.ReadInt32();
            pcm = reader.ReadBytes(len);
            reader.BaseStream.Position = 0;
            return reader.ReadBytes((int)reader.BaseStream.Length);
        }
    }
}
