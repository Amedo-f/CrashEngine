using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout
{
    public class PS2AnyTemplate : BaseTwinItem, ITwinTemplate
    {
        public String Name { get; set; }
        public UInt16 ObjectId { get; set; }
        public Byte UnkByte1 { get; set; }
        public Byte UnkByte2 { get; set; }
        public List<UInt16> UnkBehaviourIds { get; set; }
        public UInt32 Header1 { get; set; }
        public UInt32 Header2 { get; set; }
        public Byte UnkByte3 { get; set; }
        public Byte UnkByte4 { get; set; }
        public UInt32 InstancePropsHeader { get; set; }
        public UInt32 UnkInt1 { get; set; }
        public List<UInt32> Flags { get; set; }
        public List<Single> Floats { get; set; }
        public List<UInt32> Ints { get; set; }
        public PS2AnyTemplate()
        {
            UnkBehaviourIds = new List<ushort>();
            Floats = new List<float>();
            Ints = new List<uint>();
            Flags = new List<uint>();
        }

        public override int GetLength()
        {
            return 4 + Name.Length + 16 + UnkBehaviourIds.Count * 2 + 22 + Flags.Count * 4 + Floats.Count * 4 + Ints.Count * 4;
        }

        public override void Read(BinaryReader reader, int length)
        {
            Int32 NameLen = reader.ReadInt32();
            Name = new string(reader.ReadChars(NameLen));
            ObjectId = reader.ReadUInt16();
            UnkByte1 = reader.ReadByte();
            UnkByte2 = reader.ReadByte();
            var amt = reader.ReadUInt32();
            Header1 = reader.ReadUInt32();
            Header2 = reader.ReadUInt32();
            UnkBehaviourIds.Clear();
            for (var i = 0; i < amt; ++i)
            {
                UnkBehaviourIds.Add(reader.ReadUInt16());
            }
            UnkByte3 = reader.ReadByte();
            UnkByte4 = reader.ReadByte();
            InstancePropsHeader = reader.ReadUInt32();
            UnkInt1 = reader.ReadUInt32();
            Int32 flags = reader.ReadInt32();
            Flags.Clear();
            for (int i = 0; i < flags; ++i)
            {
                Flags.Add(reader.ReadUInt32());
            }
            Int32 floats = reader.ReadInt32();
            Floats.Clear();
            for (int i = 0; i < floats; ++i)
            {
                Floats.Add(reader.ReadSingle());
            }
            Int32 ints = reader.ReadInt32();
            Ints.Clear();
            for (int i = 0; i < ints; ++i)
            {
                Ints.Add(reader.ReadUInt32());
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(Name.Length);
            writer.Write(Name.ToCharArray());
            writer.Write(ObjectId);
            writer.Write(UnkByte1);
            writer.Write(UnkByte2);
            writer.Write(UnkBehaviourIds.Count);
            writer.Write(Header1);
            writer.Write(Header2);
            foreach (var s in UnkBehaviourIds)
            {
                writer.Write(s);
            }
            writer.Write(UnkByte3);
            writer.Write(UnkByte4);
            writer.Write(InstancePropsHeader);
            writer.Write(UnkInt1);
            writer.Write(Flags.Count);
            foreach (UInt32 e in Flags)
            {
                writer.Write(e);
            }
            writer.Write(Floats.Count);
            foreach (Single e in Floats)
            {
                writer.Write(e);
            }
            writer.Write(Ints.Count);
            foreach (UInt32 e in Ints)
            {
                writer.Write(e);
            }
        }

        public override String GetName()
        {
            return Name;
        }
    }
}
