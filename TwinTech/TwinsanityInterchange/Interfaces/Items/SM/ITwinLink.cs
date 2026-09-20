using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.SM
{
    public interface ITwinLink : ITwinItem
    {
        /// <summary>
        /// Linked chunks info
        /// </summary>
        List<TwinChunkLink> LinksList { get; set; }
    }
}
