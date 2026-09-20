using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes
{
    public class TwinSceneryRoot : TwinSceneryNode
    {
        public UInt32 UnkUInt;

        public override Int32 GetLength()
        {
            return base.GetLength() + 4;
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            UnkUInt = reader.ReadUInt32();
            base.Read(reader, length);
        }

        public override void Read(BinaryReader reader, Int32 length, IList<TwinSceneryBaseType> sceneries)
        {
            base.Read(reader, length, sceneries);
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(UnkUInt);
            base.Write(writer);
        }

        public override ITwinScenery.SceneryType GetObjectIndex()
        {
            return ITwinScenery.SceneryType.Root;
        }
    }
}
