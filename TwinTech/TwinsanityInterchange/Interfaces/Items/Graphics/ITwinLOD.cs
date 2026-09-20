using System;
using System.Collections.Generic;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items
{
    public interface ITwinLOD : ITwinItem
    {
        LodType Type { get; set; }
        Int32 MinDrawDistance { get; set; }
        Int32 MaxDrawDistance { get; set; }
        Int32[] ModelsDrawDistances { get; set; }
        List<UInt32> Meshes { get; set; }
    }
}
