using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class VertexJointInfo : ITwinSerializable
    {
        public Single Weight1;
        public Single Weight2;
        public Single Weight3;
        public Int32 JointIndex1;
        public Int32 JointIndex2;
        public Int32 JointIndex3;
        public Boolean Connection;

        public Int32 GetLength()
        {
            return Constants.SIZE_VECTOR4;
        }

        public Int32 GetJointConnectionsAmount()
        {
            var jointConnectionsAmount = 1;
            var totalWeight = Weight1 + Weight2;
            if (totalWeight < 1)
            {
                jointConnectionsAmount++;
            }
            if (totalWeight + Weight3 <= 1)
            {
                jointConnectionsAmount++;
            }

            return jointConnectionsAmount;
        }

        public Vector4 GetVector4()
        {
            Vector4 v = new()
            {
                X = Weight1,
                Y = Weight2,
                Z = Weight3
            };
            var xComp = (v.GetBinaryX() & 0xFFFFFE00) | (UInt32)(JointIndex1 * 4);
            var yComp = (v.GetBinaryY() & 0xFFFFFE00) | (UInt32)(JointIndex2 * 4);
            var zComp = (v.GetBinaryZ() & 0xFFFFFE00) | (UInt32)(JointIndex3 * 4);
            v.SetBinaryX(xComp);
            v.SetBinaryY(yComp);
            v.SetBinaryZ(zComp);
            UInt32 wComp = Connection ? 0 : 0x8000U;
            var weightCount = 1;
            var totalWeight = Weight1;
            if (totalWeight < 1 && Weight2 != 0)
            {
                totalWeight += Weight2;
                weightCount++;
            }
            if (totalWeight + Weight3 <= 1 && Weight3 != 0 && weightCount > 1)
            {
                weightCount++;
            }
            wComp |= (UInt32)weightCount;
            v.SetBinaryW(wComp);
            return v;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            throw new NotImplementedException();
        }

        public void Write(BinaryWriter writer)
        {
            var v = GetVector4();
            v.Write(writer);
        }
    }
}
