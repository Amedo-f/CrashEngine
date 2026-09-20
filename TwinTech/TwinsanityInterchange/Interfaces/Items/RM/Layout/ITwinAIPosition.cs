using System;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout
{
    public interface ITwinAIPosition : ITwinItem
    {
        /// <summary>
        /// AI position
        /// </summary>
        Vector4 Position { get; set; }
        /// <summary>
        /// Unknown parameter
        /// </summary>
        UInt16 UnkShort { get; set; }
    }
}
