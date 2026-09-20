using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.Collision
{
    public class TwinCollisionTrigger : ITwinSerializable
    {
        public Vector3 V1;
        public Int32 MinTriggerIndex;
        public Vector3 V2;
        public Int32 MaxTriggerIndex;

        public TwinCollisionTrigger()
        {
            V1 = new Vector3();
            V2 = new Vector3();
        }

        public Int32 GetLength()
        {
            return Constants.SIZE_VECTOR3 + Constants.SIZE_VECTOR3 + 8;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            V1.Read(reader, Constants.SIZE_VECTOR3);
            MinTriggerIndex = reader.ReadInt32();
            V2.Read(reader, Constants.SIZE_VECTOR3);
            MaxTriggerIndex = reader.ReadInt32();
        }

        public void Write(BinaryWriter writer)
        {
            V1.Write(writer);
            writer.Write(MinTriggerIndex);
            V2.Write(writer);
            writer.Write(MaxTriggerIndex);
        }
    }
}
