using System;
using System.Collections.Generic;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout
{
    /// <summary>
    /// Seems to be in-engine exclusive. Only encountered in Default
    /// </summary>
    public interface ITwinTemplate : ITwinItem
    {
        String Name { get; set; }
        UInt16 ObjectId { get; set; }
        Byte UnkByte1 { get; set; }
        Byte UnkByte2 { get; set; }
        List<UInt16> UnkBehaviourIds { get; set; }
        UInt32 Header1 { get; set; }
        UInt32 Header2 { get; set; }
        Byte UnkByte3 { get; set; }
        Byte UnkByte4 { get; set; }
        UInt32 InstancePropsHeader { get; set; }
        UInt32 UnkInt1 { get; set; }
        List<UInt32> Flags { get; set; }
        List<Single> Floats { get; set; }
        List<UInt32> Ints { get; set; }
    }
}
