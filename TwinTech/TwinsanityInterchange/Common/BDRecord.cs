using System;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class BDRecord
    {
        public BDRecord(BHRecord head, Byte[] data)
        {
            Header = head;
            Data = data;
        }
        public BHRecord Header { get; }
        public Byte[] Data { get; set; }
    }
}
