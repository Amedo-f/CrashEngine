using System;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class TwinObjectTriggerBehaviour
    {
        public UInt16 MessageID { get; set; }
        public UInt16 TriggerBehaviour { get; set; }
        public Byte BehaviourCallerIndex { get; set; }

        public TwinObjectTriggerBehaviour() { }

        public TwinObjectTriggerBehaviour(UInt32 value)
        {
            MessageID = (UInt16)(value & 0x3FF);
            TriggerBehaviour = (UInt16)(value >> 0xA & 0x3FFF);
            BehaviourCallerIndex = (Byte)(value >> 0x18 & 0x1);
        }

        public UInt32 Compress()
        {
            UInt32 result = MessageID;
            result |= (UInt32)((TriggerBehaviour & 0x3FFF) << 0xA);
            result |= (UInt32)((BehaviourCallerIndex & 0x1) << 0x18);

            return result;
        }
    }
}
