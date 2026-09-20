using System;
using System.Collections.Generic;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items
{
    public interface ITwinRigidModel : ITwinItem
    {
        /// <summary>
        /// Unused header
        /// </summary>
        UInt32 Header { get; set; } // Unused by the game
        /// <summary>
        /// List of materials to apply to each submodel of a model
        /// </summary>
        List<UInt32> Materials { get; set; }
        /// <summary>
        /// Model ID to use
        /// </summary>
        UInt32 Model { get; set; }
    }
}
