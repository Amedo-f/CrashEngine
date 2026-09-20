using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout
{
    public class PS2AnyTrigger : BaseTwinItem, ITwinTrigger
    {
        public TwinTrigger Trigger { get; set; }
        public UInt16[] TriggerMessages { get; set; }
        public PS2AnyTrigger()
        {
            TriggerMessages = new UInt16[4];
            Trigger = new TwinTrigger();
        }

        public override int GetLength()
        {
            return Trigger.GetLength() + 8;
        }

        public override void Read(BinaryReader reader, int length)
        {
            Trigger.Read(reader, length);
            for (int i = 0; i < 4; ++i)
            {
                TriggerMessages[i] = reader.ReadUInt16();
            }
        }

        public override void Write(BinaryWriter writer)
        {
            Trigger.Write(writer);
            for (int i = 0; i < 4; ++i)
            {
                writer.Write(TriggerMessages[i]);
            }
        }

        public override String GetName()
        {
            return $"Trigger {id:X}";
        }
    }
}
