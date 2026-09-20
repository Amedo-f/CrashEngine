using System;
using System.Collections.Generic;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items
{
    public interface ITwinSkydome : ITwinItem
    {
        /// <summary>
        /// Unused header
        /// </summary>
        Int32 Header { get; set; } // Unused by the game
        /// <summary>
        /// Meshe IDs to render in this skydome
        /// </summary>
        List<UInt32> Meshes { get; set; }
    }
}
